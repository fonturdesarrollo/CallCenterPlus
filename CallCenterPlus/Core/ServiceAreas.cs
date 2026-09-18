using CallCenterPlus.Core.Data;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CallCenterPlus.Core
{
	public class ServiceAreas : IServiceAreas
	{
		private readonly ISqlConnectionFactory _connectionFactory;
		public ServiceAreas(ISqlConnectionFactory connectionFactory)
		{
			_connectionFactory = connectionFactory;
		}

		public List<ServiceArea> GetAll()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM ServiceArea_Detail ORDER BY ServiceAreaId, ServiceAreaDetailName", connection)
				{
					CommandType = CommandType.Text
				};

				var list = new List<ServiceArea>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new ServiceArea
					{
						ServiceAreaId = reader.GetInt32(reader.GetOrdinal("ServiceAreaId")),
						ServiceAreaName = reader.IsDBNull(reader.GetOrdinal("ServiceAreaName")) ? null : reader.GetString(reader.GetOrdinal("ServiceAreaName")),
						ServiceAreaDetailId = reader.GetInt32(reader.GetOrdinal("ServiceAreaDetailId")),
						ServiceAreaDetailName = reader.IsDBNull(reader.GetOrdinal("ServiceAreaDetailName")) ? null : reader.GetString(reader.GetOrdinal("ServiceAreaDetailName")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener las areas de servicio", ex);
			}
		}
	}
}
