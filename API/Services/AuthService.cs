using System.Net.Mail;
using System.Text.RegularExpressions;
using CDNBackend.API.Data;
using CDNBackend.API.Middleware;
using CDNBackend.API.Models.Dtos;
using CDNBackend.API.Models.Entities;
using CDNBackend.API.Storage;

namespace CDNBackend.API.Services;

public class AuthService
{
    // Username normalizado: solo letras, números y guiones bajos (en minúsculas)
    private static readonly Regex UsernameRegex = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private readonly Database _database;
    private readonly UsersRepository _users;
    private readonly AuthRepository _auth;
    private readonly PasswordHasher _hasher;
    private readonly JwtService _jwt;
    private readonly IImageStorage _storage;
    private readonly IConfiguration _configuration;
    private readonly RealtimeService _realtime;

    public AuthService(
        Database database,
        UsersRepository users,
        AuthRepository auth,
        PasswordHasher hasher,
        JwtService jwt,
        IImageStorage storage,
        IConfiguration configuration,
        RealtimeService realtime)
    {
        _database = database;
        _users = users;
        _auth = auth;
        _hasher = hasher;
        _jwt = jwt;
        _storage = storage;
        _configuration = configuration;
        _realtime = realtime;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var nickname = request.Nickname.Trim();
        var username = request.Username.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        if (nickname.Length < 2)
            throw new ApiException(400, "El nickname debe tener al menos 2 caracteres.");
        if (!UsernameRegex.IsMatch(username))
            throw new ApiException(400, "El username solo puede contener letras, números y guiones bajos.");
        if (!MailAddress.TryCreate(email, out _))
            throw new ApiException(400, "El email no es válido.");
        if (request.Password.Length < 8)
            throw new ApiException(400, "La contraseña debe tener al menos 8 caracteres.");

        if (await _users.UsernameExistsAsync(username))
            throw new ApiException(409, "Ese username ya está en uso.");
        if (await _auth.EmailExistsAsync(email))
            throw new ApiException(409, "Ese email ya está registrado.");

        var user = new User
        {
            Nickname = nickname,
            Username = username,
            Role = "user",
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        using var connection = _database.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            user.Id = await _users.InsertAsync(user, connection, transaction);
            var credential = new UserCredential
            {
                UserId = user.Id,
                Email = email,
                PasswordHash = _hasher.Hash(request.Password)
            };
            await _auth.InsertAsync(credential, connection, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return new AuthResponse(user.Id, user.Nickname, user.Username, user.Role,
            _jwt.GenerateToken(user), user.AvatarUrl, await CreateAndStoreRefreshTokenAsync(user.Id));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        var credential = await _auth.GetByEmailAsync(email);
        if (credential is null || !_hasher.Verify(request.Password, credential.PasswordHash))
            throw new ApiException(401, "Email o contraseña incorrectos.");

        var user = await _users.GetByIdAsync(credential.UserId);
        if (user is null)
            throw new ApiException(401, "Email o contraseña incorrectos.");

        return new AuthResponse(user.Id, user.Nickname, user.Username, user.Role,
            _jwt.GenerateToken(user), user.AvatarUrl, await CreateAndStoreRefreshTokenAsync(user.Id));
    }

    /// <summary>Renueva la sesión: valida el refresh token, lo rota y emite un access token nuevo.</summary>
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ApiException(400, "Falta el refresh token.");

        var stored = await _auth.GetRefreshTokenByHashAsync(_jwt.HashRefreshToken(refreshToken));
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new ApiException(401, "La sesión expiró. Volvé a iniciar sesión.");

        var user = await _users.GetByIdAsync(stored.UserId);
        if (user is null)
            throw new ApiException(401, "La sesión expiró. Volvé a iniciar sesión.");

        // Rotación: el refresh token usado queda revocado y se emite uno nuevo.
        await _auth.RevokeRefreshTokenAsync(stored.TokenHash);

        return new AuthResponse(user.Id, user.Nickname, user.Username, user.Role,
            _jwt.GenerateToken(user), user.AvatarUrl, await CreateAndStoreRefreshTokenAsync(user.Id));
    }

    private async Task<string> CreateAndStoreRefreshTokenAsync(int userId)
    {
        var token = _jwt.GenerateRefreshToken();
        var lifetimeDays = _configuration.GetValue<double>("Jwt:RefreshTokenExpiryDays", 7);
        await _auth.InsertRefreshTokenAsync(new RefreshToken
        {
            UserId = userId,
            TokenHash = _jwt.HashRefreshToken(token),
            ExpiresAt = DateTime.UtcNow.AddDays(lifetimeDays)
        });
        return token;
    }

    /// <summary>Indica si un username está disponible (regex válido y no existe en la DB, excluyendo opcionalmente al propio usuario).</summary>
    public async Task<bool> IsUsernameAvailableAsync(string username, int? excludeId = null)
    {
        var normalized = username?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!UsernameRegex.IsMatch(normalized))
            return false;
        return !await _users.UsernameExistsAsync(normalized, excludeId);
    }

    public async Task<User> UpdateProfileAsync(int userId, UpdateProfileRequest request, int currentUserId, bool isAdmin)
    {
        if (userId != currentUserId && !isAdmin)
            throw new ApiException(403, "No tenés permisos para editar este perfil.");

        var nickname = request.Nickname.Trim();
        var username = request.Username.Trim().ToLowerInvariant();

        if (nickname.Length < 2)
            throw new ApiException(400, "El nickname debe tener al menos 2 caracteres.");
        if (!UsernameRegex.IsMatch(username))
            throw new ApiException(400, "El username solo puede contener letras, números y guiones bajos.");
        if (await _users.UsernameExistsAsync(username, excludeId: userId))
            throw new ApiException(409, "Ese username ya está en uso.");

        await _users.UpdateProfileAsync(userId, nickname, username, request.Description);
        var updated = await _users.GetByIdAsync(userId) ?? throw new ApiException(404, "Usuario no encontrado.");
        await _realtime.UserUpdatedAsync(UserDto.From(updated));
        return updated;
    }

    /// <summary>Sube y asigna el avatar del usuario (solo el dueño o un admin).</summary>
    public async Task<User> UpdateAvatarAsync(
        int userId, IFormFile file, int currentUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (userId != currentUserId && !isAdmin)
            throw new ApiException(403, "No tenés permisos para editar este perfil.");
        ValidateImageFile(file);

        var user = await _users.GetByIdAsync(userId) ?? throw new ApiException(404, "Usuario no encontrado.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = file.OpenReadStream();
        var url = await _storage.UploadAsync(stream, extension, file.ContentType, cancellationToken);

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
            await _storage.DeleteAsync(user.AvatarUrl, CancellationToken.None);

        await _users.SetAvatarAsync(userId, url);
        user.AvatarUrl = url;
        await _realtime.UserUpdatedAsync(UserDto.From(user));
        return user;
    }

    /// <summary>Sube y asigna el banner del perfil (solo el dueño o un admin).</summary>
    public async Task<User> UpdateBannerAsync(
        int userId, IFormFile file, int currentUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (userId != currentUserId && !isAdmin)
            throw new ApiException(403, "No tenés permisos para editar este perfil.");
        ValidateImageFile(file);

        var user = await _users.GetByIdAsync(userId) ?? throw new ApiException(404, "Usuario no encontrado.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var stream = file.OpenReadStream();
        var url = await _storage.UploadAsync(stream, extension, file.ContentType, cancellationToken);

        if (!string.IsNullOrWhiteSpace(user.BannerUrl))
            await _storage.DeleteAsync(user.BannerUrl, CancellationToken.None);

        await _users.SetBannerAsync(userId, url);
        user.BannerUrl = url;
        await _realtime.UserUpdatedAsync(UserDto.From(user));
        return user;
    }

    /// <summary>Abre el archivo del banner del usuario (null si no tiene).</summary>
    public async Task<(Stream Stream, string ContentType)?> OpenBannerAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.BannerUrl))
            return null;

        var stream = await _storage.OpenReadAsync(user.BannerUrl, cancellationToken);
        return (stream, ContentTypeFromExtension(user.BannerUrl));
    }

    /// <summary>Valida tamaño y tipo de un archivo de imagen (avatar o banner).</summary>
    private void ValidateImageFile(IFormFile file)
    {
        if (file.Length == 0)
            throw new ApiException(400, "El archivo está vacío.");

        var maxBytes = _configuration.GetValue<long>("ImageUpload:MaxSizeBytes", 10 * 1024 * 1024);
        if (file.Length > maxBytes)
            throw new ApiException(400, $"El archivo supera el tamaño máximo de {maxBytes / (1024 * 1024)} MB.");

        var allowedTypes = _configuration.GetSection("ImageUpload:AllowedContentTypes").Get<string[]>() ?? [];
        if (!allowedTypes.Contains(file.ContentType))
            throw new ApiException(400, "Tipo de archivo no permitido. Solo imágenes (jpg, png, gif, webp).");
    }

    /// <summary>Abre el archivo del avatar del usuario (null si no tiene).</summary>
    public async Task<(Stream Stream, string ContentType)?> OpenAvatarAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.AvatarUrl))
            return null;

        var stream = await _storage.OpenReadAsync(user.AvatarUrl, cancellationToken);
        return (stream, ContentTypeFromExtension(user.AvatarUrl));
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
}
