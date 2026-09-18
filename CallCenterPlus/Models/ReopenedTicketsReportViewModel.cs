namespace CallCenterPlus.Models;

public class ReopenedTicketsReportViewModel
{
    public int TotalTickets { get; set; }
    public int TotalReopenedTickets { get; set; }
    public int TotalReopenEvents { get; set; }
    public int ReopenRatePercentage { get; set; }
    public List<CategoryVolume> ByCategory { get; set; } = new();
    public List<ReopenedTicketRow> Rows { get; set; } = new();
}

public class ReopenedTicketRow
{
    public int TicketId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime ReopenedAt { get; set; }
    public string PreviousAgentName { get; set; } = "—";
    public string Observation { get; set; } = "—";
}
