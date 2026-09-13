using CallCenterPlus.Core;
using CallCenterPlus.Core.Data;
using CallCenterPlus.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CallCenterPlus.Core
{
	public class Employees : IEmployees
	{

		private readonly ISqlConnectionFactory _connectionFactory;

		public Employees(ISqlConnectionFactory connectionFactory)
		{
			_connectionFactory = connectionFactory;
		}

		public Employee GetByEmployeeIdNumber(int employeeIdNumber)
		{
			try
			{
				using (SqlConnection sqlConnection = _connectionFactory.CreateConnection())
				{
					sqlConnection.Open();

					Employee employee = new();

					SqlCommand cmd = new("SELECT * FROM Employee WHERE EmployeeIdNumber = @EmployeeIdNumber", sqlConnection);
					cmd.Parameters.AddWithValue("@EmployeeIdNumber", employeeIdNumber);

					using (SqlDataReader dr = cmd.ExecuteReader())
					{
						while (dr.Read())
						{
							employee.EmployeeId = (int)dr["EmployeeId"];
							employee.EmployeeIdNumber = (int)dr["EmployeeIdNumber"];
							employee.EmployeeName = (string)dr["EmployeeName"];

							employee.JobTitle = (string)dr["JobTitle"];
							employee.ManagementName = (string)dr["ManagementName"];
							employee.ManagementDivisionName = (string)dr["ManagementDivisionName"];
							employee.StateName = (string)dr["StateName"];
						}
					}

					return employee;
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al obtener los empleados {ex.Message}", ex);
			}
		}
	}
}
