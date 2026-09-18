namespace CallCenterPlus.Models;

public class AgentResolutionTimeViewModel
{
    public int TotalResolved { get; set; }
    public double OverallAverageMinutes { get; set; }
    public List<AgentResolutionTime> ByAgent { get; set; } = new();
}

public class AgentResolutionTime
{
    public string AgentName { get; set; } = string.Empty;
    public int ResolvedCount { get; set; }
    public int AverageMinutes { get; set; }
    public int MinMinutes { get; set; }
    public int MaxMinutes { get; set; }
}
