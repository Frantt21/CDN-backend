using System.Data;
using CDNBackend.API.Models.Entities;
using Dapper;

namespace CDNBackend.API.Data;

public class AuthRepository
{
    private const string Columns = "Id, UserId, Email, PasswordHash";
    private readonly Database _db;

    public AuthRepository(Database db) => _db = db;

    public async Task<UserCredential?> GetByEmailAsync(string email)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<UserCredential>(
            $"SELECT {Columns} FROM UserCredentials WHERE Email = @Email", new { Email = email });
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM UserCredentials WHERE Email = @Email) THEN 1 ELSE 0 END",
            new { Email = email });
    }

    public async Task<int> InsertAsync(UserCredential credential, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO UserCredentials (UserId, Email, PasswordHash)
            VALUES (@UserId, @Email, @PasswordHash);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await connection.ExecuteScalarAsync<int>(sql, credential, transaction);
    }
}
