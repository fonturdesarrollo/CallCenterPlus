using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;

namespace CallCenterPlus.Controllers;

public class AgentAccountController : Controller
{
    private const string SessionAgentKey = "CurrentAgent";
    private const string TempDataSuccessKey = "ChangePasswordSuccess";

    private readonly ISecurity _security;
    private readonly ILogger<AgentAccountController> _logger;

    public AgentAccountController(ISecurity security, ILogger<AgentAccountController> logger)
    {
        _security = security;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetObject<AgentUser>(SessionAgentKey) is not null)
        {
            return RedirectToAction("Requests", "Agent");
        }

        ViewData["AgentAuthPage"] = true;
        return View(new AgentLoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(AgentLoginViewModel model)
    {
        ViewData["AgentAuthPage"] = true;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        SecurityUserViewModel validUser;
        try
        {
            validUser = _security.GetValidUser(model.Username.Trim(), model.Password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar el usuario {UserName} (Security.GetValidUser)", model.Username);
            ModelState.AddModelError(string.Empty, "Ocurrió un error al iniciar sesión. Intenta nuevamente en unos minutos.");
            return View(model);
        }

        if (validUser.SecurityUserId <= 0)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
            return View(model);
        }

        HttpContext.Session.SetObject(SessionAgentKey, new AgentUser
        {
            SecurityUserId = validUser.SecurityUserId,
            SecurityGroupId = validUser.SecurityGroupId,
            UserName = validUser.UserName ?? model.Username.Trim(),
            FullName = validUser.FullName ?? model.Username.Trim(),
            Role = validUser.SecurityGroupName ?? "Agente de Soporte",
            StateName = validUser.StateName ?? string.Empty,
        });

        return RedirectToAction("Requests", "Agent");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove(SessionAgentKey);
        return RedirectToAction("Login");
    }

    // Available to every agent regardless of group — not gated by
    // ModuleAccessControl like the Security/Agent controllers are.
    [HttpGet]
    public IActionResult ChangePassword()
    {
        var agent = HttpContext.Session.GetObject<AgentUser>(SessionAgentKey);
        if (agent is null)
        {
            return RedirectToAction("Login");
        }

        ViewBag.CurrentAgent = agent;

        if (TempData[TempDataSuccessKey] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(ChangePasswordViewModel model)
    {
        var agent = HttpContext.Session.GetObject<AgentUser>(SessionAgentKey);
        if (agent is null)
        {
            return RedirectToAction("Login");
        }

        ViewBag.CurrentAgent = agent;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!string.Equals(model.NewPassword, model.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Las contraseñas no coinciden.");
            return View(model);
        }

        SecurityUserViewModel validUser;
        try
        {
            validUser = _security.GetValidUser(agent.UserName, model.CurrentPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar la contraseña actual del usuario {UserName} (Security.GetValidUser)", agent.UserName);
            ModelState.AddModelError(string.Empty, "Ocurrió un error al cambiar tu contraseña. Intenta nuevamente en unos minutos.");
            return View(model);
        }

        if (validUser.SecurityUserId <= 0)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "La contraseña actual no es correcta.");
            return View(model);
        }

        try
        {
            validUser.Password = _security.Encrypt(model.NewPassword);
            _security.AddOrEditUser(validUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar la contraseña del usuario {UserName} (Security.AddOrEditUser)", agent.UserName);
            ModelState.AddModelError(string.Empty, "No pudimos actualizar tu contraseña. Intenta nuevamente en unos minutos.");
            return View(model);
        }

        TempData[TempDataSuccessKey] = "Tu contraseña se actualizó correctamente.";
        return RedirectToAction("ChangePassword");
    }
}
