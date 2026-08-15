namespace CDNBackend.API.Models.Entities;

public class Image
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long? SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}
