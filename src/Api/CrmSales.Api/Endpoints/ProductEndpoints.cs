using CrmSales.Products.Application.Products.Commands.CreateProduct;
using CrmSales.Products.Application.Products.Commands.UpdateProduct;
using CrmSales.Products.Application.Products.Queries.GetProductById;
using CrmSales.Products.Application.Products.Queries.GetProducts;
using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.SharedKernel;
using CrmSales.SharedKernel.Application;
using Microsoft.AspNetCore.Mvc;
using Wolverine;


namespace CrmSales.Api.Endpoints;

record CreateCategoryRequest(string Name, string? Description);

record ImportCategoryRow(string Name, string? Description);
record ImportProductRow(string Name, string Sku, string? Description, decimal Price, string Currency, int StockQuantity, string? CategoryName);
record ImportRowError(int Row, string Reason);
record ImportResult(int Created, int Skipped, List<ImportRowError> Errors);

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapGet("/", async (
            IMessageBus bus,
            CancellationToken ct,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool lowInventory = false,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null) =>
        {
            var result = await bus.InvokeAsync<Result<CursorPaginationResult<
                CrmSales.Products.Application.Products.DTOs.ProductDto>>>(
                new GetProductsQuery(search, isActive, lowInventory, limit, cursor), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        group.MapGet("/summary", async (IProductRepository repo, CancellationToken ct) =>
        {
            var s = await repo.GetSummaryAsync(ct);
            return Results.Ok(new
            {
                totalCount     = s.Total,
                activeCount    = s.Active,
                lowStockCount  = s.LowStock,
                outOfStockCount = s.OutOfStock,
                inventoryValue = s.InventoryValue,
                currency       = s.Currency
            });
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

        group.MapGet("/", async (
            IProductCategoryRepository repo,
            CancellationToken ct,
            [FromQuery] string? search = null,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null) =>
        {
            var result = await repo.SearchAsync(search, limit, cursor, ct);
            return Results.Ok(new { items = result.Items.Select(c => new { c.Id, c.Name, c.Description, c.IsActive }), result.NextCursor, result.HasMore });
        });

        group.MapPost("/", async (CreateCategoryRequest req, IProductCategoryRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.Problem("Category name is required.", statusCode: StatusCodes.Status400BadRequest);
            var category = ProductCategory.Create(req.Name, req.Description);
            await repo.AddAsync(category, ct);
            return Results.Created($"/api/categories/{category.Id}", new { category.Id, category.Name });
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapPost("/import", async (
            List<ImportCategoryRow> rows,
            IProductCategoryRepository repo,
            CancellationToken ct) =>
        {
            const int MaxRows = 100;
            if (rows.Count > MaxRows)
                return Results.Problem($"Import limited to {MaxRows} rows.", statusCode: StatusCodes.Status400BadRequest);

            var existing = (await repo.GetAllAsync(ct))
                .ToDictionary(c => c.Name.Trim().ToLowerInvariant());

            int created = 0, skipped = 0;
            var errors = new List<ImportRowError>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNum = i + 2; // 1-based + header row

                if (string.IsNullOrWhiteSpace(row.Name))
                {
                    errors.Add(new(rowNum, "Name is required."));
                    continue;
                }

                var key = row.Name.Trim().ToLowerInvariant();
                if (existing.ContainsKey(key))
                {
                    skipped++;
                    continue;
                }

                var category = ProductCategory.Create(row.Name.Trim(), row.Description?.Trim());
                await repo.AddAsync(category, ct);
                existing[key] = category;
                created++;
            }

            return Results.Ok(new ImportResult(created, skipped, errors));
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }

    public static IEndpointRouteBuilder MapProductImportEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products/import", async (
            List<ImportProductRow> rows,
            IProductRepository productRepo,
            IProductCategoryRepository categoryRepo,
            CancellationToken ct) =>
        {
            const int MaxRows = 200;
            if (rows.Count > MaxRows)
                return Results.Problem($"Import limited to {MaxRows} rows.", statusCode: StatusCodes.Status400BadRequest);

            var categories = (await categoryRepo.GetAllAsync(ct))
                .ToDictionary(c => c.Name.Trim().ToLowerInvariant());

            int created = 0, skipped = 0;
            var errors = new List<ImportRowError>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNum = i + 2;

                if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.Sku))
                {
                    errors.Add(new(rowNum, "Name and SKU are required."));
                    continue;
                }
                if (row.Price <= 0)
                {
                    errors.Add(new(rowNum, "Price must be greater than 0."));
                    continue;
                }

                // SKU match → skip (protects existing quotes/orders that reference this product)
                var skuUnique = await productRepo.IsSkuUniqueAsync(row.Sku, ct: ct);
                if (!skuUnique)
                {
                    skipped++;
                    continue;
                }

                // Resolve category by name
                Guid categoryId;
                if (!string.IsNullOrWhiteSpace(row.CategoryName))
                {
                    var catKey = row.CategoryName.Trim().ToLowerInvariant();
                    if (!categories.TryGetValue(catKey, out var cat))
                    {
                        // Auto-create the category so the row isn't rejected
                        cat = ProductCategory.Create(row.CategoryName.Trim());
                        await categoryRepo.AddAsync(cat, ct);
                        categories[catKey] = cat;
                    }
                    categoryId = cat.Id;
                }
                else
                {
                    // Use or create a default "Uncategorized" category
                    const string defaultName = "Uncategorized";
                    var defaultKey = defaultName.ToLowerInvariant();
                    if (!categories.TryGetValue(defaultKey, out var defCat))
                    {
                        defCat = ProductCategory.Create(defaultName);
                        await categoryRepo.AddAsync(defCat, ct);
                        categories[defaultKey] = defCat;
                    }
                    categoryId = defCat.Id;
                }

                var currency = string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency.Trim().ToUpperInvariant();
                var product = Product.Create(row.Name.Trim(), row.Description?.Trim(),
                    row.Sku.Trim(), row.Price, currency, categoryId, row.StockQuantity);
                await productRepo.AddAsync(product, ct);
                created++;
            }

            return Results.Ok(new ImportResult(created, skipped, errors));
        }).WithTags("Products").RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }
}
