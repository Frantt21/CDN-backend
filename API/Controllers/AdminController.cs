using CDNBackend.API.Data;
using CDNBackend.API.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDNBackend.API.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly UsersRepository _users;

    public AdminController(UsersRepository users) => _users = users;

    /// <summary>Lista todos los usuarios con su email (requiere rol admin).</summary>
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetAllUsersWithEmails()
        => Ok(await _users.GetAllWithEmailsAsync());
}
