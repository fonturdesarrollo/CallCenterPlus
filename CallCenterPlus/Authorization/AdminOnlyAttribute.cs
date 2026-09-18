namespace CallCenterPlus.Authorization;

// Marks a controller/action as restricted to the Admin group (SecurityGroupId 1),
// for screens that don't have a SecurityModule of their own yet.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class AdminOnlyAttribute : Attribute
{
}
