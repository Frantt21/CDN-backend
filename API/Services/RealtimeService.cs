using CDNBackend.API.Hubs;
using CDNBackend.API.Models.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace CDNBackend.API.Services;

/// <summary>
/// Puente entre el dominio y SignalR: empuja eventos a todos los clientes
/// conectados al <see cref="FeedHub"/>. Los nombres de los métodos (primer
/// argumento de SendAsync) son el contrato con el cliente.
/// </summary>
public class RealtimeService
{
    private readonly IHubContext<FeedHub> _hub;

    public RealtimeService(IHubContext<FeedHub> hub) => _hub = hub;

    /// <summary>Una imagen nueva fue subida y ya está disponible en el feed.</summary>
    public Task ImageUploadedAsync(ImageDto image)
        => _hub.Clients.All.SendAsync("ImageUploaded", image);

    /// <summary>Una imagen fue eliminada del feed.</summary>
    public Task ImageDeletedAsync(int imageId)
        => _hub.Clients.All.SendAsync("ImageDeleted", imageId);

    /// <summary>Se editó la metadata de una imagen (nombre, descripción o categoría).</summary>
    public Task ImageUpdatedAsync(ImageDto image)
        => _hub.Clients.All.SendAsync("ImageUpdated", image);

    /// <summary>El perfil de un usuario cambió (nickname, username, descripción o avatar).</summary>
    public Task UserUpdatedAsync(UserDto user)
        => _hub.Clients.All.SendAsync("UserUpdated", user);
}
