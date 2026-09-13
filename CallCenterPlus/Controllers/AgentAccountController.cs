using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;

namespace CallCenterPlus.Controllers;

public class AgentAccountController : Controller
{
    private const string SessionAgentKey = "CurrentAgent";

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

    // TODO: replace with real authentication (SecurityUser) once security is implemented.
    // For now any non-empty username/password combination is accepted.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(AgentLoginViewModel model)
    {
        ViewData["AgentAuthPage"] = true;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        HttpContext.Session.SetObject(SessionAgentKey, new AgentUser
        {
            FullName = model.Username.Trim(),
            Role = "Agente de Soporte",
        });

        return RedirectToAction("Requests", "Agent");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Remove(SessionAgentKey);
        return RedirectToAction("Login");
    }
}
