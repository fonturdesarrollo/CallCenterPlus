using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using CallCenterPlus.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallCenterPlus.Controllers;

public class RequestController : Controller
{
    private const int NewTicketStatusId = 1; // TicketStatus: 1 = EN COLA

    private const string SessionEmployeeKey = "CurrentEmployee";
    private const string SessionPendingRequestKey = "PendingRequest";
    private const string SessionMyTicketsKey = "MyNewTickets";

    private readonly IFakeDataService _dataService;
    private readonly IServiceAreas _serviceAreas;
    private readonly ITickets _tickets;

    public RequestController(IFakeDataService dataService, IServiceAreas serviceAreas, ITickets tickets)
    {
        _dataService = dataService;
        _serviceAreas = serviceAreas;
        _tickets = tickets;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var employee = HttpContext.Session.GetObject<Employee>(SessionEmployeeKey);
        if (employee is null)
        {
            context.Result = RedirectToAction("Login", "Account");
            return;
        }

        ViewBag.CurrentEmployee = employee;

        base.OnActionExecuting(context);
    }

    private Employee CurrentEmployee => (Employee)ViewBag.CurrentEmployee;

    private List<ServiceArea> LoadServiceAreas(out bool loadFailed)
    {
        try
        {
            loadFailed = false;
            return _serviceAreas.GetAll();
        }
        catch (Exception)
        {
            loadFailed = true;
            return new List<ServiceArea>();
        }
    }

    private ServiceArea? FindServiceAreaDetail(string requestTypeId) =>
        LoadServiceAreas(out _).FirstOrDefault(s => s.ServiceAreaDetailId.ToString() == requestTypeId);

    private static bool IsOther(ServiceArea detail) =>
        string.Equals(detail.ServiceAreaDetailName?.Trim(), "Otro", StringComparison.OrdinalIgnoreCase);

    private (string Name, string Icon) ResolveRequestType(string requestTypeId)
    {
        var detail = FindServiceAreaDetail(requestTypeId);
        if (detail is null)
        {
            return ("No especificado", "bi-question-circle");
        }

        return (detail.ServiceAreaDetailName ?? "No especificado", IsOther(detail) ? "bi-three-dots" : "bi-card-list");
    }

    // Screen 1: choose between creating a request or checking a request's status.
    [HttpGet]
    public IActionResult Start()
    {
        HttpContext.Session.Remove(SessionPendingRequestKey);
        return View();
    }

    // Screen 2: request type + description.
    [HttpGet]
    public IActionResult NewRequest()
    {
        var pending = HttpContext.Session.GetObject<RequestSessionData>(SessionPendingRequestKey);

        var model = new NewRequestViewModel
        {
            AvailableServiceAreas = LoadServiceAreas(out var loadFailed),
            ServiceAreasLoadFailed = loadFailed,
            RequestTypeId = pending?.RequestTypeId ?? string.Empty,
            Description = pending?.Description ?? string.Empty,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult NewRequest(NewRequestViewModel model)
    {
        var description = model.Description?.Trim() ?? string.Empty;
        var selectedDetail = FindServiceAreaDetail(model.RequestTypeId);
        var isOther = selectedDetail is not null && IsOther(selectedDetail);
        if (isOther && description.Length < 10)
        {
            ModelState.AddModelError(nameof(NewRequestViewModel.Description),
                "Cuéntanos con al menos 10 caracteres qué necesitas.");
        }

        if (!ModelState.IsValid)
        {
            model.AvailableServiceAreas = LoadServiceAreas(out var loadFailed);
            model.ServiceAreasLoadFailed = loadFailed;
            return View(model);
        }

        HttpContext.Session.SetObject(SessionPendingRequestKey, new RequestSessionData
        {
            RequestTypeId = model.RequestTypeId,
            Description = description,
        });

        return RedirectToAction("Summary");
    }

    // Screen 3: request summary, "Send request" button.
    [HttpGet]
    public IActionResult Summary()
    {
        var pending = HttpContext.Session.GetObject<RequestSessionData>(SessionPendingRequestKey);
        if (pending is null || string.IsNullOrEmpty(pending.RequestTypeId))
        {
            return RedirectToAction("NewRequest");
        }

        var (requestTypeName, requestTypeIcon) = ResolveRequestType(pending.RequestTypeId);

        var model = new RequestSummaryViewModel
        {
            Requester = CurrentEmployee,
            RequestTypeName = requestTypeName,
            RequestTypeIcon = requestTypeIcon,
            Description = pending.Description,
            Date = DateTime.Now,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SubmitRequest()
    {
        var pending = HttpContext.Session.GetObject<RequestSessionData>(SessionPendingRequestKey);
        if (pending is null || string.IsNullOrEmpty(pending.RequestTypeId))
        {
            return RedirectToAction("NewRequest");
        }

        var detail = FindServiceAreaDetail(pending.RequestTypeId);
        if (detail is null)
        {
            return RedirectToAction("NewRequest");
        }

        var employee = CurrentEmployee;

        var ticketModel = new TicketViewModel
        {
            TicketId = 0,
            ServiceAreaDetailId = detail.ServiceAreaDetailId,
            TicketRemarks = string.IsNullOrWhiteSpace(pending.Description) ? null : pending.Description,
            EmployeeId = employee.EmployeeId,
            ManagementName = employee.ManagementName,
            ManagementDivisionName = employee.ManagementDivisionName,
            TicketStatusId = NewTicketStatusId,
            TicketEndDate = null,
        };

        int newTicketId;
        try
        {
            newTicketId = _tickets.AddOrEdit(ticketModel);
        }
        catch (Exception)
        {
            var model = new RequestSummaryViewModel
            {
                Requester = employee,
                RequestTypeName = detail.ServiceAreaDetailName ?? "No especificado",
                RequestTypeIcon = IsOther(detail) ? "bi-three-dots" : "bi-card-list",
                Description = pending.Description,
                Date = DateTime.Now,
            };
            ViewBag.SubmitError = "No pudimos enviar tu solicitud. Intenta nuevamente en unos minutos.";
            return View("Summary", model);
        }

        var ticketNumber = $"TCK-{DateTime.Now:yyyy}-{newTicketId:000000}";

        // Keep a lightweight session record so "Consultar estatus" (still demo data) can show it.
        var newFakeTicket = new FakeTicket
        {
            Number = ticketNumber,
            EmployeeIdNumber = employee.EmployeeIdNumber,
            RequestTypeName = detail.ServiceAreaDetailName ?? "No especificado",
            RequestTypeIcon = IsOther(detail) ? "bi-three-dots" : "bi-card-list",
            Description = string.IsNullOrWhiteSpace(pending.Description) ? "Sin detalles adicionales." : pending.Description,
            Status = "Abierto",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            AssignedTechnician = null,
            MinutesSpent = null,
            Comments = new(),
        };

        var myTickets = HttpContext.Session.GetObject<List<FakeTicket>>(SessionMyTicketsKey) ?? new List<FakeTicket>();
        myTickets.Add(newFakeTicket);
        HttpContext.Session.SetObject(SessionMyTicketsKey, myTickets);

        HttpContext.Session.Remove(SessionPendingRequestKey);

        return RedirectToAction("Confirmation", new { number = ticketNumber });
    }

    // Final confirmation screen after sending the request.
    [HttpGet]
    public IActionResult Confirmation(string number)
    {
        ViewBag.TicketNumber = number;
        return View();
    }

    // Alternate flow: check the status of previously created requests.
    [HttpGet]
    public IActionResult CheckStatus()
    {
        var employeeIdNumber = CurrentEmployee.EmployeeIdNumber;
        var myTickets = HttpContext.Session.GetObject<List<FakeTicket>>(SessionMyTicketsKey) ?? new List<FakeTicket>();

        var tickets = myTickets
            .Concat(_dataService.GetTicketsByEmployeeIdNumber(employeeIdNumber))
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        return View(tickets);
    }

    [HttpGet]
    public IActionResult TicketDetail(string number)
    {
        var myTickets = HttpContext.Session.GetObject<List<FakeTicket>>(SessionMyTicketsKey) ?? new List<FakeTicket>();
        var ticket = myTickets.FirstOrDefault(t => t.Number.Equals(number, StringComparison.OrdinalIgnoreCase))
            ?? _dataService.GetTicketByNumber(number);

        if (ticket is null || ticket.EmployeeIdNumber != CurrentEmployee.EmployeeIdNumber)
        {
            return RedirectToAction("CheckStatus");
        }

        return View(ticket);
    }
}
