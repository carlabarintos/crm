using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;
using CrmSales.SharedKernel.Catalog;

namespace CrmSales.Products.Application.Catalog.Queries.SearchCatalog;

public record SearchCatalogQuery(
    string? Term = null,
    bool? IsActive = null,
    CatalogItemType? Type = null,
    int Limit = 20,
    string? Cursor = null) : IQuery<CursorPaginationResult<CatalogItemLookup>>;

public static class SearchCatalogHandler
{
    public static async Task<Result<CursorPaginationResult<CatalogItemLookup>>> Handle(
        SearchCatalogQuery query,
        ICatalogItemRepository catalogRepository,
        CancellationToken ct)
    {
        var result = await catalogRepository.SearchAsync(
            query.Term, query.IsActive, query.Type, query.Limit, query.Cursor, ct);
        return Result.Success(result);
    }
}
