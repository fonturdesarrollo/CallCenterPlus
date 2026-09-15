namespace CallCenterPlus.Models;

public class AddGroupModuleViewModel
{
    public SecurityGroupModuleModel Assignment { get; set; } = new();
    public List<SecurityGroupModel> AvailableGroups { get; set; } = new();
    public List<SecurityModuleModel> AvailableModules { get; set; } = new();
    public List<SecurityAccessTypeModel> AvailableAccessTypes { get; set; } = new();
    public List<SecurityGroupModuleModel> ExistingAssignments { get; set; } = new();
}
