using System.Security.Claims;
using CDNBackend.API.Data;
using CDNBackend.API.Models.Dtos;
using CDNBackend.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDNBackend.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UsersRepository _users;
    private readonly AuthService _auth;

    public UsersController(UsersRepository users, AuthService auth)
    {
        _users = users;
        _auth = auth;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
        => Ok((await _users.GetAllAsync()).Select(UserDto.From));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById(int id)
        => await _users.GetByIdAsync(id) is { } user ? Ok(UserDto.From(user)) : NotFound();

    /// <summary>Valida en vivo si un username está disponible (para registro/edición).</summary>
    [HttpGet("check-username")]
    public async Task<ActionResult<UsernameAvailabilityDto>> CheckUsername([FromQuery] string username, [FromQuery] int? excludeId = null)
        => Ok(new UsernameAvailabilityDto(await _auth.IsUsernameAvailableAsync(username, excludeId)));

    [HttpGet("{username}")]
    public async Task<ActionResult<UserDto>> GetByUsername(string username)
        => await _users.GetByUsernameAsync(username.ToLowerInvariant()) is { } user ? Ok(UserDto.From(user)) : NotFound();

    /// <summary>Edita nickname/username/descripción (solo el dueño o un admin).</summary>
    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> UpdateProfile(int id, UpdateProfileRequest request)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var updated = await _auth.UpdateProfileAsync(id, request, currentUserId, User.IsInRole("admin"));
        return Ok(UserDto.From(updated));
    }

    /// <summary>Sube el avatar del usuario (solo el dueño o un admin).</summary>
    [Authorize]
    [HttpPost("{id:int}/avatar")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<UserDto>> UploadAvatar(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var updated = await _auth.UpdateAvatarAsync(id, file, currentUserId, User.IsInRole("admin"), cancellationToken);
        return Ok(UserDto.From(updated));
    }

    /// <summary>Devuelve el archivo del avatar (404 si el usuario no tiene uno).</summary>
    [HttpGet("{id:int}/avatar")]
    public async Task<IActionResult> GetAvatar(int id, CancellationToken cancellationToken)
    {
        var avatar = await _auth.OpenAvatarAsync(id, cancellationToken);
        return avatar is { } a ? File(a.Stream, a.ContentType) : NotFound();
    }
}
