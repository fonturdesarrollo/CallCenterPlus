using System.Reflection;
using CallCenterPlus.Core;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallCenterPlus.Authorization;

// Same rule the sidebar in _AgentLayout.cshtml uses to decide what to show,
// applied here to actually block the request instead of just hiding a link.
public static class ModuleAccessControl
{
    private const int AdminGroupId = 1;

    public static bool HasAccess(ActionExecutingContext context, AgentUser? agent, ISecurity security)
    {
        if (agent is null)
        {
            return false;
        }

        // Group 1 is the system administrator group: always full access,
        // regardless of what SecurityGroupModule actually has assigned.
        if (agent.SecurityGroupId == AdminGroupId)
        {
            return true;
        }

        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return true;
        }

        var isAdminOnly = descriptor.MethodInfo.GetCustomAttribute<AdminOnlyAttribute>() is not null
            || descriptor.ControllerTypeInfo.GetCustomAttribute<AdminOnlyAttribute>() is not null;
        if (isAdminOnly)
        {
            return false;
        }

        var requiredModuleId = descriptor.MethodInfo.GetCustomAttribute<RequireModuleIdAttribute>()
            ?? descriptor.ControllerTypeInfo.GetCustomAttribute<RequireModuleIdAttribute>();
        if (requiredModuleId is not null)
        {
            try
            {
                return requiredModuleId.ModuleIds.Any(moduleId => security.GroupHasAccessToModule(agent.SecurityGroupId, moduleId));
            }
            catch
            {
                // Fail closed: an error checking permissions should not grant access.
                return false;
            }
        }

        var requiredModule = descriptor.MethodInfo.GetCustomAttribute<RequireModuleAttribute>()
            ?? descriptor.ControllerTypeInfo.GetCustomAttribute<RequireModuleAttribute>();
        if (requiredModule is null)
        {
            // No restriction declared on this action.
            return true;
        }

        try
        {
            var moduleNamesById = security.GetAllModules()
                .Where(m => m.SecurityModuleName is not null)
                .ToDictionary(m => m.SecurityModuleId, m => m.SecurityModuleName!);

            return security.GetModulesByGroupId(agent.SecurityGroupId)
                .Any(assigned => moduleNamesById.TryGetValue(assigned.SecurityModuleId, out var name)
                    && string.Equals(name, requiredModule.ModuleName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // Fail closed: an error checking permissions should not grant access.
            return false;
        }
    }
}
