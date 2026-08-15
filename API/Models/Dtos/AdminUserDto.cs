namespace CDNBackend.API.Models.Dtos;

/// <summary>Solo lo usan los endpoints admin (incluye el email de la tabla privada).</summary>
public class AdminUserDto
{
    public int Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
