using Microsoft.AspNetCore.SignalR;

namespace CDNBackend.API.Hubs;

/// <summary>
/// Hub de tiempo real del feed. Los clientes se conectan a /hubs/feed y
/// reciben broadcasts del servidor: imagen subida, imagen borrada o perfil
/// actualizado.
///
/// TODO (próximos pasos):
///  - Autenticar con JWT (token por query string "access_token", patrón
///    estándar de SignalR porque WebSockets no lleva headers).
///  - Agrupar por usuario (Groups.AddToGroupAsync(Context.UserIdentifier, ...))
///    para eventos privados/notificaciones por usuario.
///  - Enviar solo diffs (imagen individual) en vez de forzar refetch.
/// </summary>
public class FeedHub : Hub
{
    // Por ahora el feed es público: el servidor hace broadcast a todos los
    // clientes conectados vía RealtimeService y el hub no expone métodos.
    // Los métodos públicos de un hub son invocables desde el cliente; si algún
    // día se necesitan (ej: "unirse a mi grupo"), se agregan acá.
    public override Task OnConnectedAsync()
        => base.OnConnectedAsync();
}
