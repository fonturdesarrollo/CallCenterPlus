using CallCenterPlus.Models;

namespace CallCenterPlus.Core
{
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
		public List<TicketViewModel> GetByInQueue();
	}
}
