namespace CrmSales.Users.Application.Roles.DTOs;

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UserRoleDto(Guid RoleId, string Name, string? Description, DateTime AssignedAt);
