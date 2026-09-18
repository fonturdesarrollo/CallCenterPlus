namespace CallCenterPlus.Authorization;

// Like RequireModuleAttribute, but checked against Security.GroupHasAccessToModule
// by numeric SecurityModuleId instead of by module name. Access is granted if the
// group has ANY of the listed module ids.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireModuleIdAttribute : Attribute
{
    public int[] ModuleIds { get; }

    public RequireModuleIdAttribute(params int[] moduleIds)
    {
        ModuleIds = moduleIds;
    }
}
