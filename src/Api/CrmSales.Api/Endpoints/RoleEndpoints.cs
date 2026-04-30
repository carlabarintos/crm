using CrmSales.SharedKernel.Authorization;
using CrmSales.Users.Application.Roles.Commands.AssignRole;
using CrmSales.Users.Application.Roles.Commands.CreateRole;
using CrmSales.Users.Application.Roles.Commands.DeleteRole;
using CrmSales.Users.Application.Roles.Commands.RemoveRole;
using CrmSales.Users.Application.Roles.Commands.UpdateRole;
using CrmSales.Users.Application.Roles.Queries.GetRole;
using CrmSales.Users.Application.Roles.Queries.GetRoles;
using CrmSales.Users.Application.Roles.Queries.GetRolesForUser;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CrmSales.Api.Endpoints;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAuthorization();

        group.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result<
                System.Collections.Generic.List<CrmSales.Users.Application.Roles.DTOs.RoleDto>>>(
                new GetRolesQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        group.MapGet("/available-permissions", () =>
            Results.Ok(Permissions.All.Select(p => new { name = p })));

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result<
                CrmSales.Users.Application.Roles.DTOs.RoleDto>>(
                new GetRoleQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Description);
        });

        group.MapPost("/", async (CreateRoleRequest req, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result<Guid>>(
                new CreateRoleCommand(req.Name, req.Description, req.Permissions ?? []), ct);
            return result.IsSuccess
                ? Results.Created($"/api/roles/{result.Value}", new { id = result.Value })
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        }).RequireAuthorization(p => p.RequireClaim("permission", Permissions.ManageRoles));

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateRoleRequest req, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result>(
                new UpdateRoleCommand(id, req.Name, req.Description, req.Permissions ?? []), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireClaim("permission", Permissions.ManageRoles));

        group.MapDelete("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result>(
                new DeleteRoleCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error.Description);
        }).RequireAuthorization(p => p.RequireClaim("permission", Permissions.ManageRoles));

        return app;
    }

    public static IEndpointRouteBuilder MapUserRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(p => p.RequireClaim("permission", Permissions.ManageRoles));

        group.MapGet("/{userId:guid}/roles", async (Guid userId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result<
                System.Collections.Generic.List<CrmSales.Users.Application.Roles.DTOs.UserRoleDto>>>(
                new GetRolesForUserQuery(userId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Description);
        });

        group.MapPost("/{userId:guid}/roles", async (Guid userId, [FromBody] AssignRoleRequest req, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result>(
                new AssignRoleToUserCommand(userId, req.RoleId), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        });

        group.MapDelete("/{userId:guid}/roles/{roleId:guid}", async (Guid userId, Guid roleId, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<CrmSales.SharedKernel.Result>(
                new RemoveRoleFromUserCommand(userId, roleId), ct);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error.Description);
        });

        return app;
    }
}

record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string>? Permissions);
record UpdateRoleRequest(string Name, string? Description, IReadOnlyList<string>? Permissions);
record AssignRoleRequest(Guid RoleId);
