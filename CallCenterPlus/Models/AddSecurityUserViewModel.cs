namespace CallCenterPlus.Models;

public class AddSecurityUserViewModel
{
    public SecurityUserViewModel User { get; set; } = new();
    public List<SecurityGroupModel> AvailableGroups { get; set; } = new();
    public List<SecurityStatusUserModel> AvailableStatuses { get; set; } = new();
    public List<GeographyViewModel> AvailableStates { get; set; } = new();
}
