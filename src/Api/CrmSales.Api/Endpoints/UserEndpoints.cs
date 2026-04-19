using CrmSales.Api.Services;
using CrmSales.Users.Domain.Entities;
using CrmSales.Users.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CrmSales.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/", async (IUserRepository repo, CancellationToken ct) =>
        {
            var users = await repo.GetAllAsync(ct);
            return Results.Ok(users.Select(u => new
            {
                u.Id, u.Email, u.FirstName, u.LastName, u.FullName,
                Role = u.Role.ToString(), u.IsActive
            }));
        });

        group.MapGet("/{id:guid}", async (Guid id, IUserRepository repo, CancellationToken ct) =>
        {
            var user = await repo.GetByIdAsync(id, ct);
            return user is null ? Results.NotFound() : Results.Ok(new
            {
                user.Id, user.Email, user.FirstName, user.LastName,
                user.FullName, Role = user.Role.ToString(),
                user.IsActive, user.CreatedAt
            });
        });

        group.MapPost("/", async (
            CreateUserRequest req,
            IUserRepository repo,
            KeycloakAdminClient keycloak,
            CancellationToken ct) =>
        {
            if (await repo.EmailExistsAsync(req.Email, null, ct))
                return Results.Conflict($"Email '{req.Email}' is already registered.");

            string keycloakId;
            try
            {
                keycloakId = await keycloak.CreateUserAsync(req.Email, req.FirstName, req.LastName);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Failed to create user in Keycloak: {ex.Message}",
                    statusCode: 502);
            }

            var tempPassword = Guid.NewGuid().ToString("N")[..12];
            try
            {
                await keycloak.SetTemporaryPasswordAsync(keycloakId, tempPassword);
            }
            catch
            {
                // Password set failure is non-fatal — user can be reset later
            }

            var user = User.Create(keycloakId, req.Email, req.FirstName, req.LastName, req.Role);
            await repo.AddAsync(user, ct);
            return Results.Created($"/api/users/{user.Id}", new
            {
                user.Id, user.Email, TempPassword = tempPassword
            });
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateUserRequest req,
            IUserRepository repo,
            KeycloakAdminClient keycloak,
            CancellationToken ct) =>
        {
            var user = await repo.GetByIdAsync(id, ct);
            if (user is null) return Results.NotFound();

            user.UpdateProfile(req.FirstName, req.LastName);
            if (req.Role.HasValue) user.ChangeRole(req.Role.Value);
            await repo.UpdateAsync(user, ct);

            try
            {
                await keycloak.UpdateUserAsync(user.KeycloakId, req.FirstName, req.LastName);
            }
            catch
            {
                // Local update succeeded; Keycloak sync failure is non-fatal
            }

            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            IUserRepository repo,
            KeycloakAdminClient keycloak,
            CancellationToken ct) =>
        {
            var user = await repo.GetByIdAsync(id, ct);
            if (user is null) return Results.NotFound();
            user.Deactivate();
            await repo.UpdateAsync(user, ct);

            try { await keycloak.SetEnabledAsync(user.KeycloakId, false); } catch { }

            return Results.Ok();
        });

        group.MapPost("/{id:guid}/activate", async (
            Guid id,
            IUserRepository repo,
            KeycloakAdminClient keycloak,
            CancellationToken ct) =>
        {
            var user = await repo.GetByIdAsync(id, ct);
            if (user is null) return Results.NotFound();
            user.Activate();
            await repo.UpdateAsync(user, ct);

            try { await keycloak.SetEnabledAsync(user.KeycloakId, true); } catch { }

            return Results.Ok();
        });

        return app;
    }
}

record CreateUserRequest(string Email, string FirstName, string LastName, UserRole Role);
record UpdateUserRequest(string FirstName, string LastName, UserRole? Role);
