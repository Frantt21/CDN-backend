using CDNBackend.API.Data;
using CDNBackend.API.Middleware;
using CDNBackend.API.Models.Dtos;
using CDNBackend.API.Models.Entities;
using CDNBackend.API.Storage;
using SharpImage = SixLabors.ImageSharp.Image;
using SharpSize = SixLabors.ImageSharp.Size;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace CDNBackend.API.Services;

public class ImageService
{
    private readonly ImagesRepository _images;
    private readonly IImageStorage _storage;
    private readonly IConfiguration _configuration;
    private readonly RealtimeService _realtime;

    public ImageService(
        ImagesRepository images,
        IImageStorage storage,
        IConfiguration configuration,
        RealtimeService realtime)
    {
        _images = images;
        _storage = storage;
        _configuration = configuration;
        _realtime = realtime;
    }

    public async Task<Image> UploadAsync(int userId, IFormFile file, string? name, string? description, string? category, CancellationToken cancellationToken)
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

        // Miniatura para los grids: evita decodificar/imponer texturas a resolución
        // completa en cada card (causa del lag en hover y al cambiar tabs).
        string? thumbnailUrl = null;
        try
        {
            stream.Position = 0;
            thumbnailUrl = await GenerateThumbnailAsync(stream, file.ContentType, cancellationToken);
        }
        catch
        {
            // sin miniatura el original sigue funcionando
        }

        var image = new Image
        {
            UserId = userId,
            Name = string.IsNullOrWhiteSpace(name) ? file.FileName : name,
            Description = description,
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            Url = url,
            ThumbnailUrl = thumbnailUrl,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        image.Id = await _images.InsertAsync(image);
        await _realtime.ImageUploadedAsync(ImageDto.From(image));
        return image;
    }

    public async Task<Image> GetByIdAsync(int id)
        => await _images.GetByIdAsync(id) ?? throw new ApiException(404, "Imagen no encontrada.");

    public async Task<IEnumerable<Image>> ListAsync(int? userId) => await _images.ListAsync(userId);

    public async Task<Stream> OpenReadAsync(string url, CancellationToken cancellationToken)
        => await _storage.OpenReadAsync(url, cancellationToken);

    /// <summary>
    /// Abre la miniatura de la imagen. Si la imagen es previa a esta feature
    /// (sin ThumbnailUrl), la genera bajo demanda, la guarda y actualiza la DB.
    /// </summary>
    public async Task<(Stream Stream, string ContentType)> OpenThumbnailAsync(Image image, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(image.ThumbnailUrl))
        {
            var thumbUrl = await GenerateThumbnailFromStoredAsync(image, cancellationToken);
            if (thumbUrl is not null)
            {
                image.ThumbnailUrl = thumbUrl;
                await _images.SetThumbnailUrlAsync(image.Id, thumbUrl);
            }
        }

        var url = string.IsNullOrWhiteSpace(image.ThumbnailUrl) ? image.Url : image.ThumbnailUrl;
        var stream = await _storage.OpenReadAsync(url, cancellationToken);
        return (stream, ContentTypeFromExtension(url));
    }

    /// <summary>Genera una miniatura a partir del stream original y la guarda en storage.</summary>
    private async Task<string?> GenerateThumbnailAsync(Stream source, string contentType, CancellationToken cancellationToken)
    {
        using var image = await SharpImage.LoadAsync(source, cancellationToken);
        var maxSide = _configuration.GetValue<int>("ImageUpload:ThumbnailMaxSize", 800);
        if (image.Width > maxSide || image.Height > maxSide)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new SharpSize(maxSide, maxSide)
            }));
        }

        var (extension, thumbContentType) = contentType.ToLowerInvariant() switch
        {
            "image/png" => (".png", "image/png"),
            "image/webp" => (".webp", "image/webp"),
            "image/gif" => (".gif", "image/gif"),
            _ => (".jpg", "image/jpeg")
        };

        await using var thumbStream = new MemoryStream();
        switch (thumbContentType)
        {
            case "image/jpeg":
                await image.SaveAsync(thumbStream, new JpegEncoder { Quality = 80 }, cancellationToken);
                break;
            case "image/webp":
                await image.SaveAsync(thumbStream, new WebpEncoder(), cancellationToken);
                break;
            case "image/gif":
                await image.SaveAsync(thumbStream, new GifEncoder(), cancellationToken);
                break;
            default:
                await image.SaveAsync(thumbStream, new PngEncoder(), cancellationToken);
                break;
        }

        thumbStream.Position = 0;
        return await _storage.UploadAsync(thumbStream, extension, thumbContentType, cancellationToken);
    }

    /// <summary>Genera la miniatura leyendo el original guardado (para imágenes sin thumbnail).</summary>
    private async Task<string?> GenerateThumbnailFromStoredAsync(Image image, CancellationToken cancellationToken)
    {
        try
        {
            await using var source = await _storage.OpenReadAsync(image.Url, cancellationToken);
            return await GenerateThumbnailAsync(source, image.ContentType, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string ContentTypeFromExtension(string url)
        => Path.GetExtension(url).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

    public async Task DeleteAsync(int id, int currentUserId, bool isAdmin)
    {
        var image = await GetByIdAsync(id);
        if (image.UserId != currentUserId && !isAdmin)
            throw new ApiException(403, "No tenés permisos para borrar esta imagen.");

        await _storage.DeleteAsync(image.Url, CancellationToken.None);
        await _images.DeleteAsync(id);
        await _realtime.ImageDeletedAsync(id);
    }
}
