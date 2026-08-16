using CDNBackend.API.Models.Entities;
using Dapper;

namespace CDNBackend.API.Data;

public class ImagesRepository
{
    private const string Columns = "Id, UserId, Name, Description, Category, Url, ThumbnailUrl, ContentType, SizeBytes, CreatedAt";
    private readonly Database _db;

    public ImagesRepository(Database db) => _db = db;

    public async Task<int> InsertAsync(Image image)
    {
        const string sql = """
            INSERT INTO Images (UserId, Name, Description, Category, Url, ThumbnailUrl, ContentType, SizeBytes, CreatedAt)
            VALUES (@UserId, @Name, @Description, @Category, @Url, @ThumbnailUrl, @ContentType, @SizeBytes, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        using var connection = _db.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, image);
    }

    public async Task<Image?> GetByIdAsync(int id)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<Image>(
            $"SELECT {Columns} FROM Images WHERE Id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<Image>> ListAsync(int? userId)
    {
        using var connection = _db.CreateConnection();
        return userId.HasValue
            ? await connection.QueryAsync<Image>(
                $"SELECT {Columns} FROM Images WHERE UserId = @UserId ORDER BY CreatedAt DESC",
                new { UserId = userId.Value })
            : await connection.QueryAsync<Image>($"SELECT {Columns} FROM Images ORDER BY CreatedAt DESC");
    }

    public async Task SetThumbnailUrlAsync(int id, string url)
    {
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Images SET ThumbnailUrl = @Url WHERE Id = @Id",
            new { Id = id, Url = url });
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = """
            DELETE FROM SavedImages WHERE ImageId = @Id;
            DELETE FROM Images WHERE Id = @Id;
            """;
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}
