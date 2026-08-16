using CDNBackend.API.Models.Entities;
using Dapper;

namespace CDNBackend.API.Data;

public class SavedImagesRepository
{
    private const string ImageColumns = "i.Id, i.UserId, i.Name, i.Description, i.Url, i.ThumbnailUrl, i.ContentType, i.SizeBytes, i.CreatedAt";
    private readonly Database _db;

    public SavedImagesRepository(Database db) => _db = db;

    public async Task<bool> AddAsync(int userId, int imageId)
    {
        const string sql = """
            INSERT INTO SavedImages (UserId, ImageId)
            SELECT @UserId, @ImageId
            WHERE NOT EXISTS (
                SELECT 1 FROM SavedImages WHERE UserId = @UserId AND ImageId = @ImageId
            );
            """;
        using var connection = _db.CreateConnection();
        var affected = await connection.ExecuteAsync(sql, new { UserId = userId, ImageId = imageId });
        return affected > 0;
    }

    public async Task DeleteAsync(int userId, int imageId)
    {
        const string sql = "DELETE FROM SavedImages WHERE UserId = @UserId AND ImageId = @ImageId;";
        using var connection = _db.CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, ImageId = imageId });
    }

    public async Task<IEnumerable<Image>> ListByUserAsync(int userId)
    {
        const string sql = $"""
            SELECT {ImageColumns}
            FROM SavedImages s
            JOIN Images i ON i.Id = s.ImageId
            WHERE s.UserId = @UserId
            ORDER BY s.CreatedAt DESC;
            """;
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<Image>(sql, new { UserId = userId });
    }
}
