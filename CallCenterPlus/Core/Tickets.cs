using CallCenterPlus.Core.Data;
using CallCenterPlus.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CallCenterPlus.Core
{
	public class Tickets : ITickets
	{
		private readonly ISqlConnectionFactory _connectionFactory;
		public Tickets(ISqlConnectionFactory connectionFactory)
		{
			_connectionFactory = connectionFactory;
		}

		public int AddOrEdit(TicketViewModel model)
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("Ticket_AddOrEdit", connection)
				{
					CommandType = CommandType.StoredProcedure
				};

				cmd.Parameters.AddWithValue("@TicketId", model.TicketId);
				cmd.Parameters.AddWithValue("@ServiceAreaDetailId", model.ServiceAreaDetailId);
				cmd.Parameters.AddWithValue("@TicketRemarks", (object?)model.TicketRemarks ?? DBNull.Value);
				cmd.Parameters.AddWithValue("@EmployeeId", model.EmployeeId);
				cmd.Parameters.AddWithValue("@ManagementName", model.ManagementName ?? string.Empty);
				cmd.Parameters.AddWithValue("@ManagementDivisionName", model.ManagementDivisionName ?? string.Empty);
				cmd.Parameters.AddWithValue("@TicketStatusId", model.TicketStatusId);
				cmd.Parameters.AddWithValue("@TicketEndDate", (object?)model.TicketEndDate ?? DBNull.Value);

				var result = cmd.ExecuteScalar();

				if (result is null || result is DBNull)
				{
					return model.TicketId;
				}

				return Convert.ToInt32(result);
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al guardar el ticket: {ex.Message}", ex);
			}
		}

		public List<TicketViewModel> GetByInQueue()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Ticket_Detail WHERE TicketStatusId = 1", connection)
				{
					CommandType = CommandType.Text
				};

				//cmd.Parameters.AddWithValue("@AccounTypeId", accounTypeId);
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
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los tickets", ex);
			}
		}
	}
}
