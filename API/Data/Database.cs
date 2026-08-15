using System.Data;
using Microsoft.Data.SqlClient;

namespace CDNBackend.API.Data;

public class Database
{
    private readonly string _connectionString;

    public Database(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' no configurada.");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
