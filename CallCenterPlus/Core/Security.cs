using CallCenterPlus.Core.Data;
using CallCenterPlus.Extensions;
using CallCenterPlus.Models;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace CallCenterPlus.Core
{
	public class Security : ISecurity
	{
		private readonly IConfiguration _configuration;
		private readonly ISqlConnectionFactory _connectionFactory;
		private string Key = string.Empty;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private const string allowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		private static RSA rsa = RSA.Create();
		private readonly ClientInfoService _clientInfoService;

		public Security(ISqlConnectionFactory connectionFactory, IHttpContextAccessor httpContextAccessor, IConfiguration configuration, ClientInfoService clientInfoService)
		{
			_connectionFactory = connectionFactory;
			_httpContextAccessor = httpContextAccessor;
			_configuration = configuration;
			_clientInfoService = clientInfoService;
			Key = _configuration["Cryptography:Key"]
				?? throw new InvalidOperationException("La clave 'Cryptography:Key' no fue encontrada en appsettings.");
		}

		public int AddOrEditUser(SecurityUserViewModel model)
		{
			int result = 0;
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("Security_UserAddOrEdit", connection)
				{
					CommandType = CommandType.StoredProcedure
				};

				if (model != null)
				{
					cmd.Parameters.AddWithValue("@SecurityUserId", model.SecurityUserId);
					cmd.Parameters.AddWithValue("@SecurityUserDocumentIdNumber", model.SecurityUserDocumentIdNumber);
					cmd.Parameters.AddWithValue("@UserName", model.UserName);
					cmd.Parameters.AddWithValue("@Password", model.Password);
					cmd.Parameters.AddWithValue("@FullName", model.FullName.ToUpper());
					cmd.Parameters.AddWithValue("@SecurityGroupId", model.SecurityGroupId);
					cmd.Parameters.AddWithValue("@SecurityStatusId", model.SecurityStatusId);
					cmd.Parameters.AddWithValue("@StateId", model.StateId);

					result = Convert.ToInt32(cmd.ExecuteScalar());

					AddLogbook(model.SecurityUserId, false, $"usuario {model.FullName.ToUpper()} login {model.UserName} grupo Id {model.SecurityGroupId} estatus {model.SecurityStatusId}");
				}				

				return result;
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al añadir o editar el usuario {ex.Message}", ex);
			}
		}

		public int AddOrEditGroup(SecurityGroupModel model)
		{
			int result = 0;
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				if (model != null)
				{
					SqlCommand cmd = new("Security_GroupAddOrEdit", connection)
					{
						CommandType = CommandType.StoredProcedure
					};

					cmd.Parameters.AddWithValue("@SecurityGroupId", model.SecurityGroupId);
					cmd.Parameters.AddWithValue("@SecurityGroupName", model.SecurityGroupName);
					cmd.Parameters.AddWithValue("@SecurityGroupDescription", model.SecurityGroupDescription);

					result = Convert.ToInt32(cmd.ExecuteScalar());
				}

				return result;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al añadir o editar el grupo de seguridad", ex);
			}
		}

		public int AddOrEditModule(SecurityModuleModel model)
		{
			int result = 0;
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();	

				if (model != null)
				{
					SqlCommand cmd = new("Security_ModuleAddOrEdit", connection)
					{
						CommandType = CommandType.StoredProcedure
					};

					cmd.Parameters.AddWithValue("@SecurityModuleId", model.SecurityModuleId);
					cmd.Parameters.AddWithValue("@SecurityModuleName", model.SecurityModuleName);
					cmd.Parameters.AddWithValue("@SecurityModuleDescription", model.SecurityModuleDescription);

					result = Convert.ToInt32(cmd.ExecuteScalar());
				}

				return result;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al añadir o editar el grupo de seguridad", ex);
			}
		}

		public int AddOrEditGroupModules(SecurityGroupModuleModel model)
		{
			int result = 0;
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				if (model != null)
				{
					SqlCommand cmd = new("Security_GroupModuleAddOrEdit", connection)
					{
						CommandType = CommandType.StoredProcedure
					};

					cmd.Parameters.AddWithValue("@SecurityGroupModuleId", model.SecurityGroupModuleId);
					cmd.Parameters.AddWithValue("@SecurityGroupId", model.SecurityGroupId);
					cmd.Parameters.AddWithValue("@SecurityModuleId", model.SecurityModuleId);
					cmd.Parameters.AddWithValue("@SecurityAccessTypeId", model.SecurityAccessTypeId);

					result = Convert.ToInt32(cmd.ExecuteScalar());
				}

				return result;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al añadir o editar el módulo del grupo", ex);
			}
		}

		public int DeleteGroupModules(int securityGroupModuleId)
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("DELETE FROM SecurityGroupModule WHERE SecurityGroupModuleId = @SecurityGroupModuleId", connection);
				cmd.Parameters.AddWithValue("@SecurityGroupModuleId", securityGroupModuleId);

				return cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				throw new Exception("Error al eliminar el módulo del grupo", ex);
			}
		}


		public int AddLogbook(int processId, bool isDeleteAction, string actionDescription)
		{
			int result = 0;
			string addEditDelete = processId == 0 ? "Agrego" : "Modifico";

			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("Security_LogbookAdd", connection)
				{
					CommandType = CommandType.StoredProcedure
				};

				var client = _clientInfoService.GetClientDetails();
				var httpContext = _httpContextAccessor.HttpContext;

				// The real session identity lives as AgentUser ("CurrentAgent") or
				// Employee ("CurrentEmployee") objects, not as loose session strings
				// — AddLogbook is called from both agent-driven actions
				// (Security.AddOrEditUser) and employee-driven ones (ticket
				// movements from the self-service wizard), so check both.
				var agent = httpContext?.Session.GetObject<AgentUser>("CurrentAgent");
				var employee = httpContext?.Session.GetObject<Employee>("CurrentEmployee");

				var userFullName = agent?.FullName ?? employee?.EmployeeName;
				var userLogin = agent?.UserName ?? employee?.EmployeeIdNumber.ToString();
				var userState = !string.IsNullOrEmpty(agent?.StateName) ? agent.StateName : employee?.StateName;
				var userId = agent?.SecurityUserId ?? 0;
				var deviceIP = ResolveClientIp(httpContext);

				if (isDeleteAction)
				{
					addEditDelete = "Elimino";
				}

				cmd.Parameters.AddWithValue("@SecurityUserId", userId);
				cmd.Parameters.AddWithValue("@DeviceIP", !string.IsNullOrEmpty(deviceIP) ? deviceIP : "Desconocida");
				cmd.Parameters.AddWithValue("@DeviceType", client.DeviceType);
				cmd.Parameters.AddWithValue("@DeviceBrowser", client.Browser);
				cmd.Parameters.AddWithValue("@DeviceOperatingSystem", client.OperatingSystem);
				cmd.Parameters.AddWithValue("@UserFullName", !string.IsNullOrEmpty(userFullName) ? userFullName : "Sistema");
				cmd.Parameters.AddWithValue("@UserLogin", !string.IsNullOrEmpty(userLogin) ? userLogin : "Sistema");
				cmd.Parameters.AddWithValue("@UserState", !string.IsNullOrEmpty(userState) ? userState : "N/D");
				cmd.Parameters.AddWithValue("@ActionDescription", $"{addEditDelete} {actionDescription}");

				result = Convert.ToInt32(cmd.ExecuteScalar());				

				return result;
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al añadir el logbook {ex.Message}", ex);
			}
		}

		// Prefers X-Forwarded-For (set by IIS/a reverse proxy in front of the
		// app) over the raw connection address, which would otherwise be the
		// proxy's own IP rather than the real client's.
		private static string? ResolveClientIp(HttpContext? httpContext)
		{
			if (httpContext is null)
			{
				return null;
			}

			var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
			if (!string.IsNullOrWhiteSpace(forwardedFor))
			{
				return forwardedFor.Split(',')[0].Trim();
			}

			return httpContext.Connection.RemoteIpAddress?.ToString();
		}

		public List<SecurityLogbookModel> GetLogbook()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM SecurityLogbook ORDER BY SecurityLogbookDate DESC", connection);

				var list = new List<SecurityLogbookModel>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new SecurityLogbookModel
					{
						SecurityLogbookId = reader.GetInt32(reader.GetOrdinal("SecurityLogbookId")),
						SecurityLogbookDate = reader.GetDateTime(reader.GetOrdinal("SecurityLogbookDate")),
						DeviceIP = reader.IsDBNull(reader.GetOrdinal("DeviceIP")) ? null : reader.GetString(reader.GetOrdinal("DeviceIP")),
						UserFullName = reader.IsDBNull(reader.GetOrdinal("UserFullName")) ? null : reader.GetString(reader.GetOrdinal("UserFullName")),
						UserLogin = reader.IsDBNull(reader.GetOrdinal("UserLogin")) ? null : reader.GetString(reader.GetOrdinal("UserLogin")),
						UserState = reader.IsDBNull(reader.GetOrdinal("UserState")) ? null : reader.GetString(reader.GetOrdinal("UserState")),
						ActionDescription = reader.IsDBNull(reader.GetOrdinal("ActionDescription")) ? null : reader.GetString(reader.GetOrdinal("ActionDescription")),
						DeviceBrowser = reader.IsDBNull(reader.GetOrdinal("DeviceBrowser")) ? null : reader.GetString(reader.GetOrdinal("DeviceBrowser")),
						DeviceOperatingSystem = reader.IsDBNull(reader.GetOrdinal("DeviceOperatingSystem")) ? null : reader.GetString(reader.GetOrdinal("DeviceOperatingSystem")),
						DeviceType = reader.IsDBNull(reader.GetOrdinal("DeviceType")) ? null : reader.GetString(reader.GetOrdinal("DeviceType")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener el logbook", ex);
			}
		}

		public SecurityUserViewModel GetValidUser(string login, string password)
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SecurityUserViewModel user = new();

				SqlCommand cmd = new("SELECT * FROM Security_GetValidUser WHERE UserName = @UserName", connection);
				cmd.Parameters.AddWithValue("@UserName", login);

				using (SqlDataReader dr = cmd.ExecuteReader())	
				{
					if (dr.Read())
					{
						var storedPassword = dr["Password"] as string;
						var decryptedPassword = string.IsNullOrEmpty(storedPassword) ? null : Decrypt(storedPassword);

						if (string.Equals(decryptedPassword, password, StringComparison.Ordinal))
						{
							user.SecurityUserId = (int)dr["SecurityUserId"];
							user.SecurityUserDocumentIdNumber = (int)dr["SecurityUserDocumentIdNumber"];
							user.FullName = (string)dr["FullName"];
							user.UserName = (string)dr["UserName"];
							user.Password = storedPassword;
							user.SecurityGroupId = (int)dr["SecurityGroupId"];
							user.SecurityGroupName = (string)dr["SecurityGroupName"];
							user.SecurityGroupDescription = (string)dr["SecurityGroupDescription"];
							user.SecurityStatusId = (int)dr["SecurityStatusId"];
							user.StateId = (int)dr["StateId"];
							user.StateName = (string)dr["StateName"];
						}
					}
				}

				return user;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener el usuario válido", ex);
			}
		}

		public List<SecurityModuleModel> GetModulesByGroupId(int groupId)
		{
			using SqlConnection connection = _connectionFactory.CreateConnection();
			connection.Open();

			SqlCommand cmd = new("SELECT * FROM SecurityGroupModule WHERE SecurityGroupId = @SecurityGroupId ORDER BY SecurityGroupModule.SecurityModuleId", connection);
			List<SecurityModuleModel> modules = new();

			cmd.Parameters.AddWithValue("@SecurityGroupId", groupId);

			using (SqlDataReader dr = cmd.ExecuteReader())
			{
				while (dr.Read())
				{
					modules.Add(new SecurityModuleModel
					{
						SecurityModuleId = (int)dr["SecurityModuleId"]
					});
				}
			}

			return modules.ToList();			
		}

		public List<SecurityModuleModel> GetAllModules()
		{
			using SqlConnection connection = _connectionFactory.CreateConnection();
			connection.Open();

			SqlCommand cmd = new("SELECT * FROM SecurityModule", connection);
			List<SecurityModuleModel> modules = new();

			using (SqlDataReader dr = cmd.ExecuteReader())
			{
				while (dr.Read())
				{
					modules.Add(new SecurityModuleModel
					{
						SecurityModuleId = (int)dr["SecurityModuleId"],
						SecurityModuleName = (string)dr["SecurityModuleName"],
						SecurityModuleDescription = (string)dr["SecurityModuleDescription"]
					});
				}
			}

			return modules.ToList();
		}

		public List<SecurityUserViewModel> GetAllUsers()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new(@"
					SELECT
						su.SecurityUserId,
						su.SecurityUserDocumentIdNumber,
						su.UserName,
						su.[Password],
						su.FullName,
						su.SecurityGroupId,
						sg.SecurityGroupName,
						su.SecurityStatusId,
						ss.SecurityStatusName,
						su.StateId,
						st.StateName
					FROM SecurityUser su
					JOIN SecurityGroup sg ON sg.SecurityGroupId = su.SecurityGroupId
					JOIN SecurityStatus ss ON ss.SecurityStatusId = su.SecurityStatusId
					JOIN State st ON st.StateId = su.StateId
					ORDER BY su.FullName", connection);

				List<SecurityUserViewModel> users = new();
				using (SqlDataReader dr = cmd.ExecuteReader())
				{
					while (dr.Read())
					{
						users.Add(new SecurityUserViewModel
						{
							SecurityUserId = (int)dr["SecurityUserId"],
							SecurityUserDocumentIdNumber = (int)dr["SecurityUserDocumentIdNumber"],
							UserName = (string)dr["UserName"],
							Password = (string)dr["Password"],
							FullName = (string)dr["FullName"],
							SecurityGroupId = (int)dr["SecurityGroupId"],
							SecurityGroupName = (string)dr["SecurityGroupName"],
							SecurityStatusId = (int)dr["SecurityStatusId"],
							SecurityStatusName = (string)dr["SecurityStatusName"],
							StateId = (int)dr["StateId"],
							StateName = (string)dr["StateName"],
						});
					}
				}

				return users;
			}
			catch (Exception ex)
			{
				throw new Exception($"Error al obtener los usuarios: {ex.Message}", ex);
			}
		}

		public List<SecurityGroupModel> GetAllGroups()
		{
			using SqlConnection connection = _connectionFactory.CreateConnection();
			connection.Open();

			SqlCommand cmd = new("SELECT * FROM SecurityGroup WHERE SecurityGroupId <> 1", connection);
			List<SecurityGroupModel> groups = new();

			using (SqlDataReader dr = cmd.ExecuteReader())
			{
				while (dr.Read())
				{
					groups.Add(new SecurityGroupModel
					{
						SecurityGroupId = (int)dr["SecurityGroupId"],
						SecurityGroupName = (string)dr["SecurityGroupName"],
						SecurityGroupDescription = (string)dr["SecurityGroupDescription"]
					});
				}
			}

			return groups.ToList();
		}

		public List<SecurityGroupModuleModel> GetAllGroupModules()
		{
			using SqlConnection connection = _connectionFactory.CreateConnection();
			connection.Open();

			SqlCommand cmd = new(@"
				SELECT
					gm.SecurityGroupModuleId,
					gm.SecurityGroupId,
					g.SecurityGroupName,
					gm.SecurityModuleId,
					m.SecurityModuleName,
					gm.SecurityAccessTypeId,
					at.SecurityAccessTypeName
				FROM SecurityGroupModule gm
				JOIN SecurityGroup g ON g.SecurityGroupId = gm.SecurityGroupId
				JOIN SecurityModule m ON m.SecurityModuleId = gm.SecurityModuleId
				JOIN SecurityAccessType at ON at.SecurityAccessTypeId = gm.SecurityAccessTypeId
				ORDER BY g.SecurityGroupName, m.SecurityModuleName", connection);

			List<SecurityGroupModuleModel> groupModules = new();
			using (SqlDataReader dr = cmd.ExecuteReader())
			{
				while (dr.Read())
				{
					groupModules.Add(new SecurityGroupModuleModel
					{
						SecurityGroupModuleId = (int)dr["SecurityGroupModuleId"],
						SecurityGroupId = (int)dr["SecurityGroupId"],
						SecurityGroupName = (string)dr["SecurityGroupName"],
						SecurityModuleId = (int)dr["SecurityModuleId"],
						SecurityModuleName = (string)dr["SecurityModuleName"],
						SecurityAccessTypeId = (int)dr["SecurityAccessTypeId"],
						SecurityAccessTypeName = (string)dr["SecurityAccessTypeName"],
					});
				}
			}

			return groupModules.ToList();
		}

		public List<SecurityAccessTypeModel> GetAllAccessTypes()
		{
			using SqlConnection connection = _connectionFactory.CreateConnection();
			connection.Open();

			SqlCommand cmd = new("SELECT * FROM SecurityAccessType", connection);
			List<SecurityAccessTypeModel> accessTypes = new();

			using (SqlDataReader dr = cmd.ExecuteReader())
			{
				while (dr.Read())
				{
					accessTypes.Add(new SecurityAccessTypeModel
					{
						SecurityAccessTypeId = (int)dr["SecurityAccessTypeId"],
						SecurityAccessTypeName = (string)dr["SecurityAccessTypeName"]
					});
				}
			}

			return accessTypes.ToList();
		}

		public List<SecurityStatusUserModel> GetAllUsersStatus()
		{
			using SqlConnection connection = _connectionFactory.CreateConnection();
			connection.Open();

			SqlCommand cmd = new("SELECT * FROM SecurityStatus WHERE SecurityStatusId < @SecurityStatusId", connection);
			cmd.Parameters.AddWithValue("@SecurityStatusId", 3);

			List<SecurityStatusUserModel> status = new();
			using (SqlDataReader dr = cmd.ExecuteReader())
			{
				while (dr.Read())
				{
					status.Add(new SecurityStatusUserModel
					{
						SecurityStatusId = (int)dr["SecurityStatusId"],
						SecurityStatusName = (string)dr["SecurityStatusName"],
					});
				}
			}

			return status.ToList();			
		}

		public bool GroupHasAccessToModule(int securityGroupId, int securityModuleId)
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Security_GetUserGroupModuleAccess WHERE SecurityGroupId = @SecurityGroupId AND SecurityModuleId = @SecurityModuleId", connection);
				cmd.Parameters.Add("@SecurityGroupId", SqlDbType.Int).Value = securityGroupId;
				cmd.Parameters.Add("@SecurityModuleId", SqlDbType.Int).Value = securityModuleId;

				using (SqlDataReader dr = cmd.ExecuteReader())
				{
					return dr.HasRows;
				}	
			}
			catch (Exception ex)
			{
				throw new Exception("Error al verificar el acceso del módulo del grupo", ex);
			}
		}



		public string? Encrypt(string plainText)
		{
			using (Aes aes = Aes.Create())
			{
				aes.Key = Encoding.UTF8.GetBytes(Key);
				aes.IV = new byte[16];

				using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
				using (var ms = new MemoryStream())
				{
					using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
					using (var writer = new StreamWriter(cs))
					{
						writer.Write(plainText);
					}
					return Convert.ToBase64String(ms.ToArray());
				}
			}
		}

		public string Decrypt(string encryptedText)
		{
			using (Aes aes = Aes.Create())
			{
				aes.Key = Encoding.UTF8.GetBytes(Key);
				aes.IV = new byte[16];

				using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
				using (var ms = new MemoryStream(Convert.FromBase64String(encryptedText)))
				using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
				using (var reader = new StreamReader(cs))
				{
					return reader.ReadToEnd();
				}
			}
		}

		public List<SecurityUserViewModel> GetAgentsForTickets()
		{
			try
			{
				using SqlConnection connection = _connectionFactory.CreateConnection();
				connection.Open();

				SqlCommand cmd = new("SELECT * FROM Security_GetAgentForTickets", connection);

				var list = new List<SecurityUserViewModel>();
				using SqlDataReader reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					list.Add(new SecurityUserViewModel
					{
						SecurityUserId = reader.GetInt32("SecurityUserId"),
						SecurityUserDocumentIdNumber = reader.GetInt32("SecurityUserDocumentIdNumber"),
						UserName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? null : reader.GetString(reader.GetOrdinal("UserName")),
						Password = reader.IsDBNull(reader.GetOrdinal("Password")) ? null : reader.GetString(reader.GetOrdinal("Password")),
						FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? null : reader.GetString(reader.GetOrdinal("FullName")),
						SecurityGroupId = reader.GetInt32("SecurityGroupId"),
						SecurityGroupName = reader.IsDBNull(reader.GetOrdinal("SecurityGroupName")) ? null : reader.GetString(reader.GetOrdinal("SecurityGroupName")),
						SecurityStatusId = reader.GetInt32("SecurityStatusId"),
						SecurityStatusName = reader.IsDBNull(reader.GetOrdinal("SecurityStatusName")) ? null : reader.GetString(reader.GetOrdinal("SecurityStatusName")),
						StateId = reader.GetInt32("StateId"),
						StateName = reader.IsDBNull(reader.GetOrdinal("StateName")) ? null : reader.GetString(reader.GetOrdinal("StateName")),
					});
				}

				return list;
			}
			catch (Exception ex)
			{
				throw new Exception("Error al obtener los agentes", ex);
			}
		}
	}
}
