using CallCenterPlus.Authorization;
using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallCenterPlus.Controllers;

// SecurityModule id 5 = "Reportes". Group 1 (Admin) always bypasses this via
// ModuleAccessControl, same rule as everywhere else.
[RequireModuleId(5)]
public class ReportsController : Controller
{
    private const string SessionAgentKey = "CurrentAgent";
    private const int EndedByTechnicianStatusId = 4; // "Finalizado técnico" — the movement that carries Minutos/Fecha de finalización.
    private const int ReopenedStatusId = 1; // "En cola" — agents can never set this via SaveMovement (GetStatusForAgent/Supervisor both start at 2), so every status-1 movement is a reopen event.

    private readonly IServiceAreas _serviceAreas;
    private readonly ITickets _tickets;
    private readonly ISecurity _security;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IServiceAreas serviceAreas, ITickets tickets, ISecurity security, ILogger<ReportsController> logger)
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

        if (!ModuleAccessControl.HasAccess(context, agent, _security))
        {
            context.Result = RedirectToAction("AccessDenied", "Agent");
            return;
        }

        ViewBag.CurrentAgent = agent;

        base.OnActionExecuting(context);
    }

    [HttpGet]
    public IActionResult Volume()
    {
        var loadFailed = false;

        // "En cola" (status 1) is excluded on purpose — it's just the ticket's
        // initial status before any agent has touched it, not real volume yet.
        List<TicketViewModel> tickets;
        try
        {
            tickets = _tickets.GetByWithAgent()
                .Concat(_tickets.GetByEnded())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los tickets para el reporte de volumen");
            tickets = new List<TicketViewModel>();
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

        var total = tickets.Count;

        var byCategory = tickets
            .GroupBy(t =>
            {
                categoryLookup.TryGetValue(t.ServiceAreaDetailId, out var detail);
                return detail?.ServiceAreaDetailName ?? "No especificado";
            })
            .Select(g => new CategoryVolume
            {
                Name = g.Key,
                Count = g.Count(),
                Percentage = total == 0 ? 0 : (int)Math.Round(g.Count() * 100.0 / total),
            })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .ToList();

        var countsByStatus = tickets
            .GroupBy(t => t.TicketStatusId)
            .ToDictionary(g => g.Key, g => g.Count());

        var byStatus = new[] { 2, 3, 4, 5 }
            .Select(statusId =>
            {
                countsByStatus.TryGetValue(statusId, out var count);
                return new StatusVolume
                {
                    StatusId = statusId,
                    Name = ResolveStatusLabel(statusId),
                    Count = count,
                    Percentage = total == 0 ? 0 : (int)Math.Round(count * 100.0 / total),
                };
            })
            .ToList();

        var byDepartment = tickets
            .GroupBy(t => string.IsNullOrWhiteSpace(t.ManagementName) ? "No especificado" : t.ManagementName!)
            .Select(g => new CategoryVolume
            {
                Name = g.Key,
                Count = g.Count(),
                Percentage = total == 0 ? 0 : (int)Math.Round(g.Count() * 100.0 / total),
            })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .ToList();

        var model = new TicketVolumeReportViewModel
        {
            TotalTickets = total,
            ByCategory = byCategory,
            ByStatus = byStatus,
            ByDepartment = byDepartment,
        };

        ViewBag.LoadFailed = loadFailed;
        return View(model);
    }

    // Uses the "Minutos invertidos" the technician enters when marking a
    // ticket "Finalizado técnico" — the only real duration the system
    // captures per movement (see Views/Agent/TicketDetail.cshtml).
    [HttpGet]
    public IActionResult ResolutionTime()
    {
        var loadFailed = false;

        List<TicketDetail> details;
        try
        {
            details = _tickets.GetAllTicketDetails();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los movimientos de los tickets para el reporte de tiempos de resolución");
            details = new List<TicketDetail>();
            loadFailed = true;
        }

        var resolved = details
            .Where(d => d.TicketStatusId == EndedByTechnicianStatusId
                && d.TicketDetailMinutesByTechnician > 0
                && !string.IsNullOrWhiteSpace(d.FullName))
            .ToList();

        var byAgent = resolved
            .GroupBy(d => d.FullName!.Trim())
            .Select(g => new AgentResolutionTime
            {
                AgentName = g.Key,
                ResolvedCount = g.Count(),
                AverageMinutes = (int)Math.Round(g.Average(d => d.TicketDetailMinutesByTechnician)),
                MinMinutes = g.Min(d => d.TicketDetailMinutesByTechnician),
                MaxMinutes = g.Max(d => d.TicketDetailMinutesByTechnician),
            })
            .OrderBy(a => a.AverageMinutes)
            .ThenBy(a => a.AgentName)
            .ToList();

        var model = new AgentResolutionTimeViewModel
        {
            TotalResolved = resolved.Count,
            OverallAverageMinutes = resolved.Count == 0 ? 0 : resolved.Average(d => d.TicketDetailMinutesByTechnician),
            ByAgent = byAgent,
        };

        ViewBag.LoadFailed = loadFailed;
        return View(model);
    }

    // A reopen event is any TicketDetail movement with TicketStatusId == 1 —
    // that status is only ever set by Request/ReopenTicket (see ReopenedStatusId).
    [HttpGet]
    public IActionResult Reopened()
    {
        var loadFailed = false;

        List<TicketDetail> details;
        try
        {
            details = _tickets.GetAllTicketDetails();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los movimientos de los tickets para el reporte de reaperturas");
            details = new List<TicketDetail>();
            loadFailed = true;
        }

        int totalTickets;
        try
        {
            totalTickets = _tickets.GetByInQueue().Count
                + _tickets.GetByWithAgent().Count
                + _tickets.GetByEnded().Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los tickets para el reporte de reaperturas");
            totalTickets = 0;
            loadFailed = true;
        }

        var reopenEvents = details
            .Where(d => d.TicketStatusId == ReopenedStatusId)
            .OrderByDescending(d => d.TicketMovementDate)
            .ToList();

        var byCategory = reopenEvents
            .GroupBy(d => string.IsNullOrWhiteSpace(d.ServiceAreaDetailName) ? "No especificado" : d.ServiceAreaDetailName!)
            .Select(g => new CategoryVolume
            {
                Name = g.Key,
                Count = g.Count(),
                Percentage = reopenEvents.Count == 0 ? 0 : (int)Math.Round(g.Count() * 100.0 / reopenEvents.Count),
            })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .ToList();

        var rows = reopenEvents
            .Select(d => new ReopenedTicketRow
            {
                TicketId = d.TicketId,
                EmployeeName = d.EmployeeName ?? "—",
                Department = d.ManagementName ?? "—",
                Category = d.ServiceAreaDetailName ?? "No especificado",
                ReopenedAt = d.TicketMovementDate,
                PreviousAgentName = string.IsNullOrWhiteSpace(d.FullName) ? "—" : d.FullName!,
                Observation = string.IsNullOrWhiteSpace(d.TicketDetailRemarksByTechnician) ? "—" : d.TicketDetailRemarksByTechnician!,
            })
            .ToList();

        var totalReopenedTickets = reopenEvents.Select(d => d.TicketId).Distinct().Count();

        var model = new ReopenedTicketsReportViewModel
        {
            TotalTickets = totalTickets,
            TotalReopenedTickets = totalReopenedTickets,
            TotalReopenEvents = reopenEvents.Count,
            ReopenRatePercentage = totalTickets == 0 ? 0 : (int)Math.Round(totalReopenedTickets * 100.0 / totalTickets),
            ByCategory = byCategory,
            Rows = rows,
        };

        ViewBag.LoadFailed = loadFailed;
        return View(model);
    }

    // How long each still-unattended ticket has been sitting in "En cola".
    [HttpGet]
    public IActionResult QueueAging()
    {
        var loadFailed = false;

        List<TicketViewModel> queued;
        try
        {
            queued = _tickets.GetByInQueue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los tickets en cola para el reporte de antigüedad");
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

        var now = DateTime.Now;

        var rows = queued
            .Select(t =>
            {
                categoryLookup.TryGetValue(t.ServiceAreaDetailId, out var detail);
                var waiting = now - t.TicketStartDate;
                return new QueuedTicketRow
                {
                    TicketId = t.TicketId,
                    EmployeeName = t.EmployeeName ?? "—",
                    Department = t.ManagementName ?? "—",
                    Category = detail?.ServiceAreaDetailName ?? "No especificado",
                    CreatedAt = t.TicketStartDate,
                    WaitingHours = waiting.TotalHours,
                    WaitingLabel = FormatWaiting(waiting),
                    Severity = ResolveSeverity(waiting.TotalHours),
                };
            })
            .OrderByDescending(r => r.WaitingHours)
            .ToList();

        var buckets = new List<AgingBucket>
        {
            new() { Label = "Menos de 24 horas", Severity = "success", Count = rows.Count(r => r.WaitingHours < 24) },
            new() { Label = "1 a 3 días", Severity = "warning", Count = rows.Count(r => r.WaitingHours >= 24 && r.WaitingHours < 72) },
            new() { Label = "Más de 3 días", Severity = "danger", Count = rows.Count(r => r.WaitingHours >= 72) },
        };

        var model = new QueueAgingReportViewModel
        {
            TotalInQueue = rows.Count,
            AverageWaitingLabel = rows.Count == 0 ? "—" : FormatWaiting(TimeSpan.FromHours(rows.Average(r => r.WaitingHours))),
            LongestWaiting = rows.FirstOrDefault(),
            Buckets = buckets,
            Rows = rows,
        };

        ViewBag.LoadFailed = loadFailed;
        return View(model);
    }

    private static string ResolveSeverity(double waitingHours) => waitingHours switch
    {
        < 24 => "success",
        < 72 => "warning",
        _ => "danger",
    };

    private static string FormatWaiting(TimeSpan waiting)
    {
        if (waiting.TotalDays >= 1)
        {
            return $"{(int)waiting.TotalDays}d {waiting.Hours}h";
        }

        if (waiting.TotalHours >= 1)
        {
            return $"{(int)waiting.TotalHours}h";
        }

        return $"{Math.Max(0, (int)waiting.TotalMinutes)} min";
    }

    private static string ResolveStatusLabel(int statusId) => statusId switch
    {
        1 => "En cola",
        2 => "Técnico asignado",
        3 => "Técnico trabajando",
        4 => "Finalizado técnico",
        5 => "Finalizado",
        _ => "Desconocido",
    };
}
