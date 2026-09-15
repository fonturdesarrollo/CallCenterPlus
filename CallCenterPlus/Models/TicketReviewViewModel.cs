namespace CallCenterPlus.Models;

public class TicketReviewViewModel
{
    public TicketViewModel Ticket { get; set; } = new();
    public string ServiceAreaDetailName { get; set; } = string.Empty;

    public List<SecurityUserViewModel> AvailableAgents { get; set; } = new();
    public List<TicketStatus> AvailableStatuses { get; set; } = new();
    public List<TicketDetail> Movements { get; set; } = new();

    public int SelectedAgentId { get; set; }
    public int SelectedStatusId { get; set; } = 2;
}
