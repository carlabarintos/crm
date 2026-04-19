using CrmSales.Api.Endpoints;
using CrmSales.Api.Middleware;
using CrmSales.Api.Services;
using CrmSales.Contacts.Application;
using CrmSales.Contacts.Infrastructure;
using CrmSales.Contacts.Infrastructure.Persistence;
using CrmSales.Opportunities.Application;
using CrmSales.Opportunities.Infrastructure;
using CrmSales.Opportunities.Infrastructure.Persistence;
using CrmSales.Orders.Application;
using CrmSales.Orders.Infrastructure;
using CrmSales.Orders.Infrastructure.Persistence;
using CrmSales.Products.Application;
using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.Repositories;
using CrmSales.Products.Infrastructure;
using CrmSales.Products.Infrastructure.Persistence;
using CrmSales.Quotes.Application;
using CrmSales.Quotes.Infrastructure;
using CrmSales.Quotes.Infrastructure.Persistence;
using CrmSales.SharedKernel.Messaging;
using CrmSales.Users.Application;
using CrmSales.Users.Infrastructure;
using CrmSales.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using Wolverine;
using Wolverine.RabbitMQ;

// Npgsql 6+ requires DateTimeKind.Utc for timestamptz; treat unspecified as UTC in dev
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Database connection string ─────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("crm-db")
    ?? throw new InvalidOperationException("Connection string 'crm-db' not found.");


// ── Keycloak Admin client ─────────────────────────────────────────────────
builder.Services.AddHttpClient<KeycloakAdminClient>();

// ── JWT Bearer authentication (Keycloak) ──────────────────────────────────
var keycloakBase = builder.Configuration["Keycloak:AdminUrl"] ?? "http://localhost:8080";
var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "crm";
var keycloakAuthority = $"{keycloakBase}/realms/{keycloakRealm}";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.Audience = "crm-web-api";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true,
            NameClaimType = "preferred_username"
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer")
                    .LogError("JWT auth failed: {Error}", ctx.Exception.Message);
                return Task.CompletedTask;
            },
            OnMessageReceived = ctx =>
            {
                var hasToken = !string.IsNullOrEmpty(ctx.Token) ||
                    ctx.Request.Headers.ContainsKey("Authorization");
                ctx.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer")
                    .LogInformation("JWT token present: {HasToken}", hasToken);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Module service registrations ───────────────────────────────────────────
builder.Services
    .AddProductsApplication()
    .AddProductsInfrastructure(connectionString)
    .AddUsersApplication()
    .AddUsersInfrastructure(connectionString)
    .AddContactsApplication()
    .AddContactsInfrastructure(connectionString)
    .AddOpportunitiesApplication()
    .AddOpportunitiesInfrastructure(connectionString)
    .AddQuotesApplication()
    .AddQuotesInfrastructure(connectionString)
    .AddOrdersApplication()
    .AddOrdersInfrastructure(connectionString);

// ── Wolverine ─────────────────────────────────────────────────────────────
var rabbitUri = builder.Configuration.GetConnectionString("rabbitmq")
    ?? "amqp://guest:guest@localhost:5672/";

builder.Host.UseWolverine(opts =>
{
    // ── Handler discovery — scan all module assemblies ──────────────────
    opts.Discovery
        .IncludeAssembly(typeof(CrmSales.Products.Application.DependencyInjection).Assembly)
        .IncludeAssembly(typeof(CrmSales.Users.Application.DependencyInjection).Assembly)
        .IncludeAssembly(typeof(CrmSales.Opportunities.Application.DependencyInjection).Assembly)
        .IncludeAssembly(typeof(CrmSales.Quotes.Application.DependencyInjection).Assembly)
        .IncludeAssembly(typeof(CrmSales.Orders.Application.DependencyInjection).Assembly)
        // Infrastructure handlers (RabbitMQ consumers)
        .IncludeAssembly(typeof(CrmSales.Orders.Infrastructure.DependencyInjection).Assembly);

    // ── RabbitMQ transport ─────────────────────────────────────────────
    opts.UseRabbitMq(new Uri(rabbitUri))
        .AutoProvision()           // creates exchanges/queues automatically
        .AutoPurgeOnStartup();     // clean slate in dev

    // Route: Quotes publishes → RabbitMQ → Orders consumes
    opts.PublishMessage<QuoteAcceptedMessage>()
        .ToRabbitQueue("crm.orders.quote-accepted");

    opts.ListenToRabbitQueue("crm.orders.quote-accepted")
        .PreFetchCount(10)
        .ProcessInline();          // sequential per-queue processing

});

// ── JSON options — allow enum names as strings in request/response bodies ─
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── OpenAPI ────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "CRM Sales API";
        document.Info.Version = "v1";
        document.Info.Description = "CRM Sales Journey — Products, Users, Opportunities, Quotes, Orders";
        return Task.CompletedTask;
    });
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration["AllowedOrigins"] ?? "http://localhost:5001")
              .AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Ensure all module schemas/tables exist ────────────────────────────────
// EnsureCreated uses a DB-wide HasTables check which stops after the first
// module creates its schema. Use per-schema checks instead.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await EnsureModuleTablesAsync(sp.GetRequiredService<ProductsDbContext>(), "products");
    await EnsureModuleTablesAsync(sp.GetRequiredService<UsersDbContext>(), "users");
    await EnsureModuleTablesAsync(sp.GetRequiredService<ContactsDbContext>(), "contacts");
    await EnsureModuleTablesAsync(sp.GetRequiredService<OpportunitiesDbContext>(), "opportunities");
    await EnsureModuleTablesAsync(sp.GetRequiredService<QuotesDbContext>(), "quotes");
    await EnsureModuleTablesAsync(sp.GetRequiredService<OrdersDbContext>(), "orders");

    // Add ContactId column to Opportunities if it was created before this column existed
    var oppCtx = sp.GetRequiredService<OpportunitiesDbContext>();
    var oppConn = oppCtx.Database.GetDbConnection();
    await oppConn.OpenAsync();
    await using var alterCmd = oppConn.CreateCommand();
    alterCmd.CommandText = "ALTER TABLE IF EXISTS opportunities.\"Opportunities\" ADD COLUMN IF NOT EXISTS \"ContactId\" uuid NULL";
    await alterCmd.ExecuteNonQueryAsync();
    await oppConn.CloseAsync();

    // Seed a default category so products can be created without a separate category step
    var categoryRepo = sp.GetRequiredService<IProductCategoryRepository>();
    var existing = await categoryRepo.GetAllAsync();
    if (!existing.Any())
        await categoryRepo.AddAsync(ProductCategory.Create("General", "Default product category"));
}

static async Task EnsureModuleTablesAsync(DbContext ctx, string schemaName)
{
    // Check whether the module's primary table exists. If not, create tables.
    // Also probe the schema with a real query to detect catalog corruption; if
    // that probe fails we drop and recreate the schema to recover cleanly.
    var conn = ctx.Database.GetDbConnection();
    await conn.OpenAsync();

    int count;
    try
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*)::int FROM information_schema.tables WHERE table_schema = @s AND table_type = 'BASE TABLE'";
        var p = cmd.CreateParameter();
        p.ParameterName = "s";
        p.Value = schemaName;
        cmd.Parameters.Add(p);
        count = (int)(await cmd.ExecuteScalarAsync())!;
    }
    finally
    {
        await conn.CloseAsync();
    }

    if (count > 0)
    {
        // Probe with an actual table read to detect stale OID / catalog corruption.
        // If this throws, fall through and recreate the schema.
        bool healthy = false;
        try
        {
            await conn.OpenAsync();
            await using var probe = conn.CreateCommand();
            probe.CommandText = $"SELECT 1 FROM information_schema.columns WHERE table_schema = '{schemaName}' LIMIT 1";
            await probe.ExecuteScalarAsync();
            healthy = true;
        }
        catch
        {
            // catalog corruption detected — recreate below
        }
        finally
        {
            await conn.CloseAsync();
        }

        if (healthy) return;

        // Drop the corrupted schema and recreate from scratch.
        await conn.OpenAsync();
        try
        {
            await using var drop = conn.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE; CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"";
            await drop.ExecuteNonQueryAsync();
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    await ctx.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
}

// ── Ensure Keycloak realm & OIDC client exist (blocks startup until ready) ───
{
    using var scope = app.Services.CreateScope();
    var keycloak = scope.ServiceProvider.GetRequiredService<KeycloakAdminClient>();
    for (var attempt = 1; attempt <= 20; attempt++)
    {
        try
        {
            await keycloak.EnsureRealmExistsAsync();
            app.Logger.LogInformation("Keycloak realm/client setup completed.");
            break;
        }
        catch (Exception ex)
        {
            app.Logger.LogInformation("Keycloak not ready (attempt {Attempt}/20): {Message}. Retrying in 5s...", attempt, ex.Message);
            if (attempt < 20) await Task.Delay(5000);
        }
    }
}

app.MapDefaultEndpoints();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options => options.Title = "CRM Sales API").AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ── Map endpoint groups ────────────────────────────────────────────────────
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapUserEndpoints();
app.MapContactEndpoints();
app.MapOpportunityEndpoints();
app.MapQuoteEndpoints();
app.MapOrderEndpoints();

app.Run();
