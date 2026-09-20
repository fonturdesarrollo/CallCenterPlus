namespace CallCenterPlus.Models;

/// <summary>
/// Session identity for the call center agent panel, populated from a real
/// SecurityUser after a successful login (see ISecurity.GetValidUser).
/// </summary>
public class AgentUser
{
    public int SecurityUserId { get; set; }
    public int SecurityGroupId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;

    public string Initials =>
        string.IsNullOrWhiteSpace(FullName)
            ? "?"
            : string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
}
