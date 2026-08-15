using CDNBackend.API.Middleware;

namespace CDNBackend.API.Storage;

/// <summary>
/// Almacenamiento local en wwwroot/uploads (solo desarrollo).
/// En producción se reemplaza por el proveedor de CDN (AzureBlobStorage).
/// </summary>
public class LocalDiskStorage : IImageStorage
{
    private const string RelativePrefix = "/uploads/";
    private readonly string _uploadsPath;

    public LocalDiskStorage(IWebHostEnvironment environment)
    {
        _uploadsPath = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<string> UploadAsync(Stream stream, string extension, string contentType, CancellationToken cancellationToken)
    {
        var key = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_uploadsPath, key);

        await using var file = File.Create(path);
        await stream.CopyToAsync(file, cancellationToken);

        return RelativePrefix + key;
    }

    public Task<Stream> OpenReadAsync(string url, CancellationToken cancellationToken)
    {
        var path = GetPath(url);
        if (!File.Exists(path))
            throw new ApiException(404, "El archivo no existe.");

        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken)
    {
        var path = GetPath(url);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string GetPath(string url)
    {
        var key = url.StartsWith(RelativePrefix) ? url[RelativePrefix.Length..] : Path.GetFileName(url);
        return Path.Combine(_uploadsPath, key);
    }
}
