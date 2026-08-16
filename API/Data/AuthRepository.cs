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

    public async Task<int> InsertRefreshTokenAsync(RefreshToken token)
    {
        using var connection = _db.CreateConnection();
        const string sql = """
            INSERT INTO RefreshTokens (UserId, TokenHash, ExpiresAt)
            VALUES (@UserId, @TokenHash, @ExpiresAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await connection.ExecuteScalarAsync<int>(sql, token);
    }

    public async Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RefreshToken>(
            "SELECT Id, UserId, TokenHash, ExpiresAt, CreatedAt, RevokedAt FROM RefreshTokens WHERE TokenHash = @TokenHash",
            new { TokenHash = tokenHash });
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE TokenHash = @TokenHash",
            new { TokenHash = tokenHash });
    }
}
