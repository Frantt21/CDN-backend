using CDNBackend.API.Models.Entities;

namespace CDNBackend.API.Models.Dtos;

public record ImageDto(
    int Id,
    int UserId,
    string Name,
    string? Description,
    string Url,
    string? ThumbnailUrl,
    string ContentType,
    long? SizeBytes,
    DateTime CreatedAt)
{
    public static ImageDto From(Image image) =>
        new(image.Id, image.UserId, image.Name, image.Description, image.Url, image.ThumbnailUrl, image.ContentType, image.SizeBytes, image.CreatedAt);
}
