namespace CallCenterPlus.Models;

public class TicketVolumeReportViewModel
{
    public int TotalTickets { get; set; }
    public List<CategoryVolume> ByCategory { get; set; } = new();
    public List<StatusVolume> ByStatus { get; set; } = new();
    public List<CategoryVolume> ByDepartment { get; set; } = new();
}

public class CategoryVolume
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percentage { get; set; }
}

public class StatusVolume
{
    public int StatusId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percentage { get; set; }
}
