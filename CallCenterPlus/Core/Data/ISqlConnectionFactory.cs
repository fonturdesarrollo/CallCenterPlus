using Microsoft.Data.SqlClient;

namespace CallCenterPlus.Core.Data
{
    public interface ISqlConnectionFactory
    {
        SqlConnection CreateConnection();
    }
}
