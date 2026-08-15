using System.Net.Mail;
using System.Text.RegularExpressions;
using CDNBackend.API.Data;
using CDNBackend.API.Middleware;
using CDNBackend.API.Models.Dtos;
using CDNBackend.API.Models.Entities;

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

    public AuthService(Database database, UsersRepository users, AuthRepository auth, PasswordHasher hasher, JwtService jwt)
    {
        _database = database;
        _users = users;
        _auth = auth;
        _hasher = hasher;
        _jwt = jwt;
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

        return new AuthResponse(user.Id, user.Nickname, user.Username, user.Role, _jwt.GenerateToken(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var credential = await _auth.GetByEmailAsync(email);
        if (credential is null || !_hasher.Verify(request.Password, credential.PasswordHash))
            throw new ApiException(401, "Email o contraseña incorrectos.");

        var user = await _users.GetByIdAsync(credential.UserId);
        if (user is null)
            throw new ApiException(401, "Email o contraseña incorrectos.");

        return new AuthResponse(user.Id, user.Nickname, user.Username, user.Role, _jwt.GenerateToken(user));
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
        return await _users.GetByIdAsync(userId) ?? throw new ApiException(404, "Usuario no encontrado.");
    }
}
