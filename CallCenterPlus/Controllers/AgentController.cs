using System.Globalization;
using CallCenterPlus.Authorization;
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
    private const int AdminGroupId = 1;
    private const int AllStatusesModuleId = 2; // SecurityModule "Todos los estatus"

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

        if (!ModuleAccessControl.HasAccess(context, agent, _security))
        {
            context.Result = RedirectToAction("AccessDenied");
            return;
        }

        ViewBag.CurrentAgent = agent;

        base.OnActionExecuting(context);
    }

    // Shared by the "En cola" / "En atención" / "Finalizados" tabs on
    // Requests — same mapping, different source query.
    private List<PendingRequestRow> GetRequestRows(Func<List<TicketViewModel>> fetch, string fetchName, bool includeTechnician, out bool loadFailed)
    {
        loadFailed = false;

        List<TicketViewModel> tickets;
        try
        {
            tickets = fetch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los tickets ({FetchName})", fetchName);
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

        // "Técnico" (en atención / finalizados only) = the FullName on each
        // ticket's latest movement — Ticket_Detail has no agent column of its
        // own, only TicketDetail_Detail does (via GetAllTicketDetails).
        Dictionary<int, string> technicianByTicketId = new();
        if (includeTechnician)
        {
            try
            {
                technicianByTicketId = _tickets.GetAllTicketDetails()
                    .Where(d => !string.IsNullOrWhiteSpace(d.FullName))
                    .GroupBy(d => d.TicketId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(d => d.TicketMovementDate).First().FullName!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener los movimientos para resolver el técnico (Tickets.GetAllTicketDetails)");
                loadFailed = true;
            }
        }

        return tickets
            .Select(t =>
            {
                categoryLookup.TryGetValue(t.ServiceAreaDetailId, out var detail);
                technicianByTicketId.TryGetValue(t.TicketId, out var technician);
                return new PendingRequestRow
                {
                    TicketId = t.TicketId,
                    EmployeeName = t.EmployeeName ?? "—",
                    Department = t.ManagementName ?? "—",
                    RequestTypeName = detail?.ServiceAreaDetailName ?? "No especificado",
                    TicketRemarks = string.IsNullOrWhiteSpace(t.TicketRemarks) ? "—" : t.TicketRemarks,
                    CreatedAt = t.TicketStartDate,
                    Status = ResolveStatusLabel(t.TicketStatusId),
                    Technician = string.IsNullOrWhiteSpace(technician) ? "—" : technician,
                };
            })
            .OrderBy(r => r.TicketId)
            .ToList();
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

    // Group 1 (Admin) always has full access, same rule as ModuleAccessControl.
    // Everyone else needs their group tied to the "Todos los estatus" module.
    private bool HasFullStatusAccess(AgentUser? agent)
    {
        if (agent is null)
        {
            return false;
        }

        if (agent.SecurityGroupId == AdminGroupId)
        {
            return true;
        }

        try
        {
            return _security.GroupHasAccessToModule(agent.SecurityGroupId, AllStatusesModuleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar el acceso a todos los estatus (Security.GroupHasAccessToModule) para el grupo {SecurityGroupId}", agent.SecurityGroupId);
            return false;
        }
    }

    [HttpGet]
    [RequireModule("Solicitudes")]
    public IActionResult Requests(string view = "queue")
    {
        var normalizedView = view?.Trim().ToLowerInvariant() switch
        {
            "attention" => "attention",
            "ended" => "ended",
            _ => "queue",
        };

        List<PendingRequestRow> rows;
        bool loadFailed;
        switch (normalizedView)
        {
            case "attention":
                rows = GetRequestRows(_tickets.GetByWithAgent, "GetByWithAgent", includeTechnician: true, out loadFailed);
                break;
            case "ended":
                rows = GetRequestRows(_tickets.GetByEnded, "GetByEnded", includeTechnician: true, out loadFailed);
                break;
            default:
                rows = GetRequestRows(_tickets.GetByInQueue, "GetByInQueue", includeTechnician: false, out loadFailed);
                break;
        }

        ViewBag.LoadFailed = loadFailed;
        ViewBag.CurrentView = normalizedView;
        return View(rows);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // "Tomar ticket": full ticket data + agent/status assignment + movement
    // history.
    [HttpGet]
    [RequireModule("Solicitudes")]
    public IActionResult TicketDetail(int id, string view = "queue")
    {
        ViewBag.ReturnView = view;

        List<TicketViewModel> matches;
        try
        {
            matches = _tickets.GetById(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el ticket {TicketId} (GetById)", id);
            return RedirectToAction("Requests", new { view });
        }

        var ticket = matches.FirstOrDefault();
        if (ticket is null)
        {
            return RedirectToAction("Requests", new { view });
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
            agents = _security.GetAgentsForTickets();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los agentes (Security.GetAgentsForTickets) para el ticket {TicketId}", id);
            agents = new List<SecurityUserViewModel>();
            loadFailed = true;
        }

        // TicketDetail_Detail (behind GetDetailByTicketId) doesn't expose the
        // technician's SecurityUserId, only their FullName — so movements
        // read via that view always come back with SecurityUserId 0. Resolve
        // the real id by name against the full user list instead.
        List<SecurityUserViewModel> allUsers;
        try
        {
            allUsers = _security.GetAllUsers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los usuarios (Security.GetAllUsers) para el ticket {TicketId}", id);
            allUsers = new List<SecurityUserViewModel>();
            loadFailed = true;
        }

        var currentAgent = HttpContext.Session.GetObject<AgentUser>(SessionAgentKey);
        var hasFullStatusAccess = HasFullStatusAccess(currentAgent);

        List<TicketStatus> statuses;
        try
        {
            statuses = hasFullStatusAccess ? _tickets.GetStatusForSupervisor() : _tickets.GetStatusForAgent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los estatus ({Fetch}) para el ticket {TicketId}", hasFullStatusAccess ? "GetStatusForSupervisor" : "GetStatusForAgent", id);
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

        // The combo only comes from "Agentes" (GetAgentsForTickets), so an
        // already-assigned agent or the logged-in agent might not literally
        // be a member of that group — make sure both are still real,
        // selectable options so the combo can actually show them selected.
        void EnsureAgentOption(int securityUserId, string? fullName)
        {
            if (securityUserId > 0 && !agents.Any(a => a.SecurityUserId == securityUserId))
            {
                agents.Add(new SecurityUserViewModel
                {
                    SecurityUserId = securityUserId,
                    FullName = fullName,
                });
            }
        }

        var latestMovementAgentId = latestMovement is null ? 0 : ResolveAgentIdByName(latestMovement.FullName, allUsers);
        if (latestMovementAgentId > 0)
        {
            EnsureAgentOption(latestMovementAgentId, latestMovement!.FullName);
        }
        if (currentAgent is not null)
        {
            EnsureAgentOption(currentAgent.SecurityUserId, currentAgent.FullName);
        }

        // Once a movement exists, it already records the real assigned agent;
        // otherwise default to whichever agent is logged in and taking it.
        var defaultAgentId = (latestMovementAgentId > 0 ? latestMovementAgentId : (int?)null)
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
    [RequireModule("Solicitudes")]
    public IActionResult SaveMovement(TicketMovementInputModel input, string view = "queue")
    {
        List<TicketViewModel> matches;
        try
        {
            matches = _tickets.GetById(input.TicketId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el ticket {TicketId} antes de guardar el movimiento (GetById)", input.TicketId);
            return RedirectToAction("Requests", new { view });
        }

        var ticket = matches.FirstOrDefault();
        if (ticket is null)
        {
            return RedirectToAction("Requests", new { view });
        }

        // Group-agnostic list (not just "Agentes") so the newly selected agent
        // can be resolved by name even if they're not in that group — the
        // combo can now select agents from any group (see TicketDetail GET).
        List<SecurityUserViewModel> allUsers;
        try
        {
            allUsers = _security.GetAllUsers();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los usuarios (Security.GetAllUsers) al guardar el movimiento del ticket {TicketId}", input.TicketId);
            allUsers = new List<SecurityUserViewModel>();
        }

        var currentAgent = HttpContext.Session.GetObject<AgentUser>(SessionAgentKey);
        var hasFullStatusAccess = HasFullStatusAccess(currentAgent);

        List<TicketStatus> statuses;
        try
        {
            statuses = hasFullStatusAccess ? _tickets.GetStatusForSupervisor() : _tickets.GetStatusForAgent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener los estatus ({Fetch}) al guardar el movimiento del ticket {TicketId}", hasFullStatusAccess ? "GetStatusForSupervisor" : "GetStatusForAgent", input.TicketId);
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

        var actorName = FormatDisplayName(currentAgent?.FullName) ?? "Un agente";

        var newAgentName = FormatDisplayName(allUsers.FirstOrDefault(a => a.SecurityUserId == input.AgentId)?.FullName) ?? "un agente";
        var previousAgentName = FormatDisplayName(previousMovement?.FullName);

        // TicketDetail_Detail doesn't expose SecurityUserId, only FullName —
        // resolve the previous agent's real id by name to correctly detect
        // whether the agent actually changed (see also TicketDetail GET).
        var previousAgentIdResolved = previousMovement is null ? 0 : ResolveAgentIdByName(previousMovement.FullName, allUsers);
        int? previousAgentId = previousAgentIdResolved > 0 ? previousAgentIdResolved : null;

        var newStatusName = ResolveStatusName(input.StatusId, statuses);

        var observaciones = (input.StatusId == 3 || input.StatusId == 4) ? input.Observaciones?.Trim() : null;
        var minutos = input.StatusId == 4 ? input.Minutos : null;

        var description = BuildProcessDescription(
            actorName,
            previousAgentId,
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

        return RedirectToAction("TicketDetail", new { id = input.TicketId, view });
    }

    // TicketDetail_Detail only exposes the technician's FullName, not their
    // SecurityUserId, so movements are matched back to a real user by name.
    private static int ResolveAgentIdByName(string? fullName, List<SecurityUserViewModel> allUsers)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return 0;
        }

        return allUsers
            .FirstOrDefault(u => string.Equals(u.FullName?.Trim(), fullName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.SecurityUserId ?? 0;
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
