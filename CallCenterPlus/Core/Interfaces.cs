using CallCenterPlus.Models;

namespace CallCenterPlus.Core
{
	public interface ISecurity
	{
		public int AddOrEditUser(SecurityUserViewModel model);
		public int AddOrEditGroup(SecurityGroupModel model);
		public int AddOrEditModule(SecurityModuleModel model);
		public int AddOrEditGroupModules(SecurityGroupModuleModel model);
		public int DeleteGroupModules(int securityGroupModuleId);
		public List<SecurityModuleModel> GetModulesByGroupId(int groupId);
		public List<SecurityModuleModel> GetAllModules();
		public SecurityUserViewModel GetValidUser(string login, string password);
		public List<SecurityUserViewModel> GetAllUsers();
		public List<SecurityGroupModel> GetAllGroups();
		public List<SecurityGroupModuleModel> GetAllGroupModules();
		public List<SecurityAccessTypeModel> GetAllAccessTypes();
		public List<SecurityStatusUserModel> GetAllUsersStatus();
		public List<SecurityUserViewModel> GetAgentsForTickets();
		public bool GroupHasAccessToModule(int securityGroupId, int securityModuleId);
		public string? Encrypt(string plainText);
	}

	public interface IEmployees
	{
		public  Employee GetByEmployeeIdNumber(int employeeIdNumber);
	}

	public interface IServiceAreas
	{
		public List<ServiceArea> GetAll();
	}
	public interface ITickets
	{
		public int AddOrEdit(TicketViewModel model);
		public int AddOrEditTicketDetail(TicketDetail model);		
		public List<TicketViewModel> GetByInQueue();
		public List<TicketViewModel> GetByWithAgent();
		public List<TicketViewModel> GetByEnded();
		public List<TicketViewModel> GetById(int ticketId);
		public List<TicketViewModel> GetByEmployeeId(int employeeId);
		public List<TicketStatus> GetStatusForAgent();
		public List<TicketStatus> GetStatusForSupervisor();
		public List<TicketDetail> GetDetailByTicketId(int ticketId);
		public List<TicketDetail> GetAllTicketDetails();
	}
	public interface IGeography
	{
		public List<GeographyViewModel> GetAllStates();
		public List<GeographyViewModel> GetStateById(int stateId);
		public List<GeographyViewModel> GetAllMunicipalities();
		public List<GeographyViewModel> GetMunicipalityByStateId(int stateId);
	}
}
