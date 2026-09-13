namespace CallCenterPlus.Models
{
	public class TicketViewModel
	{
		public int TicketId { get; set; }
		public int ServiceAreaDetailId { get; set; }
		public string? TicketRemarks { get; set; }
		public int EmployeeId { get; set; }
		public int EmployeeIdNumber { get; set; }
		public string? ManagementName { get; set; }
		public string? EmployeeName { get; set; }
		public string? ManagementDivisionName { get; set; }
		public int TicketStatusId { get; set; }
		public DateTime? TicketEndDate { get; set; }
		public DateTime TicketStartDate { get; set; }
	}
}
