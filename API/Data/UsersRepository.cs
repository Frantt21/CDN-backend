using System.Data;
using CDNBackend.API.Models.Dtos;
using CDNBackend.API.Models.Entities;
using Dapper;

namespace CDNBackend.API.Data;

public class UsersRepository
{
    private const string Columns = "Id, Nickname, Username, Role, Description, AvatarUrl, BannerUrl, CreatedAt";
    private readonly Database _db;

    public UsersRepository(Database db) => _db = db;

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<User>($"SELECT {Columns} FROM Users ORDER BY CreatedAt DESC");
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<User>(
            $"SELECT {Columns} FROM Users WHERE Id = @Id", new { Id = id });
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<User>(
            $"SELECT {Columns} FROM Users WHERE Username = @Username", new { Username = username });
    }

    public async Task<bool> UsernameExistsAsync(string username, int? excludeId = null)
    {
        using var connection = _db.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Users WHERE Username = @Username AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 1 ELSE 0 END
            """,
            new { Username = username, ExcludeId = excludeId });
    }

    public async Task UpdateProfileAsync(int id, string nickname, string username, string? description)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Users SET Nickname = @Nickname, Username = @Username, Description = @Description WHERE Id = @Id",
            new { Id = id, Nickname = nickname, Username = username, Description = description });
    }

    public async Task SetAvatarAsync(int id, string url)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Users SET AvatarUrl = @Url WHERE Id = @Id",
            new { Id = id, Url = url });
    }

    public async Task SetBannerAsync(int id, string url)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Users SET BannerUrl = @Url WHERE Id = @Id",
            new { Id = id, Url = url });
    }

    public async Task<IEnumerable<AdminUserDto>> GetAllWithEmailsAsync()
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<AdminUserDto>(
            """
            SELECT u.Id, u.Nickname, u.Username, u.Role, u.CreatedAt, c.Email
            FROM Users u
            JOIN UserCredentials c ON c.UserId = u.Id
            ORDER BY u.CreatedAt DESC
            """);
    }

    public async Task<int> InsertAsync(User user, IDbConnection connection, IDbTransaction transaction)
    {
        const string sql = """
            INSERT INTO Users (Nickname, Username, Description, CreatedAt)
            VALUES (@Nickname, @Username, @Description, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        return await connection.ExecuteScalarAsync<int>(sql, user, transaction);
    }
}
