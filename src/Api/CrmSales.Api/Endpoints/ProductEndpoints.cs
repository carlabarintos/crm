using CrmSales.Products.Application.Products.Commands.CreateProduct;
using CrmSales.Products.Application.Products.Commands.UpdateProduct;
using CrmSales.Products.Application.Products.Queries.GetProductById;
using CrmSales.Products.Application.Products.Queries.GetProducts;
using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using Microsoft.AspNetCore.Mvc;
using Wolverine;


namespace CrmSales.Api.Endpoints;

record CreateCategoryRequest(string Name, string? Description);

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<System.Collections.Generic.IReadOnlyList<
                CrmSales.Products.Application.Products.DTOs.ProductDto>>>(
                new GetProductsQuery(search, isActive), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<
                CrmSales.Products.Application.Products.DTOs.ProductDto>>(
                new GetProductByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Description);
        }).WithName("GetProductById");

        group.MapPost("/", async (CreateProductCommand cmd, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<Guid>>(cmd, ct);
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetProductById", new { id = result.Value }, result.Value)
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductCommand cmd,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest("ID mismatch.");
            var result = await bus.InvokeAsync<Result>(cmd, ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }

    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories").WithTags("Categories").RequireAuthorization();

        group.MapGet("/", async (IProductCategoryRepository repo, CancellationToken ct) =>
        {
            var cats = await repo.GetAllAsync(ct);
            return Results.Ok(cats.Select(c => new { c.Id, c.Name, c.Description, c.IsActive }));
        });

        group.MapPost("/", async (CreateCategoryRequest req, IProductCategoryRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.Problem("Category name is required.", statusCode: StatusCodes.Status400BadRequest);
            var category = ProductCategory.Create(req.Name, req.Description);
            await repo.AddAsync(category, ct);
            return Results.Created($"/api/categories/{category.Id}", new { category.Id, category.Name });
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }
}
