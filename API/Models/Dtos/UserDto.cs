using CDNBackend.API.Models.Entities;

namespace CDNBackend.API.Models.Dtos;

public record UserDto(int Id, string Nickname, string Username, string Role, string? Description, string? AvatarUrl, string? BannerUrl, string? AvatarPosition, string? BannerPosition, DateTime CreatedAt)
{
    public static UserDto From(User user) =>
        new(user.Id, user.Nickname, user.Username, user.Role, user.Description, user.AvatarUrl, user.BannerUrl, user.AvatarPosition, user.BannerPosition, user.CreatedAt);
}

/// <summary>Resultado de la validación en vivo de disponibilidad de un username.</summary>
public record UsernameAvailabilityDto(bool Available);
