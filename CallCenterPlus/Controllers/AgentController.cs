using CallCenterPlus.Core;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CallCenterPlus.Controllers;

public class AgentController : Controller
{
    private const string SessionAgentKey = "CurrentAgent";

    private readonly IServiceAreas _serviceAreas;
    private readonly ITickets _tickets;

    public AgentController(IServiceAreas serviceAreas, ITickets tickets)
    {
        _serviceAreas = serviceAreas;
        _tickets = tickets;
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
        catch (Exception)
        {
            queued = new List<TicketViewModel>();
            loadFailed = true;
        }

        Dictionary<int, ServiceArea> categoryLookup;
        try
        {
            categoryLookup = _serviceAreas.GetAll().ToDictionary(s => s.ServiceAreaDetailId, s => s);
        }
        catch (Exception)
        {
            categoryLookup = new Dictionary<int, ServiceArea>();
            loadFailed = true;
        }

        return queued
            .Select(t =>
            {
                categoryLookup.TryGetValue(t.ServiceAreaDetailId, out var detail);
                return new PendingRequestRow
                {
                    EmployeeName = t.EmployeeName ?? "—",
                    Department = t.ManagementName ?? "—",
                    RequestTypeName = detail?.ServiceAreaDetailName ?? "No especificado",
                    CategoryName = detail?.ServiceAreaName ?? "—",
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
        catch (Exception)
        {
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
}
