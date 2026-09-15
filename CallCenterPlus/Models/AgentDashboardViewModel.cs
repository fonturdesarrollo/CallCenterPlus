namespace CallCenterPlus.Models;

public class AgentDashboardViewModel
{
    public List<AgentKpiCard> Kpis { get; set; } = new();
    public List<CategoryBreakdown> TopCategories { get; set; } = new();
    public List<PendingRequestRow> PendingRequests { get; set; } = new();
}

public class AgentKpiCard
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string TrendLabel { get; set; } = string.Empty;
    public bool TrendUp { get; set; }
    public string ColorClass { get; set; } = string.Empty;
    public List<int> Sparkline { get; set; } = new();
}

public class CategoryBreakdown
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percentage { get; set; }
    public string Icon { get; set; } = "bi-folder";
}

public class PendingRequestRow
{
    public int TicketId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string RequestTypeName { get; set; } = string.Empty;
    public string TicketRemarks { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "En cola";

    public string Initials =>
        string.IsNullOrWhiteSpace(EmployeeName)
            ? "?"
            : string.Concat(EmployeeName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
}
