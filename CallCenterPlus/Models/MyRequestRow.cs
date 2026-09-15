namespace CallCenterPlus.Models;

public class MyRequestRow
{
    public int TicketId { get; set; }
    public string RequestTypeName { get; set; } = string.Empty;
    public string RequestTypeIcon { get; set; } = "bi-card-list";
    public string TicketRemarks { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int TicketStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
}
