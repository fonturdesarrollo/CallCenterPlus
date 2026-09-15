using System.Globalization;
using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallCenterPlus.Controllers;

public class AgentController : Controller
{
    private const string SessionAgentKey = "CurrentAgent";
    private const string TempDataSuccessKey = "TicketMovementSuccess";
    private const string TempDataErrorKey = "TicketMovementError";

    private readonly IServiceAreas _serviceAreas;
    private readonly ITickets _tickets;
    private readonly ISecurity _security;
    private readonly ILogger<AgentController> _logger;

    public AgentController(IServiceAreas serviceAreas, ITickets tickets, ISecurity security, ILogger<AgentController> logger)
    {
        _serviceAreas = serviceAreas;
        _tickets = tickets;
        _security = security;
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

        ViewBag.CurrentAgent = agent;

        base.OnActionExecuting(context);
    }

    // Real queued tickets, joined with the ServiceArea catalog for display names.
    private List<PendingRequestRow> GetQueuedRequestRows(out bool loadFailed)
    {
        loadFailed = false;

        List<TicketViewModel> queued;
        try
        {
            queued = _tickets.GetByInQueue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los tickets en cola (GetByInQueue)");
            queued = new List<TicketViewModel>();
            loadFailed = true;
        }

        Dictionary<int, ServiceArea> categoryLookup;
        try
        {
            categoryLookup = _serviceAreas.GetAll().ToDictionary(s => s.ServiceAreaDetailId, s => s);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el catálogo de categorías (ServiceAreas.GetAll)");
            categoryLookup = new Dictionary<int, ServiceArea>();
            loadFailed = true;
        }

        return queued
            .Select(t =>
            {
                categoryLookup.TryGetValue(t.ServiceAreaDetailId, out var detail);
                return new PendingRequestRow
                {
                    TicketId = t.TicketId,
                    EmployeeName = t.EmployeeName ?? "—",
                    Department = t.ManagementName ?? "—",
                    RequestTypeName = detail?.ServiceAreaDetailName ?? "No especificado",
                    TicketRemarks = string.IsNullOrWhiteSpace(t.TicketRemarks) ? "—" : t.TicketRemarks,
                    CreatedAt = t.TicketStartDate,
                    Status = "En cola",
                };
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    // TODO: replace the remaining demo data below (KPIs, chart, categories)
    // with real queries once those are ready. The pending-requests list
    // below is already real (Ticket_Detail via ITickets.GetByInQueue()).
    [HttpGet]
    public IActionResult Dashboard()
    {
        List<string> categoryNames;
        try
        {
            categoryNames = _serviceAreas.GetAll()
                .Select(s => s.ServiceAreaName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(3)
                .Select(n => n!)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener las categorías para el dashboard (ServiceAreas.GetAll)");
            categoryNames = new List<string>();
        }

        if (categoryNames.Count == 0)
        {
            categoryNames = new List<string> { "Soporte Técnico", "Redes", "Telefonía" };
        }

        var categoryIcons = new[] { "bi-pc-display", "bi-hdd-network", "bi-telephone" };
        var categoryCounts = new[] { 42, 27, 15 };
        var categoryPercentages = new[] { 47, 30, 17 };

        var model = new AgentDashboardViewModel
        {
            Kpis = new List<AgentKpiCard>
            {
                new() { Label = "Solicitudes pendientes", Value = "18", TrendLabel = "12.4%", TrendUp = true,  ColorClass = "bg-agent-primary", Sparkline = new() { 8, 10, 9, 12, 14, 11, 13, 16, 18 } },
                new() { Label = "En proceso",             Value = "7",  TrendLabel = "4.1%",  TrendUp = false, ColorClass = "bg-agent-info",    Sparkline = new() { 10, 9, 11, 8, 7, 9, 8, 7, 7 } },
                new() { Label = "Resueltas hoy",          Value = "5",  TrendLabel = "25.0%", TrendUp = true,  ColorClass = "bg-agent-warning", Sparkline = new() { 2, 3, 2, 4, 3, 5, 4, 5, 5 } },
                new() { Label = "Tiempo prom. (min)",     Value = "34", TrendLabel = "8.3%",  TrendUp = false, ColorClass = "bg-agent-danger",  Sparkline = new() { 46, 44, 41, 39, 40, 37, 36, 35, 34 } },
            },
            TopCategories = categoryNames.Select((name, i) => new CategoryBreakdown
            {
                Name = name,
                Icon = categoryIcons[i % categoryIcons.Length],
                Count = categoryCounts[i % categoryCounts.Length],
                Percentage = categoryPercentages[i % categoryPercentages.Length],
            }).ToList(),
            PendingRequests = GetQueuedRequestRows(out var loadFailed),
        };

        ViewBag.LoadFailed = loadFailed;
        return View(model);
    }

    [HttpGet]
    public IActionResult Requests()
    {
        var rows = GetQueuedRequestRows(out var loadFailed);
        ViewBag.LoadFailed = loadFailed;
        return View(rows);
    }

    // "Tomar ticket": full ticket data + agent/status assignment + movement
    // history.
    [HttpGet]
    public IActionResult TicketDetail(int id)
    {
        List<TicketViewModel> matches;
        try
        {
            matches = _tickets.GetById(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el ticket {TicketId} (GetById)", id);
            return RedirectToAction("Requests");
        }

        var ticket = matches.FirstOrDefault();
        if (ticket is null)
        {
            return RedirectToAction("Requests");
        }

        var loadFailed = false;
        List<ServiceArea> serviceAreas;
        try
        {
            serviceAreas = _serviceAreas.GetAll();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener las áreas de servicio (ServiceAreas.GetAll) para el ticket {TicketId}", id);
            serviceAreas = new List<ServiceArea>();
            loadFailed = true;
        }

        var serviceAreaDetailName = serviceAreas
            .FirstOrDefault(s => s.ServiceAreaDetailId == ticket.ServiceAreaDetailId)
            ?.ServiceAreaDetailName ?? "No especificado";

        List<SecurityUserViewModel> agents;
        try
        {
            agents = _security.GetAllAgents();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los agentes (Security.GetAllAgents) para el ticket {TicketId}", id);
            agents = new List<SecurityUserViewModel>();
            loadFailed = true;
        }

        List<TicketStatus> statuses;
        try
        {
            statuses = _tickets.GetStatusForAgent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los estatus (Tickets.GetStatusForAgent) para el ticket {TicketId}", id);
            statuses = new List<TicketStatus>();
            loadFailed = true;
        }

        List<TicketDetail> movements;
        try
        {
            movements = _tickets.GetDetailByTicketId(id).OrderByDescending(m => m.TicketMovementDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el historial de movimientos (GetDetailByTicketId) para el ticket {TicketId}", id);
            movements = new List<TicketDetail>();
            loadFailed = true;
        }

        var latestMovement = movements.FirstOrDefault();

        // The ticket row itself only reflects "en cola" vs. attended; once a
        // movement exists, its status is the real, up-to-date one to show.
        if (latestMovement is not null)
        {
            ticket.TicketStatusId = latestMovement.TicketStatusId;
        }

        // Once a movement exists, it already records the real assigned agent;
        // otherwise default to whichever agent is logged in and taking it.
        var currentAgent = HttpContext.Session.GetObject<AgentUser>(SessionAgentKey);
        var defaultAgentId = latestMovement?.SecurityUserId
            ?? currentAgent?.SecurityUserId
            ?? agents.FirstOrDefault()?.SecurityUserId
            ?? 0;

        var model = new TicketReviewViewModel
        {
            Ticket = ticket,
            ServiceAreaDetailName = serviceAreaDetailName,
            AvailableAgents = agents,
            AvailableStatuses = statuses,
            Movements = movements,
            SelectedAgentId = defaultAgentId,
            SelectedStatusId = latestMovement?.TicketStatusId ?? 2,
        };

        ViewBag.LoadFailed = loadFailed;

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
    public IActionResult SaveMovement(TicketMovementInputModel input)
    {
        List<TicketViewModel> matches;
        try
        {
            matches = _tickets.GetById(input.TicketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el ticket {TicketId} antes de guardar el movimiento (GetById)", input.TicketId);
            return RedirectToAction("Requests");
        }

        var ticket = matches.FirstOrDefault();
        if (ticket is null)
        {
            return RedirectToAction("Requests");
        }

        List<SecurityUserViewModel> agents;
        try
        {
            agents = _security.GetAllAgents();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los agentes (Security.GetAllAgents) al guardar el movimiento del ticket {TicketId}", input.TicketId);
            agents = new List<SecurityUserViewModel>();
        }

        List<TicketStatus> statuses;
        try
        {
            statuses = _tickets.GetStatusForAgent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los estatus (Tickets.GetStatusForAgent) al guardar el movimiento del ticket {TicketId}", input.TicketId);
            statuses = new List<TicketStatus>();
        }

        List<TicketDetail> movements;
        try
        {
            movements = _tickets.GetDetailByTicketId(input.TicketId).OrderByDescending(m => m.TicketMovementDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el historial (GetDetailByTicketId) al guardar el movimiento del ticket {TicketId}", input.TicketId);
            movements = new List<TicketDetail>();
        }

        var previousMovement = movements.FirstOrDefault();
        var previousStatusId = previousMovement?.TicketStatusId ?? ticket.TicketStatusId;

        var currentAgent = HttpContext.Session.GetObject<AgentUser>(SessionAgentKey);
        var actorName = FormatDisplayName(currentAgent?.FullName) ?? "Un agente";

        var newAgentName = FormatDisplayName(agents.FirstOrDefault(a => a.SecurityUserId == input.AgentId)?.FullName) ?? "un agente";
        var previousAgentName = FormatDisplayName(previousMovement?.FullName);

        var newStatusName = ResolveStatusName(input.StatusId, statuses);

        var observaciones = (input.StatusId == 3 || input.StatusId == 4) ? input.Observaciones?.Trim() : null;
        var minutos = input.StatusId == 4 ? input.Minutos : null;

        var description = BuildProcessDescription(
            actorName,
            previousMovement?.SecurityUserId,
            previousAgentName,
            input.AgentId,
            newAgentName,
            previousStatusId,
            input.StatusId,
            newStatusName,
            observaciones,
            minutos);

        var detail = new TicketDetail
        {
            TicketDetailId = 0,
            TicketId = input.TicketId,
            SecurityUserId = input.AgentId,
            TicketDetailRemarksByTechnician = observaciones,
            TicketDetailMinutesByTechnician = minutos ?? 0,
            TicketDetailEndDateByTechnician = input.StatusId == 4 ? (input.FechaFinalizacion ?? DateTime.Now) : (DateTime?)null,
            TicketStatusId = input.StatusId,
            TicketDetailProcessDescrption = description,
        };

        try
        {
            _tickets.AddOrEditTicketDetail(detail);
            TempData[TempDataSuccessKey] = "El movimiento del ticket se guardó correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar el movimiento (AddOrEditTicketDetail) del ticket {TicketId}", input.TicketId);
            TempData[TempDataErrorKey] = $"No se pudo guardar el movimiento: {ex.Message}";
        }

        return RedirectToAction("TicketDetail", new { id = input.TicketId });
    }

    // Presentable version of a raw (often ALL CAPS) name/status from the DB.
    private static string? FormatDisplayName(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var culture = CultureInfo.GetCultureInfo("es-ES");
        return culture.TextInfo.ToTitleCase(rawValue.Trim().ToLower(culture));
    }

    private static string ResolveStatusName(int statusId, List<TicketStatus> statuses)
    {
        var match = statuses.FirstOrDefault(s => s.TicketStatusId == statusId)?.TicketStatusName;
        var formatted = FormatDisplayName(match);
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            return formatted;
        }

        return statusId switch
        {
            1 => "En cola",
            5 => "Finalizado",
            _ => "un nuevo estatus",
        };
    }

    // Builds a human-readable, automatic summary of what changed in this
    // movement, stored in TicketDetail.TicketDetailProcessDescrption.
    private static string BuildProcessDescription(
        string actorName,
        int? previousAgentId,
        string? previousAgentName,
        int newAgentId,
        string newAgentName,
        int previousStatusId,
        int newStatusId,
        string newStatusName,
        string? observaciones,
        int? minutos)
    {
        var clauses = new List<string>();

        var agentChanged = previousAgentId is null || previousAgentId != newAgentId;
        if (agentChanged)
        {
            clauses.Add(previousAgentId is null || string.IsNullOrWhiteSpace(previousAgentName)
                ? $"{actorName} asignó el ticket a {newAgentName}"
                : $"{actorName} reasignó el ticket de {previousAgentName} a {newAgentName}");
        }
        else
        {
            clauses.Add($"{actorName} actualizó el ticket asignado a {newAgentName}");
        }

        if (!string.IsNullOrWhiteSpace(observaciones))
        {
            clauses.Add($"registró las siguientes observaciones: \"{observaciones}\"");
        }

        if (newStatusId != previousStatusId)
        {
            clauses.Add($"cambió el estatus del ticket a {newStatusName}");
        }

        if (minutos.HasValue && minutos.Value > 0)
        {
            clauses.Add($"reportó {minutos.Value} minuto{(minutos.Value == 1 ? "" : "s")} invertidos en la tarea");
        }

        if (clauses.Count == 1)
        {
            return clauses[0] + ".";
        }

        return string.Join(", ", clauses.Take(clauses.Count - 1)) + " y " + clauses[^1] + ".";
    }
}
