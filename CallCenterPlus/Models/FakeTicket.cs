namespace CallCenterPlus.Models;

public class FakeTicket
{
    public string Number { get; set; } = string.Empty;
    public int EmployeeIdNumber { get; set; }
    public string RequestTypeName { get; set; } = string.Empty;
    public string RequestTypeIcon { get; set; } = "bi-question-circle";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Abierto";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? AssignedTechnician { get; set; }
    public int? MinutesSpent { get; set; }
    public List<TicketComment> Comments { get; set; } = new();
}
