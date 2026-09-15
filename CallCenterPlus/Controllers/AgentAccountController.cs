using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;

namespace CallCenterPlus.Controllers;

public class AgentAccountController : Controller
{
    private const string SessionAgentKey = "CurrentAgent";

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
            FullName = validUser.FullName ?? model.Username.Trim(),
            Role = validUser.SecurityGroupName ?? "Agente de Soporte",
        });

        return RedirectToAction("Requests", "Agent");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove(SessionAgentKey);
        return RedirectToAction("Login");
    }
}
