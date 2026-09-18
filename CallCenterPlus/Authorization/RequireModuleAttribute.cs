namespace CallCenterPlus.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireModuleAttribute : Attribute
{
    public string ModuleName { get; }

    public RequireModuleAttribute(string moduleName)
    {
        ModuleName = moduleName;
    }
}
