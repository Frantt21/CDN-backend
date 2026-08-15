namespace CDNBackend.API.Storage;

/// <summary>
/// TODO: implementar con Azure.Storage.Blobs + CDN (producción).
/// Requiere agregar el paquete Azure.Storage.Blobs y configurar la
/// connection string en Storage:Azure dentro de appsettings.
/// </summary>
public class AzureBlobStorage : IImageStorage
{
    public Task<string> UploadAsync(Stream stream, string extension, string contentType, CancellationToken cancellationToken)
        => throw new NotImplementedException("AzureBlobStorage aún no está implementado. Usá Storage:Provider=Local en desarrollo.");

    public Task<Stream> OpenReadAsync(string url, CancellationToken cancellationToken)
        => throw new NotImplementedException("AzureBlobStorage aún no está implementado.");

    public Task DeleteAsync(string url, CancellationToken cancellationToken)
        => throw new NotImplementedException("AzureBlobStorage aún no está implementado.");
}
