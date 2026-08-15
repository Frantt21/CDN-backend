namespace CDNBackend.API.Storage;

public interface IImageStorage
{
    /// <summary>Guarda el stream y devuelve la URL pública del archivo.</summary>
    Task<string> UploadAsync(Stream stream, string extension, string contentType, CancellationToken cancellationToken);

    /// <summary>Abre el archivo para lectura a partir de su URL almacenada.</summary>
    Task<Stream> OpenReadAsync(string url, CancellationToken cancellationToken);

    /// <summary>Borra el archivo a partir de su URL almacenada.</summary>
    Task DeleteAsync(string url, CancellationToken cancellationToken);
}
