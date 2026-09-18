namespace CallCenterPlus.Models;

public class QueueAgingReportViewModel
{
    public int TotalInQueue { get; set; }
    public string AverageWaitingLabel { get; set; } = "—";
    public QueuedTicketRow? LongestWaiting { get; set; }
    public List<AgingBucket> Buckets { get; set; } = new();
    public List<QueuedTicketRow> Rows { get; set; } = new();
}

public class AgingBucket
{
    public string Label { get; set; } = string.Empty;
    public string Severity { get; set; } = "success";
    public int Count { get; set; }
}

public class QueuedTicketRow
{
    public int TicketId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public double WaitingHours { get; set; }
    public string WaitingLabel { get; set; } = string.Empty;
    public string Severity { get; set; } = "success";
}
