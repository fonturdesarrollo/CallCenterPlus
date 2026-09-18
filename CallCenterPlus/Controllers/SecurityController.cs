using CallCenterPlus.Authorization;
using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallCenterPlus.Controllers;

// Group 1 (Admin) always has full access to every action here, via
// ModuleAccessControl. Non-admins can reach AddUser/Users/EditUser if their
// group has SecurityModule id 3/4 respectively; everything else (groups,
// modules, group-module assignments) stays Admin-only.
public class SecurityController : Controller
{
    private const string SessionAgentKey = "CurrentAgent";
    private const string TempDataSuccessKey = "SecuritySuccessMessage";
    private const string TempDataErrorKey = "SecurityErrorMessage";
    private const int AdminGroupId = 1;

    private readonly ISecurity _security;
    private readonly IGeography _geography;
    private readonly ILogger<SecurityController> _logger;

    private AgentUser? CurrentAgent { get; set; }

    public SecurityController(ISecurity security, IGeography geography, ILogger<SecurityController> logger)
    {
        _security = security;
        _geography = geography;
        _logger = logger;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var agent = HttpContext.Session.GetObject<AgentUser>(SessionAgentKey);
        if (agent is null)
        {
            context.Result = RedirectToAction("Login", "AgentAccount");
            return;
        }

        if (!ModuleAccessControl.HasAccess(context, agent, _security))
        {
            context.Result = RedirectToAction("AccessDenied", "Agent");
            return;
        }

        ViewBag.CurrentAgent = agent;
        CurrentAgent = agent;

        base.OnActionExecuting(context);
    }

    private void PopulateLookups(AddSecurityUserViewModel model)
    {
        model.AvailableGroups = _security.GetAllGroups();
        model.AvailableStatuses = _security.GetAllUsersStatus();
        model.AvailableStates = _geography.GetAllStates();
    }

    private AddGroupModuleViewModel BuildAddGroupModuleViewModel(SecurityGroupModuleModel? assignment = null)
    {
        var model = new AddGroupModuleViewModel
        {
            Assignment = assignment ?? new SecurityGroupModuleModel(),
        };

        try
        {
            model.AvailableGroups = _security.GetAllGroups();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los grupos (Security.GetAllGroups)");
        }

        try
        {
            model.AvailableModules = _security.GetAllModules();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los módulos (Security.GetAllModules)");
        }

        try
        {
            model.AvailableAccessTypes = _security.GetAllAccessTypes();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los tipos de acceso (Security.GetAllAccessTypes)");
        }

        try
        {
            model.ExistingAssignments = _security.GetAllGroupModules();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los módulos asignados a grupos (Security.GetAllGroupModules)");
        }

        return model;
    }

    [HttpGet]
    [RequireModuleId(3)]
    public IActionResult AddUser()
    {
        var model = new AddSecurityUserViewModel();
        PopulateLookups(model);

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireModuleId(3)]
    public IActionResult AddUser(AddSecurityUserViewModel model)
    {
        model.User.SecurityUserId = 0;

        if (string.IsNullOrWhiteSpace(model.User.Password))
        {
            ModelState.AddModelError("User.Password", "La contraseña es requerida.");
        }

        if (!ModelState.IsValid)
        {
            PopulateLookups(model);
            return View(model);
        }

        try
        {
            model.User.Password = _security.Encrypt(model.User.Password!);

            var newUserId = _security.AddOrEditUser(model.User);

            TempData[TempDataSuccessKey] = $"Usuario \"{model.User.UserName}\" creado correctamente (Id {newUserId}).";
            return RedirectToAction("AddUser");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear el usuario {UserName} (Security.AddOrEditUser)", model.User.UserName);
            ViewBag.SubmitError = "No pudimos crear el usuario. Intenta nuevamente en unos minutos.";
            PopulateLookups(model);
            return View(model);
        }
    }

    [HttpGet]
    [AdminOnly]
    public IActionResult AddGroup()
    {
        var model = new SecurityGroupModel();

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AdminOnly]
    public IActionResult AddGroup(SecurityGroupModel model)
    {
        model.SecurityGroupId = 0;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var newGroupId = _security.AddOrEditGroup(model);

            TempData[TempDataSuccessKey] = $"Grupo \"{model.SecurityGroupName}\" creado correctamente (Id {newGroupId}).";
            return RedirectToAction("AddGroup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear el grupo {SecurityGroupName} (Security.AddOrEditGroup)", model.SecurityGroupName);
            ViewBag.SubmitError = "No pudimos crear el grupo. Intenta nuevamente en unos minutos.";
            return View(model);
        }
    }

    [HttpGet]
    [AdminOnly]
    public IActionResult AddModule()
    {
        var model = new SecurityModuleModel();

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AdminOnly]
    public IActionResult AddModule(SecurityModuleModel model)
    {
        model.SecurityModuleId = 0;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var newModuleId = _security.AddOrEditModule(model);

            TempData[TempDataSuccessKey] = $"Módulo \"{model.SecurityModuleName}\" creado correctamente (Id {newModuleId}).";
            return RedirectToAction("AddModule");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear el módulo {SecurityModuleName} (Security.AddOrEditModule)", model.SecurityModuleName);
            ViewBag.SubmitError = "No pudimos crear el módulo. Intenta nuevamente en unos minutos.";
            return View(model);
        }
    }

    [HttpGet]
    [AdminOnly]
    public IActionResult AddGroupModule()
    {
        var model = BuildAddGroupModuleViewModel();

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        if (TempData[TempDataErrorKey] is string errorMessage)
        {
            ViewBag.SubmitError = errorMessage;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AdminOnly]
    public IActionResult AddGroupModule(SecurityGroupModuleModel assignment)
    {
        assignment.SecurityGroupModuleId = 0;

        if (assignment.SecurityGroupId <= 0)
        {
            ModelState.AddModelError("Assignment.SecurityGroupId", "El grupo es requerido.");
        }
        if (assignment.SecurityModuleId <= 0)
        {
            ModelState.AddModelError("Assignment.SecurityModuleId", "El módulo es requerido.");
        }
        if (assignment.SecurityAccessTypeId <= 0)
        {
            ModelState.AddModelError("Assignment.SecurityAccessTypeId", "El tipo de acceso es requerido.");
        }

        if (ModelState.IsValid)
        {
            List<SecurityGroupModuleModel> existingAssignments;
            try
            {
                existingAssignments = _security.GetAllGroupModules();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar módulos ya asignados (Security.GetAllGroupModules)");
                existingAssignments = new List<SecurityGroupModuleModel>();
            }

            var alreadyAssigned = existingAssignments.Any(a =>
                a.SecurityGroupId == assignment.SecurityGroupId &&
                a.SecurityModuleId == assignment.SecurityModuleId);

            if (alreadyAssigned)
            {
                ModelState.AddModelError("Assignment.SecurityModuleId", "Este módulo ya está asignado a este grupo.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(BuildAddGroupModuleViewModel(assignment));
        }

        try
        {
            _security.AddOrEditGroupModules(assignment);

            TempData[TempDataSuccessKey] = "Módulo asignado al grupo correctamente.";
            return RedirectToAction("AddGroupModule");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al asignar el módulo {SecurityModuleId} al grupo {SecurityGroupId} (Security.AddOrEditGroupModules)", assignment.SecurityModuleId, assignment.SecurityGroupId);
            ViewBag.SubmitError = "No pudimos asignar el módulo al grupo. Intenta nuevamente en unos minutos.";
            return View(BuildAddGroupModuleViewModel(assignment));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AdminOnly]
    public IActionResult DeleteGroupModule(int id)
    {
        try
        {
            _security.DeleteGroupModules(id);
            TempData[TempDataSuccessKey] = "Módulo removido del grupo correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar la asignación {SecurityGroupModuleId} (Security.DeleteGroupModules)", id);
            TempData[TempDataErrorKey] = "No pudimos remover el módulo del grupo. Intenta nuevamente en unos minutos.";
        }

        return RedirectToAction("AddGroupModule");
    }

    [HttpGet]
    [RequireModuleId(4)]
    public IActionResult Users()
    {
        List<SecurityUserViewModel> users;
        var loadFailed = false;
        try
        {
            users = _security.GetAllUsers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los usuarios (Security.GetAllUsers)");
            users = new List<SecurityUserViewModel>();
            loadFailed = true;
        }

        // Only Admin (group 1) can see other Admin accounts in this list.
        if (CurrentAgent?.SecurityGroupId != AdminGroupId)
        {
            users = users.Where(u => u.SecurityGroupId != AdminGroupId).ToList();
        }

        ViewBag.LoadFailed = loadFailed;

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        return View(users);
    }

    [HttpGet]
    [AdminOnly]
    public IActionResult Groups()
    {
        List<SecurityGroupModel> groups;
        var loadFailed = false;
        try
        {
            groups = _security.GetAllGroups();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los grupos (Security.GetAllGroups)");
            groups = new List<SecurityGroupModel>();
            loadFailed = true;
        }

        ViewBag.LoadFailed = loadFailed;

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        return View(groups);
    }

    [HttpGet]
    [AdminOnly]
    public IActionResult EditGroup(int id)
    {
        var group = _security.GetAllGroups().FirstOrDefault(g => g.SecurityGroupId == id);
        if (group is null)
        {
            return RedirectToAction("Groups");
        }

        return View(group);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AdminOnly]
    public IActionResult EditGroup(SecurityGroupModel model)
    {
        if (model.SecurityGroupId <= 0)
        {
            return RedirectToAction("Groups");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            _security.AddOrEditGroup(model);

            TempData[TempDataSuccessKey] = $"Grupo \"{model.SecurityGroupName}\" actualizado correctamente.";
            return RedirectToAction("Groups");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar el grupo {SecurityGroupName} (Security.AddOrEditGroup)", model.SecurityGroupName);
            ViewBag.SubmitError = "No pudimos actualizar el grupo. Intenta nuevamente en unos minutos.";
            return View(model);
        }
    }

    [HttpGet]
    [AdminOnly]
    public IActionResult Modules()
    {
        List<SecurityModuleModel> modules;
        var loadFailed = false;
        try
        {
            modules = _security.GetAllModules();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los módulos (Security.GetAllModules)");
            modules = new List<SecurityModuleModel>();
            loadFailed = true;
        }

        ViewBag.LoadFailed = loadFailed;

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        return View(modules);
    }

    [HttpGet]
    [AdminOnly]
    public IActionResult EditModule(int id)
    {
        var module = _security.GetAllModules().FirstOrDefault(m => m.SecurityModuleId == id);
        if (module is null)
        {
            return RedirectToAction("Modules");
        }

        return View(module);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AdminOnly]
    public IActionResult EditModule(SecurityModuleModel model)
    {
        if (model.SecurityModuleId <= 0)
        {
            return RedirectToAction("Modules");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            _security.AddOrEditModule(model);

            TempData[TempDataSuccessKey] = $"Módulo \"{model.SecurityModuleName}\" actualizado correctamente.";
            return RedirectToAction("Modules");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar el módulo {SecurityModuleName} (Security.AddOrEditModule)", model.SecurityModuleName);
            ViewBag.SubmitError = "No pudimos actualizar el módulo. Intenta nuevamente en unos minutos.";
            return View(model);
        }
    }

    [HttpGet]
    [RequireModuleId(4)]
    public IActionResult EditUser(int id)
    {
        var user = _security.GetAllUsers().FirstOrDefault(u => u.SecurityUserId == id);
        if (user is null)
        {
            return RedirectToAction("Users");
        }

        // Non-admins can't edit Admin accounts, even by navigating here directly.
        if (user.SecurityGroupId == AdminGroupId && CurrentAgent?.SecurityGroupId != AdminGroupId)
        {
            return RedirectToAction("Users");
        }

        // The password stays hidden/round-tripped unless a new one is entered.
        user.NewPassword = null;

        var model = new AddSecurityUserViewModel { User = user };
        PopulateLookups(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireModuleId(4)]
    public IActionResult EditUser(AddSecurityUserViewModel model)
    {
        if (model.User.SecurityUserId <= 0)
        {
            return RedirectToAction("Users");
        }

        // Non-admins can't edit Admin accounts. Checked against the existing
        // stored record, not the submitted form value, since that's attacker-controlled.
        var existingUser = _security.GetAllUsers().FirstOrDefault(u => u.SecurityUserId == model.User.SecurityUserId);
        if (existingUser is null || (existingUser.SecurityGroupId == AdminGroupId && CurrentAgent?.SecurityGroupId != AdminGroupId))
        {
            return RedirectToAction("Users");
        }

        if (!ModelState.IsValid)
        {
            PopulateLookups(model);
            return View(model);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(model.User.NewPassword))
            {
                model.User.Password = _security.Encrypt(model.User.NewPassword);
            }
            // else: model.User.Password keeps the existing (hidden, round-tripped) value.

            _security.AddOrEditUser(model.User);

            TempData[TempDataSuccessKey] = $"Usuario \"{model.User.UserName}\" actualizado correctamente.";
            return RedirectToAction("Users");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar el usuario {UserName} (Security.AddOrEditUser)", model.User.UserName);
            ViewBag.SubmitError = "No pudimos actualizar el usuario. Intenta nuevamente en unos minutos.";
            PopulateLookups(model);
            return View(model);
        }
    }
}
