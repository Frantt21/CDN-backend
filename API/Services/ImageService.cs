using CDNBackend.API.Data;
using CDNBackend.API.Middleware;
using CDNBackend.API.Models.Entities;
using CDNBackend.API.Storage;

namespace CDNBackend.API.Services;

public class ImageService
{
    private readonly ImagesRepository _images;
    private readonly IImageStorage _storage;
    private readonly IConfiguration _configuration;

    public ImageService(ImagesRepository images, IImageStorage storage, IConfiguration configuration)
    {
        _images = images;
        _storage = storage;
        _configuration = configuration;
    }

    public async Task<Image> UploadAsync(int userId, IFormFile file, string? name, string? description, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            throw new ApiException(400, "El archivo está vacío.");

        var maxBytes = _configuration.GetValue<long>("ImageUpload:MaxSizeBytes", 10 * 1024 * 1024);
        if (file.Length > maxBytes)
            throw new ApiException(400, $"El archivo supera el tamaño máximo de {maxBytes / (1024 * 1024)} MB.");

        var allowedTypes = _configuration.GetSection("ImageUpload:AllowedContentTypes").Get<string[]>() ?? [];
        if (!allowedTypes.Contains(file.ContentType))
            throw new ApiException(400, "Tipo de archivo no permitido. Solo imágenes (jpg, png, gif, webp).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = file.OpenReadStream();
        var url = await _storage.UploadAsync(stream, extension, file.ContentType, cancellationToken);

        var image = new Image
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(name) ? file.FileName : name,
            Description = description,
            Url = url,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        image.Id = await _images.InsertAsync(image);
        return image;
    }

    public async Task<Image> GetByIdAsync(int id)
        => await _images.GetByIdAsync(id) ?? throw new ApiException(404, "Imagen no encontrada.");

    public async Task<IEnumerable<Image>> ListAsync(int? userId) => await _images.ListAsync(userId);

    public async Task<Stream> OpenReadAsync(string url, CancellationToken cancellationToken)
        => await _storage.OpenReadAsync(url, cancellationToken);

    public async Task DeleteAsync(int id, int currentUserId, bool isAdmin)
    {
        var image = await GetByIdAsync(id);
        if (image.UserId != currentUserId && !isAdmin)
            throw new ApiException(403, "No tenés permisos para borrar esta imagen.");

        await _storage.DeleteAsync(image.Url, CancellationToken.None);
        await _images.DeleteAsync(id);
    }
}
