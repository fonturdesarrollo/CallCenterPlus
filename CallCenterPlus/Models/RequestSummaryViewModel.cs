namespace CallCenterPlus.Models;

public class RequestSummaryViewModel
{
    public Employee Requester { get; set; } = new();
    public string RequestTypeName { get; set; } = string.Empty;
    public string RequestTypeIcon { get; set; } = "bi-question-circle";
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
}
