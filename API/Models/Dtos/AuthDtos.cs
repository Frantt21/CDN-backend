using System.ComponentModel.DataAnnotations;

namespace CDNBackend.API.Models.Dtos;

public record RegisterRequest(
    [Required] string Nickname,
    [Required] string Username,
    [Required] string Email,
    [Required] string Password,
    string? Description);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record AuthResponse(
    int UserId,
    string Nickname,
    string Username,
    string Role,
    string Token,
    string? AvatarUrl = null,
    string? RefreshToken = null);

public record RefreshRequest([Required] string RefreshToken);

public record UpdateProfileRequest(
    [Required] string Nickname,
    [Required] string Username,
    string? Description);
