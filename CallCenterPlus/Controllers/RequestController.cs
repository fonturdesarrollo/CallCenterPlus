using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallCenterPlus.Controllers;

public class RequestController : Controller
{
    private const int NewTicketStatusId = 1; // TicketStatus: 1 = EN COLA

    private const string SessionEmployeeKey = "CurrentEmployee";
    private const string SessionPendingRequestKey = "PendingRequest";

    private readonly IServiceAreas _serviceAreas;
    private readonly ITickets _tickets;
    private readonly ILogger<RequestController> _logger;

    public RequestController(IServiceAreas serviceAreas, ITickets tickets, ILogger<RequestController> logger)
    {
        _serviceAreas = serviceAreas;
        _tickets = tickets;
        _logger = logger;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener las áreas de servicio (ServiceAreas.GetAll)");
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

    private static string ResolveStatusName(int statusId) => statusId switch
    {
        1 => "En cola",
        2 => "Técnico asignado",
        3 => "Técnico trabajando",
        4 => "Finalizado técnico",
        5 => "Finalizado",
        _ => "Desconocido",
    };

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
    public IActionResult SubmitRequest(int? extensionOrCellPhone)
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

        // Empty submission maps to 0 (no extension/cell phone provided).
        var extension = extensionOrCellPhone ?? 0;

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
            EmployeePhone = extension,
        };

        int newTicketId;
        try
        {
            newTicketId = _tickets.AddOrEdit(ticketModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar la solicitud (Tickets.AddOrEdit) del empleado {EmployeeId}", employee.EmployeeId);
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
        var employeeId = CurrentEmployee.EmployeeId;

        List<TicketViewModel> tickets;
        var loadFailed = false;
        try
        {
            tickets = _tickets.GetByEmployeeId(employeeId).OrderByDescending(t => t.TicketStartDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener las solicitudes del empleado {EmployeeId} (Tickets.GetByEmployeeId)", employeeId);
            tickets = new List<TicketViewModel>();
            loadFailed = true;
        }

        var categoryLookup = LoadServiceAreas(out var serviceAreasLoadFailed).ToDictionary(s => s.ServiceAreaDetailId, s => s);
        loadFailed = loadFailed || serviceAreasLoadFailed;

        var rows = new List<MyRequestRow>();
        foreach (var t in tickets)
        {
            categoryLookup.TryGetValue(t.ServiceAreaDetailId, out var detail);

            // The Ticket row itself only reflects "en cola" vs. attended; once
            // a movement exists, its status is the real, up-to-date one.
            var effectiveStatusId = t.TicketStatusId;
            try
            {
                var lastMovement = _tickets.GetDetailByTicketId(t.TicketId)
                    .OrderByDescending(m => m.TicketMovementDate)
                    .FirstOrDefault();
                if (lastMovement is not null)
                {
                    effectiveStatusId = lastMovement.TicketStatusId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el último movimiento del ticket {TicketId} (GetDetailByTicketId)", t.TicketId);
            }

            rows.Add(new MyRequestRow
            {
                TicketId = t.TicketId,
                RequestTypeName = detail?.ServiceAreaDetailName ?? "No especificado",
                RequestTypeIcon = detail is not null && IsOther(detail) ? "bi-three-dots" : "bi-card-list",
                TicketRemarks = string.IsNullOrWhiteSpace(t.TicketRemarks) ? "—" : t.TicketRemarks,
                CreatedAt = t.TicketStartDate,
                TicketStatusId = effectiveStatusId,
                StatusName = ResolveStatusName(effectiveStatusId),
            });
        }

        ViewBag.LoadFailed = loadFailed;
        return View(rows);
    }

    // Full movement history of one of the employee's own requests.
    [HttpGet]
    public IActionResult TicketDetail(int id)
    {
        List<TicketViewModel> myTickets;
        try
        {
            // Scoped to the current employee's own tickets, so an id that
            // isn't theirs simply won't be found below.
            myTickets = _tickets.GetByEmployeeId(CurrentEmployee.EmployeeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la solicitud {TicketId} del empleado {EmployeeId} (Tickets.GetByEmployeeId)", id, CurrentEmployee.EmployeeId);
            return RedirectToAction("CheckStatus");
        }

        var ticket = myTickets.FirstOrDefault(t => t.TicketId == id);
        if (ticket is null)
        {
            return RedirectToAction("CheckStatus");
        }

        var detail = LoadServiceAreas(out _).FirstOrDefault(s => s.ServiceAreaDetailId == ticket.ServiceAreaDetailId);

        List<TicketDetail> movements;
        try
        {
            movements = _tickets.GetDetailByTicketId(id).OrderBy(m => m.TicketMovementDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el historial (GetDetailByTicketId) de la solicitud {TicketId}", id);
            movements = new List<TicketDetail>();
        }

        var lastMovement = movements.LastOrDefault();

        // The Ticket row itself only reflects "en cola" vs. attended; once a
        // movement exists, its status is the real, up-to-date one to show.
        var effectiveStatusId = lastMovement?.TicketStatusId ?? ticket.TicketStatusId;

        var model = new MyRequestDetailViewModel
        {
            TicketId = ticket.TicketId,
            RequestTypeName = detail?.ServiceAreaDetailName ?? "No especificado",
            RequestTypeIcon = detail is not null && IsOther(detail) ? "bi-three-dots" : "bi-card-list",
            Description = ticket.TicketRemarks,
            TicketStatusId = effectiveStatusId,
            StatusName = ResolveStatusName(effectiveStatusId),
            CreatedAt = ticket.TicketStartDate,
            UpdatedAt = lastMovement?.TicketMovementDate ?? ticket.TicketStartDate,
            AssignedTechnician = lastMovement?.FullName,
            MinutesSpent = lastMovement?.TicketDetailMinutesByTechnician > 0 ? lastMovement.TicketDetailMinutesByTechnician : null,
            Movements = movements,
        };

        return View(model);
    }
}
