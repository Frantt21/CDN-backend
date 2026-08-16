namespace CDNBackend.API.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    /// <summary>JSON {"x","y","zoom"} del editor de avatar.</summary>
    public string? AvatarPosition { get; set; }
    /// <summary>JSON {"x","y","zoom"} del editor de banner.</summary>
    public string? BannerPosition { get; set; }
    public DateTime CreatedAt { get; set; }
}
