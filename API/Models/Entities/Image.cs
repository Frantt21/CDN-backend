namespace CDNBackend.API.Models.Entities;

public class Image
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    /// <summary>Versión reducida para grids (null en imágenes previas a esta feature).</summary>
    public string? ThumbnailUrl { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}
