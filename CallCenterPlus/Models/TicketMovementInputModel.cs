namespace CallCenterPlus.Models;

public class TicketMovementInputModel
{
    public int TicketId { get; set; }
    public int AgentId { get; set; }
    public int StatusId { get; set; }
    public string? Observaciones { get; set; }
    public int? Minutos { get; set; }
    public DateTime? FechaFinalizacion { get; set; }
}
