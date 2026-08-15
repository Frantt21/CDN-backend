using CDNBackend.API.Data;
using CDNBackend.API.Middleware;
using CDNBackend.API.Models.Entities;

namespace CDNBackend.API.Services;

public class SavedImageService
{
    private readonly SavedImagesRepository _saved;
    private readonly ImagesRepository _images;

    public SavedImageService(SavedImagesRepository saved, ImagesRepository images)
    {
        _saved = saved;
        _images = images;
    }

    /// <summary>Guarda una imagen del usuario actual. Idempotente: si ya estaba guardada, no duplica.</summary>
    public async Task<bool> SaveAsync(int userId, int imageId)
    {
        var image = await _images.GetByIdAsync(imageId)
            ?? throw new ApiException(404, "Imagen no encontrada.");
        return await _saved.AddAsync(userId, image.Id);
    }

    public async Task UnsaveAsync(int userId, int imageId)
        => await _saved.DeleteAsync(userId, imageId);

    public async Task<IEnumerable<Image>> ListByUserAsync(int userId)
        => await _saved.ListByUserAsync(userId);
}
