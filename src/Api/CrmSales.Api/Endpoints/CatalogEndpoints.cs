using CrmSales.Products.Application.Catalog.Queries.SearchCatalog;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Catalog;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CrmSales.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog")
            .WithTags("Catalog")
            .RequireAuthorization();

        group.MapGet("/search", async (
            IMessageBus bus,
            CancellationToken ct,
            [FromQuery] string? q = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? type = null,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null) =>
        {
            CatalogItemType? itemType = null;
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<CatalogItemType>(type, true, out var parsed))
                itemType = parsed;

            var result = await bus.InvokeAsync<Result<CursorPaginationResult<CatalogItemLookup>>>(
                new SearchCatalogQuery(q, isActive, itemType, limit, cursor), ct);

            return result.IsSuccess
                ? Results.Ok(new
                {
                    items = result.Value.Items.Select(i => new
                    {
                        i.Id, i.Name, i.Price, i.Currency,
                        Type = i.Type.ToString(), i.IsActive
                    }),
                    result.Value.NextCursor,
                    result.Value.HasMore
                })
                : Results.Problem(result.Error.Description);
        });

        return app;
    }
}
