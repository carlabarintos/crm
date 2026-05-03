using CrmSales.Api.Services;
using CrmSales.Products.Application.Products.Commands.CreateProduct;
using CrmSales.Products.Application.Products.Commands.UpdateProduct;
using CrmSales.Products.Application.Products.Queries.GetProductById;
using CrmSales.Products.Application.Products.Queries.GetProducts;
using CrmSales.Products.Application.Services.Commands.CreateService;
using CrmSales.Products.Application.Services.Commands.UpdateService;
using CrmSales.Products.Application.Services.DTOs;
using CrmSales.Products.Application.Services.Queries.GetServiceById;
using CrmSales.Products.Application.Services.Queries.GetServices;
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
record ImportServiceRow(string Name, string ServiceCode, string? Description, decimal Price, string Currency, string? UnitOfMeasure, int? EstimatedDurationMinutes, string? CategoryName);
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

        group.MapPost("/", async (CreateProductCommand cmd, IProductRepository repo, ICompanyLimitsService limits, IMessageBus bus, CancellationToken ct) =>
        {
            var companyLimits = await limits.GetForCurrentTenantAsync(ct);
            if (companyLimits?.MaxProducts is int max)
            {
                var count = await repo.CountAsync(ct);
                if (count >= max)
                    return Results.Problem($"Product limit of {max} reached for your plan.", statusCode: StatusCodes.Status429TooManyRequests);
            }
            var result = await bus.InvokeAsync<Result<Guid>>(cmd, ct);
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetProductById", new { id = result.Value }, result.Value)
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        }).RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductCommand cmd,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest("ID mismatch.");
            var result = await bus.InvokeAsync<Result>(cmd, ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

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

        group.MapPost("/", async (CreateCategoryRequest req, IProductCategoryRepository repo, ICompanyLimitsService limits, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.Problem("Category name is required.", statusCode: StatusCodes.Status400BadRequest);
            var companyLimits = await limits.GetForCurrentTenantAsync(ct);
            if (companyLimits?.MaxCategories is int max)
            {
                var count = await repo.CountAsync(ct);
                if (count >= max)
                    return Results.Problem($"Category limit of {max} reached for your plan.", statusCode: StatusCodes.Status429TooManyRequests);
            }
            var category = ProductCategory.Create(req.Name, req.Description);
            await repo.AddAsync(category, ct);
            return Results.Created($"/api/categories/{category.Id}", new { category.Id, category.Name });
        }).RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

        group.MapPost("/import", async (
            List<ImportCategoryRow> rows,
            IProductCategoryRepository repo,
            ICompanyLimitsService limits,
            CancellationToken ct) =>
        {
            const int MaxRows = 100;
            if (rows.Count > MaxRows)
                return Results.Problem($"Import limited to {MaxRows} rows.", statusCode: StatusCodes.Status400BadRequest);

            var companyLimits = await limits.GetForCurrentTenantAsync(ct);
            int? maxCategories = companyLimits?.MaxCategories;
            int currentCount = maxCategories.HasValue ? await repo.CountAsync(ct) : 0;

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

                if (maxCategories is int catMax && currentCount + created >= catMax)
                {
                    errors.Add(new(rowNum, $"Category limit of {catMax} reached for your plan."));
                    continue;
                }

                var category = ProductCategory.Create(row.Name.Trim(), row.Description?.Trim());
                await repo.AddAsync(category, ct);
                existing[key] = category;
                created++;
            }

            return Results.Ok(new ImportResult(created, skipped, errors));
        }).RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

        return app;
    }

    public static IEndpointRouteBuilder MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/services")
            .WithTags("Services")
            .RequireAuthorization();

        group.MapGet("/", async (
            IMessageBus bus,
            CancellationToken ct,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null) =>
        {
            var result = await bus.InvokeAsync<Result<CursorPaginationResult<ServiceDto>>>(
                new GetServicesQuery(search, isActive, limit, cursor), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<ServiceDto>>(new GetServiceByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Description);
        }).WithName("GetServiceById");

        group.MapPost("/", async (CreateServiceCommand cmd, IServiceRepository serviceRepo, ICompanyLimitsService limits, IMessageBus bus, CancellationToken ct) =>
        {
            var companyLimits = await limits.GetForCurrentTenantAsync(ct);
            if (companyLimits?.MaxServices is int max)
            {
                var count = await serviceRepo.CountAsync(ct);
                if (count >= max)
                    return Results.Problem($"Service limit of {max} reached for your plan.", statusCode: StatusCodes.Status429TooManyRequests);
            }
            var result = await bus.InvokeAsync<Result<Guid>>(cmd, ct);
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetServiceById", new { id = result.Value }, result.Value)
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        }).RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateServiceCommand cmd,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest("ID mismatch.");
            var result = await bus.InvokeAsync<Result>(cmd, ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

        return app;
    }

    public static IEndpointRouteBuilder MapServiceImportEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/services/import", async (
            List<ImportServiceRow> rows,
            IServiceRepository serviceRepo,
            IProductCategoryRepository categoryRepo,
            ICompanyLimitsService limits,
            CancellationToken ct) =>
        {
            const int MaxRows = 200;
            if (rows.Count > MaxRows)
                return Results.Problem($"Import limited to {MaxRows} rows.", statusCode: StatusCodes.Status400BadRequest);

            var companyLimits = await limits.GetForCurrentTenantAsync(ct);
            int? maxServices = companyLimits?.MaxServices;
            int currentCount = maxServices.HasValue ? await serviceRepo.CountAsync(ct) : 0;

            var categories = (await categoryRepo.GetAllAsync(ct))
                .ToDictionary(c => c.Name.Trim().ToLowerInvariant());

            int created = 0, skipped = 0;
            var errors = new List<ImportRowError>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNum = i + 2;

                if (string.IsNullOrWhiteSpace(row.Name) || string.IsNullOrWhiteSpace(row.ServiceCode))
                {
                    errors.Add(new(rowNum, "Name and Service Code are required."));
                    continue;
                }
                if (row.Price <= 0)
                {
                    errors.Add(new(rowNum, "Price must be greater than 0."));
                    continue;
                }

                if (maxServices is int svcMax && currentCount + created >= svcMax)
                {
                    errors.Add(new(rowNum, $"Service limit of {svcMax} reached for your plan."));
                    continue;
                }

                // ServiceCode match → skip (protects existing quotes that reference this service)
                var codeUnique = await serviceRepo.IsServiceCodeUniqueAsync(row.ServiceCode, ct: ct);
                if (!codeUnique)
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
                        cat = ProductCategory.Create(row.CategoryName.Trim());
                        await categoryRepo.AddAsync(cat, ct);
                        categories[catKey] = cat;
                    }
                    categoryId = cat.Id;
                }
                else
                {
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
                var service = Service.Create(row.Name.Trim(), row.Description?.Trim(),
                    row.ServiceCode.Trim(), row.Price, currency, categoryId,
                    row.UnitOfMeasure?.Trim(), row.EstimatedDurationMinutes);
                await serviceRepo.AddAsync(service, ct);
                created++;
            }

            return Results.Ok(new ImportResult(created, skipped, errors));
        }).WithTags("Services").RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

        return app;
    }

    public static IEndpointRouteBuilder MapProductImportEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products/import", async (
            List<ImportProductRow> rows,
            IProductRepository productRepo,
            IProductCategoryRepository categoryRepo,
            ICompanyLimitsService limits,
            CancellationToken ct) =>
        {
            const int MaxRows = 200;
            if (rows.Count > MaxRows)
                return Results.Problem($"Import limited to {MaxRows} rows.", statusCode: StatusCodes.Status400BadRequest);

            var companyLimits = await limits.GetForCurrentTenantAsync(ct);
            int? maxProducts = companyLimits?.MaxProducts;
            int currentCount = maxProducts.HasValue ? await productRepo.CountAsync(ct) : 0;

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

                if (maxProducts is int prodMax && currentCount + created >= prodMax)
                {
                    errors.Add(new(rowNum, $"Product limit of {prodMax} reached for your plan."));
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
        }).WithTags("Products").RequireAuthorization(p => p.RequireClaim("permission", CrmSales.SharedKernel.Authorization.Permissions.ManageProducts));

        return app;
    }
}
