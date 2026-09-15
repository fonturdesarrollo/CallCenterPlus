using CallCenterPlus.Core.Data;
using CallCenterPlus.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CallCenterPlus.Core
{
	public class Geography : IGeography
	{
		private readonly ISqlConnectionFactory _connectionFactory;
		public Geography(ISqlConnectionFactory connectionFactory)
		{
			_connectionFactory = connectionFactory;
		}

		public List<GeographyViewModel> GetAllStates()
		{
			List<GeographyViewModel> states = new();

			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM State ORDER BY StateName", connection);

				using (SqlDataReader dr = cmd.ExecuteReader())
				{
					while (dr.Read())
					{
						states.Add(new GeographyViewModel
						{
							StateId = dr.GetInt32(dr.GetOrdinal("StateId")),
							StateName = dr.GetString(dr.GetOrdinal("StateName"))
						});
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los estados", ex);
			}

			return states;
		}

		public List<GeographyViewModel> GetStateById(int stateId)
		{
			List<GeographyViewModel> state = new();

			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM State WHERE StateId = @StateId", connection);
				cmd.Parameters.AddWithValue("@StateId", stateId);

				using (SqlDataReader dr = cmd.ExecuteReader())
				{
					while (dr.Read())
					{
						state.Add(new GeographyViewModel
						{
							StateId = (int)dr["StateId"],
							StateName = (string)dr["StateName"]
						});
					}
				}				
			}
			catch (Exception ex)
			{
				throw;
			}

			return state;
		}

		public List<GeographyViewModel> GetAllMunicipalities()
		{
			List<GeographyViewModel> municipalities = new();

			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Municipality ORDER BY MunicipalityName", connection);

				using (SqlDataReader dr = cmd.ExecuteReader())
				{
					while (dr.Read())
					{
						municipalities.Add(new GeographyViewModel
						{
							MunicipalityId = dr.GetInt32(dr.GetOrdinal("MunicipalityId")),
							MunicipalityName = dr.GetString(dr.GetOrdinal("MunicipalityName"))
						});
					}
				}				
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los municipios", ex);
			}

			return municipalities;
		}

		public List<GeographyViewModel> GetMunicipalityByStateId(int stateId)
		{
			List<GeographyViewModel> municipality = new();

			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Geography_GetMunicipalityByStateId WHERE StateId = @StateId", connection);
				cmd.Parameters.AddWithValue("@StateId", stateId);

				using (SqlDataReader dr = cmd.ExecuteReader())
				{
					while (dr.Read())
					{
						municipality.Add(new GeographyViewModel
						{
							MunicipalityId = (int)dr["MunicipalityId"],
							MunicipalityName = (string)dr["MunicipalityName"]
						});
					}
				}				
			}
			catch (Exception ex)
			{
				throw;
			}

			return municipality;
		}
	}
}
