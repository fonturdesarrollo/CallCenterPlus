namespace CallCenterPlus.Models
{
	public class TicketDetail
	{
		public int TicketDetailId { get; set; }
		public DateTime TicketMovementDate { get; set; }
		public string? TicketDetailRemarksByTechnician { get; set; }
		public int TicketDetailMinutesByTechnician { get; set; }
		public DateTime? TicketDetailEndDateByTechnician { get; set; }
		public int TicketId { get; set; }
		public int SecurityUserId { get; set; }
		public int ServiceAreaDetailId { get; set; }
		public string? TicketRemarks { get; set; }
		public int EmployeeId { get; set; }
		public int EmployeeIdNumber { get; set; }
		public string? ManagementName { get; set; }
		public string? EmployeeName { get; set; }
		public string? ManagementDivisionName { get; set; }
		public int TicketStatusId { get; set; }
		public string? TicketStatusName { get; set; }
		public DateTime? TicketEndDate { get; set; }
		public DateTime TicketStartDate { get; set; }
		public int EmployeePhone { get; set; }
		public string? ServiceAreaDetailName { get; set; }
		public string? FullName { get; set; }
		public string? TicketDetailProcessDescrption { get; set; }
	}
}
