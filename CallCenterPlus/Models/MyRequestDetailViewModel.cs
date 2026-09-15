namespace CallCenterPlus.Models;

public class MyRequestDetailViewModel
{
    public int TicketId { get; set; }
    public string RequestTypeName { get; set; } = string.Empty;
    public string RequestTypeIcon { get; set; } = "bi-card-list";
    public string? Description { get; set; }
    public int TicketStatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? AssignedTechnician { get; set; }
    public int? MinutesSpent { get; set; }
    public List<TicketDetail> Movements { get; set; } = new();
}
