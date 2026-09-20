namespace CallCenterPlus.Models;

public class PendingRequestRow
{
    public int TicketId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string RequestTypeName { get; set; } = string.Empty;
    public string TicketRemarks { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "En cola";
    public string Technician { get; set; } = "—";

    public string Initials =>
        string.IsNullOrWhiteSpace(EmployeeName)
            ? "?"
            : string.Concat(EmployeeName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
}
