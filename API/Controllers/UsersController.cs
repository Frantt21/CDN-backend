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
}
