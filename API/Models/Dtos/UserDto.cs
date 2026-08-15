using CDNBackend.API.Models.Entities;

namespace CDNBackend.API.Models.Dtos;

public record UserDto(int Id, string Nickname, string Username, string Role, string? Description, string? AvatarUrl, DateTime CreatedAt)
{
    public static UserDto From(User user) =>
        new(user.Id, user.Nickname, user.Username, user.Role, user.Description, user.AvatarUrl, user.CreatedAt);
}
