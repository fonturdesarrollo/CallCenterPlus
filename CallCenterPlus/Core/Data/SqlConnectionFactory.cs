using Microsoft.Data.SqlClient;

namespace CallCenterPlus.Core.Data
{
    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connectionString")
                ?? throw new InvalidOperationException("La cadena de conexion 'connectionString' no fue encontrada en appsettings.");
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
