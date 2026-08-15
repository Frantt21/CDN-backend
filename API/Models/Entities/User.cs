namespace CDNBackend.API.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
