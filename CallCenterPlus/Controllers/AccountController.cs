using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;

namespace CallCenterPlus.Controllers;

public class AccountController : Controller
{
    private const string SessionEmployeeKey = "CurrentEmployee";

    private readonly IEmployees _employees;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IEmployees employees, ILogger<AccountController> logger)
    {
        _employees = employees;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetObject<Employee>(SessionEmployeeKey) is not null)
        {
            return RedirectToAction("Start", "Request");
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var digitsOnly = new string(model.NationalId.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digitsOnly, out var employeeIdNumber))
        {
            ModelState.AddModelError(nameof(LoginViewModel.NationalId),
                "El número de cédula ingresado no es válido.");
            return View(model);
        }

        Employee? employee;
        try
        {
            employee = _employees.GetByEmployeeIdNumber(employeeIdNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar la cédula {EmployeeIdNumber} (Employees.GetByEmployeeIdNumber)", employeeIdNumber);
            ModelState.AddModelError(string.Empty,
                "Ocurrió un error al validar tu cédula. Intenta nuevamente en unos minutos.");
            return View(model);
        }

        if (employee is null || employee.EmployeeId == 0)
        {
            ModelState.AddModelError(nameof(LoginViewModel.NationalId),
                "No encontramos ningún empleado con esa cédula. Verifica el número e intenta de nuevo.");
            return View(model);
        }

        HttpContext.Session.SetObject(SessionEmployeeKey, employee);

        return RedirectToAction("Start", "Request");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}
