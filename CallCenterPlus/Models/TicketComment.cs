namespace CallCenterPlus.Models;

public class TicketComment
{
    public string Author { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsTechnician { get; set; }
}
