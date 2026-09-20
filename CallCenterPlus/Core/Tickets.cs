using CallCenterPlus.Core.Data;
using CallCenterPlus.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CallCenterPlus.Core
{
	public class Tickets : ITickets
	{
		private readonly ISqlConnectionFactory _connectionFactory;
		private readonly ISecurity _security;
		public Tickets(ISqlConnectionFactory connectionFactory, ISecurity security)
		{
			_connectionFactory = connectionFactory;
			_security = security;
		}

		public int AddOrEdit(TicketViewModel model)
		{
			int result = 0;

			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("Ticket_AddOrEdit", connection)
				{
					CommandType = CommandType.StoredProcedure
				};

				if(model != null)
				{
					cmd.Parameters.AddWithValue("@TicketId", model.TicketId);
					cmd.Parameters.AddWithValue("@ServiceAreaDetailId", model.ServiceAreaDetailId);
					cmd.Parameters.AddWithValue("@TicketRemarks", (object?)model.TicketRemarks ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
					cmd.Parameters.AddWithValue("@ManagementName", model.ManagementName ?? string.Empty);
					cmd.Parameters.AddWithValue("@ManagementDivisionName", model.ManagementDivisionName ?? string.Empty);
					cmd.Parameters.AddWithValue("@TicketStatusId", model.TicketStatusId);
					cmd.Parameters.AddWithValue("@EmployeePhone", model.EmployeePhone);
					cmd.Parameters.AddWithValue("@TicketEndDate", (object?)model.TicketEndDate ?? DBNull.Value);

					var execResult = cmd.ExecuteScalar();

					if (execResult is null || execResult is DBNull)
					{
						return model.TicketId;
					}

					_security.AddLogbook(model.TicketId, false,
						$" Solicitante ->" +
						$" nombre: {model.EmployeeName} -" +
						$" cedula n°: {model.EmployeeIdNumber} -" +
						$" gerencia: {model.ManagementName} -" +
						$" division: {model.ManagementDivisionName} -" +
						$" requerimiento: {model.TicketRemarks} -");

					return Convert.ToInt32(execResult);
				}

				return result;
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al guardar el ticket: {ex.Message}", ex);
			}
		}

		public int AddOrEditTicketDetail(TicketDetail model)
		{
			int result = 0;

			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("TicketDetail_AddOrEdit", connection)
				{
					CommandType = CommandType.StoredProcedure
				};

				if (model != null)
				{
					cmd.Parameters.AddWithValue("@TicketDetailId", model.TicketDetailId);
					cmd.Parameters.AddWithValue("@TicketId", model.TicketId);
					cmd.Parameters.AddWithValue("@SecurityUserId", model.SecurityUserId);
					cmd.Parameters.AddWithValue("@TicketDetailRemarksByTechnician", (object?)model.TicketDetailRemarksByTechnician ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TicketDetailMinutesByTechnician", model.TicketDetailMinutesByTechnician);
					cmd.Parameters.AddWithValue("@TicketDetailEndDateByTechnician", (object?)model.TicketDetailEndDateByTechnician ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TicketStatusId", model.TicketStatusId);
					cmd.Parameters.AddWithValue("@TicketDetailProcessDescrption", (object?)model.TicketDetailProcessDescrption ?? DBNull.Value);

					var execResult = cmd.ExecuteScalar();

					if (execResult is null || execResult is DBNull)
					{
						return model.TicketId;
					}

					// TicketDetailProcessDescrption is already the human-readable
					// summary of this movement (agent/status/observaciones/minutos,
					// built by the caller — AgentController.BuildProcessDescription
					// or RequestController's reopen flow), so reuse it as-is instead
					// of re-describing the change from scratch.
					_security.AddLogbook(model.TicketId, false,
						string.IsNullOrWhiteSpace(model.TicketDetailProcessDescrption)
							? $"Movimiento en el ticket #{model.TicketId} (estatus {model.TicketStatusId})."
							: $"Ticket #{model.TicketId} -> {model.TicketDetailProcessDescrption}");

					return Convert.ToInt32(execResult);
				}

				return result;
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al guardar el detalle del ticket: {ex.Message}", ex);
			}
		}

		public List<TicketViewModel> GetByInQueue()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Ticket_Detail WHERE TicketStatusId = 1 ORDER BY TicketId", connection);

				var list = new List<TicketViewModel>();
				using SqlDataReader reader = cmd.ExecuteReader();

				while (reader.Read())
				{
					list.Add(new TicketViewModel
					{
						TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
						ServiceAreaDetailId = reader.GetInt32(reader.GetOrdinal("ServiceAreaDetailId")),
						TicketRemarks = reader.IsDBNull(reader.GetOrdinal("TicketRemarks")) ? null : reader.GetString(reader.GetOrdinal("TicketRemarks")),
						EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
						EmployeeIdNumber = reader.GetInt32(reader.GetOrdinal("EmployeeIdNumber")),
						EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
						ManagementName = reader.IsDBNull(reader.GetOrdinal("ManagementName")) ? null : reader.GetString(reader.GetOrdinal("ManagementName")),
						ManagementDivisionName = reader.IsDBNull(reader.GetOrdinal("ManagementDivisionName")) ? null : reader.GetString(reader.GetOrdinal("ManagementDivisionName")),
						TicketStartDate = reader.GetDateTime(reader.GetOrdinal("TicketStartDate")),
						EmployeePhone = reader.GetInt32(reader.GetOrdinal("EmployeePhone")),
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los tickets", ex);
			}
		}

		public List<TicketViewModel> GetByWithAgent()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Ticket_Detail WHERE TicketStatusId > 1 AND TicketStatusId < 4", connection);

				var list = new List<TicketViewModel>();
				using SqlDataReader reader = cmd.ExecuteReader();

				while (reader.Read())
				{
					list.Add(new TicketViewModel
					{
						TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
						ServiceAreaDetailId = reader.GetInt32(reader.GetOrdinal("ServiceAreaDetailId")),
						TicketRemarks = reader.IsDBNull(reader.GetOrdinal("TicketRemarks")) ? null : reader.GetString(reader.GetOrdinal("TicketRemarks")),
						EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
						EmployeeIdNumber = reader.GetInt32(reader.GetOrdinal("EmployeeIdNumber")),
						EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
						ManagementName = reader.IsDBNull(reader.GetOrdinal("ManagementName")) ? null : reader.GetString(reader.GetOrdinal("ManagementName")),
						ManagementDivisionName = reader.IsDBNull(reader.GetOrdinal("ManagementDivisionName")) ? null : reader.GetString(reader.GetOrdinal("ManagementDivisionName")),
						TicketStartDate = reader.GetDateTime(reader.GetOrdinal("TicketStartDate")),
						EmployeePhone = reader.GetInt32(reader.GetOrdinal("EmployeePhone")),
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los tickets", ex);
			}
		}

		public List<TicketViewModel> GetByEnded()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Ticket_Detail WHERE TicketStatusId >= 4", connection);

				var list = new List<TicketViewModel>();
				using SqlDataReader reader = cmd.ExecuteReader();

				while (reader.Read())
				{
					list.Add(new TicketViewModel
					{
						TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
						ServiceAreaDetailId = reader.GetInt32(reader.GetOrdinal("ServiceAreaDetailId")),
						TicketRemarks = reader.IsDBNull(reader.GetOrdinal("TicketRemarks")) ? null : reader.GetString(reader.GetOrdinal("TicketRemarks")),
						EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
						EmployeeIdNumber = reader.GetInt32(reader.GetOrdinal("EmployeeIdNumber")),
						EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
						ManagementName = reader.IsDBNull(reader.GetOrdinal("ManagementName")) ? null : reader.GetString(reader.GetOrdinal("ManagementName")),
						ManagementDivisionName = reader.IsDBNull(reader.GetOrdinal("ManagementDivisionName")) ? null : reader.GetString(reader.GetOrdinal("ManagementDivisionName")),
						TicketStartDate = reader.GetDateTime(reader.GetOrdinal("TicketStartDate")),
						EmployeePhone = reader.GetInt32(reader.GetOrdinal("EmployeePhone")),
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los tickets", ex);
			}
		}

		public List<TicketViewModel> GetById(int ticketId)
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Ticket_Detail WHERE TicketId = @TicketId", connection);
				cmd.Parameters.AddWithValue("@TicketId", ticketId);

				var list = new List<TicketViewModel>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new TicketViewModel
					{
						TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
						ServiceAreaDetailId = reader.GetInt32(reader.GetOrdinal("ServiceAreaDetailId")),
						TicketRemarks = reader.IsDBNull(reader.GetOrdinal("TicketRemarks")) ? null : reader.GetString(reader.GetOrdinal("TicketRemarks")),
						EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
						EmployeeIdNumber = reader.GetInt32(reader.GetOrdinal("EmployeeIdNumber")),
						EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
						ManagementName = reader.IsDBNull(reader.GetOrdinal("ManagementName")) ? null : reader.GetString(reader.GetOrdinal("ManagementName")),
						ManagementDivisionName = reader.IsDBNull(reader.GetOrdinal("ManagementDivisionName")) ? null : reader.GetString(reader.GetOrdinal("ManagementDivisionName")),
						TicketStartDate = reader.GetDateTime(reader.GetOrdinal("TicketStartDate")),
						EmployeePhone = reader.GetInt32(reader.GetOrdinal("EmployeePhone")),
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los tickets", ex);
			}
		}

		public List<TicketViewModel> GetByEmployeeId(int employeeId)
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Ticket_Detail WHERE EmployeeId = @EmployeeId", connection);
				cmd.Parameters.AddWithValue("@EmployeeId", employeeId);

				var list = new List<TicketViewModel>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new TicketViewModel
					{
						TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
						ServiceAreaDetailId = reader.GetInt32(reader.GetOrdinal("ServiceAreaDetailId")),
						TicketRemarks = reader.IsDBNull(reader.GetOrdinal("TicketRemarks")) ? null : reader.GetString(reader.GetOrdinal("TicketRemarks")),
						EmployeeId = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
						EmployeeIdNumber = reader.GetInt32(reader.GetOrdinal("EmployeeIdNumber")),
						EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
						ManagementName = reader.IsDBNull(reader.GetOrdinal("ManagementName")) ? null : reader.GetString(reader.GetOrdinal("ManagementName")),
						ManagementDivisionName = reader.IsDBNull(reader.GetOrdinal("ManagementDivisionName")) ? null : reader.GetString(reader.GetOrdinal("ManagementDivisionName")),
						TicketStartDate = reader.GetDateTime(reader.GetOrdinal("TicketStartDate")),
						EmployeePhone = reader.GetInt32(reader.GetOrdinal("EmployeePhone")),
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los tickets del empleado", ex);
			}
		}

		public List<TicketDetail> GetDetailByTicketId(int ticketId)
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM TicketDetail_Detail WHERE TicketId = @TicketId", connection);
				cmd.Parameters.AddWithValue("@TicketId", ticketId);

				var list = new List<TicketDetail>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new TicketDetail
					{
						TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
						TicketDetailId = reader.GetInt32(reader.GetOrdinal("TicketDetailId")),
						TicketMovementDate = reader.GetDateTime(reader.GetOrdinal("TicketMovementDate")),
						TicketDetailRemarksByTechnician = reader.IsDBNull(reader.GetOrdinal("TicketDetailRemarksByTechnician")) ? null : reader.GetString(reader.GetOrdinal("TicketDetailRemarksByTechnician")),
						TicketDetailMinutesByTechnician = reader.GetInt32(reader.GetOrdinal("TicketDetailMinutesByTechnician")),
						TicketDetailEndDateByTechnician = reader.IsDBNull(reader.GetOrdinal("TicketDetailEndDateByTechnician")) ? null : reader.GetDateTime(reader.GetOrdinal("TicketDetailEndDateByTechnician")),
						ServiceAreaDetailName = reader.IsDBNull(reader.GetOrdinal("ServiceAreaDetailName")) ? null : reader.GetString(reader.GetOrdinal("ServiceAreaDetailName")),
						TicketRemarks = reader.IsDBNull(reader.GetOrdinal("TicketRemarks")) ? null : reader.GetString(reader.GetOrdinal("TicketRemarks")),
						EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
						ManagementName = reader.IsDBNull(reader.GetOrdinal("ManagementName")) ? null : reader.GetString(reader.GetOrdinal("ManagementName")),
						ManagementDivisionName = reader.IsDBNull(reader.GetOrdinal("ManagementDivisionName")) ? null : reader.GetString(reader.GetOrdinal("ManagementDivisionName")),
						EmployeePhone = reader.GetInt32(reader.GetOrdinal("EmployeePhone")),
						TicketStartDate = reader.GetDateTime(reader.GetOrdinal("TicketStartDate")),
						TicketStatusName = reader.IsDBNull(reader.GetOrdinal("TicketStatusName")) ? null : reader.GetString(reader.GetOrdinal("TicketStatusName")),
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
						FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? null : reader.GetString(reader.GetOrdinal("FullName")),
						TicketDetailProcessDescrption = reader.IsDBNull(reader.GetOrdinal("TicketDetailProcessDescrption")) ? null : reader.GetString(reader.GetOrdinal("TicketDetailProcessDescrption")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los tickets", ex);
			}
		}

		public List<TicketDetail> GetAllTicketDetails()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM TicketDetail_Detail ORDER BY TicketId, TicketMovementDate", connection);

				var list = new List<TicketDetail>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new TicketDetail
					{
						TicketId = reader.GetInt32(reader.GetOrdinal("TicketId")),
						TicketDetailId = reader.GetInt32(reader.GetOrdinal("TicketDetailId")),
						TicketMovementDate = reader.GetDateTime(reader.GetOrdinal("TicketMovementDate")),
						TicketDetailRemarksByTechnician = reader.IsDBNull(reader.GetOrdinal("TicketDetailRemarksByTechnician")) ? null : reader.GetString(reader.GetOrdinal("TicketDetailRemarksByTechnician")),
						TicketDetailMinutesByTechnician = reader.GetInt32(reader.GetOrdinal("TicketDetailMinutesByTechnician")),
						TicketDetailEndDateByTechnician = reader.IsDBNull(reader.GetOrdinal("TicketDetailEndDateByTechnician")) ? null : reader.GetDateTime(reader.GetOrdinal("TicketDetailEndDateByTechnician")),
						ServiceAreaDetailName = reader.IsDBNull(reader.GetOrdinal("ServiceAreaDetailName")) ? null : reader.GetString(reader.GetOrdinal("ServiceAreaDetailName")),
						TicketRemarks = reader.IsDBNull(reader.GetOrdinal("TicketRemarks")) ? null : reader.GetString(reader.GetOrdinal("TicketRemarks")),
						EmployeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? null : reader.GetString(reader.GetOrdinal("EmployeeName")),
						ManagementName = reader.IsDBNull(reader.GetOrdinal("ManagementName")) ? null : reader.GetString(reader.GetOrdinal("ManagementName")),
						ManagementDivisionName = reader.IsDBNull(reader.GetOrdinal("ManagementDivisionName")) ? null : reader.GetString(reader.GetOrdinal("ManagementDivisionName")),
						EmployeePhone = reader.GetInt32(reader.GetOrdinal("EmployeePhone")),
						TicketStartDate = reader.GetDateTime(reader.GetOrdinal("TicketStartDate")),
						TicketStatusName = reader.IsDBNull(reader.GetOrdinal("TicketStatusName")) ? null : reader.GetString(reader.GetOrdinal("TicketStatusName")),
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
						FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? null : reader.GetString(reader.GetOrdinal("FullName")),
						TicketDetailProcessDescrption = reader.IsDBNull(reader.GetOrdinal("TicketDetailProcessDescrption")) ? null : reader.GetString(reader.GetOrdinal("TicketDetailProcessDescrption")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los movimientos de los tickets", ex);
			}
		}

		public List<TicketStatus> GetStatusForAgent()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM TicketStatus WHERE TicketStatusId > 1 AND TicketStatusId < 5", connection);

				var list = new List<TicketStatus>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new TicketStatus
					{
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
						TicketStatusName = reader.IsDBNull(reader.GetOrdinal("TicketStatusName")) ? null : reader.GetString(reader.GetOrdinal("TicketStatusName")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los estatus", ex);
			}
		}

		public List<TicketStatus> GetStatusForSupervisor()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM TicketStatus WHERE TicketStatusId > 1", connection);

				var list = new List<TicketStatus>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new TicketStatus
					{
						TicketStatusId = reader.GetInt32(reader.GetOrdinal("TicketStatusId")),
						TicketStatusName = reader.IsDBNull(reader.GetOrdinal("TicketStatusName")) ? null : reader.GetString(reader.GetOrdinal("TicketStatusName")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los estatus", ex);
			}
		}
	}
}
