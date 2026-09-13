namespace CallCenterPlus.Models;

/// <summary>
/// Fake, session-only identity for the call center agent panel. Replace with
/// real authentication (SecurityUser) once security is implemented.
/// </summary>
public class AgentUser
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public string Initials =>
        string.IsNullOrWhiteSpace(FullName)
            ? "?"
            : string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
}
