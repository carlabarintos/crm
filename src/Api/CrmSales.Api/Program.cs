using CrmSales.Api.Auditing;
using CrmSales.Api.Endpoints;
using CrmSales.Api.Master;
using CrmSales.Api.Middleware;
using CrmSales.Api.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Api.Services;
using Serilog;
using System.Threading.RateLimiting;
using CrmSales.Contacts.Application;
using CrmSales.Contacts.Infrastructure;
using CrmSales.Opportunities.Application;
using CrmSales.Opportunities.Infrastructure;
using CrmSales.Orders.Application;
using CrmSales.Orders.Infrastructure;
using CrmSales.Settings.Application;
using CrmSales.Settings.Infrastructure;
using CrmSales.Products.Application;
using CrmSales.Products.Infrastructure;
using CrmSales.Products.Infrastructure.Persistence;
using CrmSales.Quotes.Application;
using CrmSales.Quotes.Infrastructure;
using CrmSales.SharedKernel.Messaging;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Users.Application;
using CrmSales.Users.Infrastructure;
using Microsoft.AspNetCore.Authentication;
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

builder.Host.UseSerilog((ctx, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "crm-api")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

    var seqUrl = ctx.Configuration["Seq:Url"];
    if (!string.IsNullOrEmpty(seqUrl))
        config.WriteTo.Seq(seqUrl);
});

builder.AddServiceDefaults();

// ── Database connection string ─────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("crm-db")
    ?? throw new InvalidOperationException("Connection string 'crm-db' not found.");


builder.Services.AddHttpContextAccessor();

// ── Keycloak Admin client ─────────────────────────────────────────────────
builder.Services.AddHttpClient<KeycloakAdminClient>();

// ── JWT Bearer authentication (Keycloak) ──────────────────────────────────
var keycloakBase = builder.Configuration["Keycloak:AdminUrl"] ?? "http://localhost:8080";
var keycloakRealm = builder.Configuration["Keycloak:Realm"] ?? "crm";
// PublicUrl is the browser-facing Keycloak URL used as JWT issuer; falls back to AdminUrl
var keycloakPublic = builder.Configuration["Keycloak:PublicUrl"] ?? keycloakBase;
var keycloakAuthority = $"{keycloakPublic}/realms/{keycloakRealm}";

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
                // Allow token via query string for SSE connections (EventSource cannot set headers)
                var queryToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(queryToken) &&
                    ctx.Request.Path.StartsWithSegments("/api/notifications/stream"))
                {
                    ctx.Token = queryToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddTransient<IClaimsTransformation, UserClaimsTransformation>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<INotificationBroadcaster, NotificationBroadcaster>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<TenantProvisioner>();
builder.Services.AddHostedService<ExpiryCheckerService>();
builder.Services.AddDbContext<MasterDbContext>(opts =>
    opts.UseNpgsql(connectionString));

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
    .AddOrdersInfrastructure(connectionString)
    .AddSettingsApplication()
    .AddSettingsInfrastructure(connectionString);

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
        .IncludeAssembly(typeof(CrmSales.Orders.Infrastructure.DependencyInjection).Assembly)
        .IncludeAssembly(typeof(CrmSales.Settings.Application.DependencyInjection).Assembly);

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
        policy.WithOrigins(
                  (builder.Configuration["AllowedOrigins"] ?? "http://localhost:5001")
                  .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Authenticated users: 300 requests per minute per user
    options.AddPolicy("authenticated", ctx =>
    {
        var user = ctx.User.FindFirst("preferred_username")?.Value
                   ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                   ?? "anonymous";
        return RateLimitPartition.GetSlidingWindowLimiter(user, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6
        });
    });

    // Global fallback: 60 requests per minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

var app = builder.Build();

// ── Ensure master schema + Companies table ────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var masterConn = masterCtx.Database.GetDbConnection();
    await masterConn.OpenAsync();
    try
    {
        await using var schemaCmd = masterConn.CreateCommand();
        schemaCmd.CommandText = "CREATE SCHEMA IF NOT EXISTS \"master\"";
        await schemaCmd.ExecuteNonQueryAsync();

        await using var checkCmd = masterConn.CreateCommand();
        checkCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'master' AND table_name = 'Companies')";
        var companiesExists = (bool)(await checkCmd.ExecuteScalarAsync())!;

        await using var auditCheckCmd = masterConn.CreateCommand();
        auditCheckCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'master' AND table_name = 'AuditLogs')";
        var auditLogsExists = (bool)(await auditCheckCmd.ExecuteScalarAsync())!;

        await using var arCheckCmd = masterConn.CreateCommand();
        arCheckCmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'master' AND table_name = 'AccessRequests')";
        var accessRequestsExists = (bool)(await arCheckCmd.ExecuteScalarAsync())!;

        if (!companiesExists)
        {
            // Fresh install — let EF Core create all master tables at once (including AccessRequests)
            app.Logger.LogInformation("master schema tables missing — creating tables.");
            await masterConn.CloseAsync();
            await masterCtx.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }
        else if (!auditLogsExists)
        {
            // Existing install being upgraded — create AuditLogs and AccessRequests
            app.Logger.LogInformation("AuditLogs table missing — creating it.");
            await using var createAuditCmd = masterConn.CreateCommand();
            createAuditCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS master."AuditLogs" (
                    "Id"          uuid         NOT NULL,
                    "TenantId"    varchar(100) NOT NULL,
                    "EventType"   varchar(100) NOT NULL,
                    "EntityType"  varchar(100) NOT NULL,
                    "EntityId"    varchar(100) NOT NULL,
                    "Description" varchar(500) NOT NULL,
                    "Actor"       varchar(200) NOT NULL,
                    "OccurredAt"  timestamp    NOT NULL,
                    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_AuditLogs_TenantId_OccurredAt"
                    ON master."AuditLogs" ("TenantId", "OccurredAt");
                CREATE TABLE IF NOT EXISTS master."AccessRequests" (
                    "Id"          uuid          NOT NULL,
                    "Name"        varchar(200)  NOT NULL,
                    "Company"     varchar(200)  NOT NULL,
                    "Email"       varchar(254)  NOT NULL,
                    "Phone"       varchar(50)   NULL,
                    "Message"     varchar(2000) NULL,
                    "Status"      varchar(20)   NOT NULL DEFAULT 'Pending',
                    "RequestedAt" timestamp     NOT NULL,
                    "ReviewedAt"  timestamp     NULL,
                    CONSTRAINT "PK_AccessRequests" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_AccessRequests_Status"
                    ON master."AccessRequests" ("Status");
                CREATE INDEX IF NOT EXISTS "IX_AccessRequests_RequestedAt"
                    ON master."AccessRequests" ("RequestedAt");
                """;
            await createAuditCmd.ExecuteNonQueryAsync();
        }
        else if (!accessRequestsExists)
        {
            // Existing install with Companies + AuditLogs — add new AccessRequests table
            app.Logger.LogInformation("AccessRequests table missing — creating it.");
            await using var createArCmd = masterConn.CreateCommand();
            createArCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS master."AccessRequests" (
                    "Id"          uuid          NOT NULL,
                    "Name"        varchar(200)  NOT NULL,
                    "Company"     varchar(200)  NOT NULL,
                    "Email"       varchar(254)  NOT NULL,
                    "Phone"       varchar(50)   NULL,
                    "Message"     varchar(2000) NULL,
                    "Status"      varchar(20)   NOT NULL DEFAULT 'Pending',
                    "RequestedAt" timestamp     NOT NULL,
                    "ReviewedAt"  timestamp     NULL,
                    CONSTRAINT "PK_AccessRequests" PRIMARY KEY ("Id")
                );
                CREATE INDEX IF NOT EXISTS "IX_AccessRequests_Status"
                    ON master."AccessRequests" ("Status");
                CREATE INDEX IF NOT EXISTS "IX_AccessRequests_RequestedAt"
                    ON master."AccessRequests" ("RequestedAt");
                """;
            await createArCmd.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (masterConn.State == System.Data.ConnectionState.Open)
            await masterConn.CloseAsync();
    }
}


// ── Ensure TaxRates table exists in every provisioned tenant schema ───────────
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        // Grab any module's connection — they all share the same connection string
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = $"""
                    SELECT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = '{slug}' AND table_name = 'TaxRates'
                    )
                    """;
                var exists = (bool)(await checkCmd.ExecuteScalarAsync())!;
                if (!exists)
                {
                    app.Logger.LogInformation("Creating TaxRates table in tenant schema '{Slug}'.", slug);
                    await using var createCmd = conn.CreateCommand();
                    createCmd.CommandText = $"""
                        CREATE TABLE IF NOT EXISTS "{slug}"."TaxRates" (
                            "Id"          uuid         NOT NULL,
                            "Version"     integer      NOT NULL DEFAULT 0,
                            "Name"        varchar(100) NOT NULL,
                            "Rate"        numeric(5,2) NOT NULL,
                            "Description" varchar(500) NULL,
                            "Region"      varchar(100) NULL,
                            "IsDefault"   boolean      NOT NULL DEFAULT false,
                            "IsActive"    boolean      NOT NULL DEFAULT true,
                            "CreatedAt"   timestamp    NOT NULL,
                            "UpdatedAt"   timestamp    NOT NULL,
                            CONSTRAINT "PK_TaxRates_{slug}" PRIMARY KEY ("Id")
                        );
                        CREATE UNIQUE INDEX IF NOT EXISTS "IX_TaxRates_Name_{slug}"
                            ON "{slug}"."TaxRates" ("Name");
                        """;
                    await createCmd.ExecuteNonQueryAsync();
                }
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}

// ── Ensure TaxRateName/TaxRatePercent columns exist on Quotes and Orders ─────
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    ALTER TABLE IF EXISTS "{slug}"."Quotes"
                        ADD COLUMN IF NOT EXISTS "TaxRateName"           varchar(100) NULL,
                        ADD COLUMN IF NOT EXISTS "TaxRatePercent"        numeric(5,2) NOT NULL DEFAULT 0,
                        ADD COLUMN IF NOT EXISTS "QuoteDiscountPercent"  numeric(5,2) NOT NULL DEFAULT 0;
                    ALTER TABLE IF EXISTS "{slug}"."Orders"
                        ADD COLUMN IF NOT EXISTS "TaxRateName"            varchar(100) NULL,
                        ADD COLUMN IF NOT EXISTS "TaxRatePercent"         numeric(5,2) NOT NULL DEFAULT 0,
                        ADD COLUMN IF NOT EXISTS "QuoteDiscountPercent"   numeric(5,2) NOT NULL DEFAULT 0;
                    ALTER TABLE IF EXISTS "{slug}"."OrderLineItems"
                        ADD COLUMN IF NOT EXISTS "DiscountPercent" numeric(5,2) NOT NULL DEFAULT 0;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}

// ── Ensure EmailTemplates and EmailSettings tables exist per tenant schema ────
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    CREATE TABLE IF NOT EXISTS "{slug}"."EmailTemplates" (
                        "Id"           uuid         NOT NULL,
                        "Version"      integer      NOT NULL DEFAULT 0,
                        "TemplateType" varchar(50)  NOT NULL,
                        "Subject"      varchar(500) NOT NULL,
                        "BodyHtml"     text         NOT NULL,
                        "IsActive"     boolean      NOT NULL DEFAULT true,
                        "UpdatedAt"    timestamp    NOT NULL,
                        CONSTRAINT "PK_EmailTemplates_{slug}" PRIMARY KEY ("Id")
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmailTemplates_TemplateType_{slug}"
                        ON "{slug}"."EmailTemplates" ("TemplateType");
                    CREATE TABLE IF NOT EXISTS "{slug}"."EmailSettings" (
                        "Id"           uuid         NOT NULL,
                        "Version"      integer      NOT NULL DEFAULT 0,
                        "Host"         varchar(500) NOT NULL DEFAULT '',
                        "Port"         integer      NOT NULL DEFAULT 587,
                        "Username"     varchar(500) NOT NULL DEFAULT '',
                        "Password"     varchar(500) NOT NULL DEFAULT '',
                        "FromName"     varchar(500) NOT NULL DEFAULT '',
                        "FromAddress"  varchar(500) NOT NULL DEFAULT '',
                        "EnableSsl"    boolean      NOT NULL DEFAULT true,
                        "IsEnabled"    boolean      NOT NULL DEFAULT false,
                        "UpdatedAt"    timestamp    NOT NULL,
                        CONSTRAINT "PK_EmailSettings_{slug}" PRIMARY KEY ("Id")
                    );
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}

// ── Ensure AuthMode column exists on EmailSettings ────────────────────────────
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    ALTER TABLE IF EXISTS "{slug}"."EmailSettings"
                        ADD COLUMN IF NOT EXISTS "AuthMode" varchar(50) NOT NULL DEFAULT 'UsernamePassword';
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}

// ── Widen Password column on EmailSettings to text (stores AES-256-GCM ciphertext) ─
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    ALTER TABLE IF EXISTS "{slug}"."EmailSettings"
                        ALTER COLUMN "Password" TYPE text;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}

// ── Ensure ReorderPoint column exists on Products ───────────────────────────
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    ALTER TABLE IF EXISTS "{slug}"."Products"
                        ADD COLUMN IF NOT EXISTS "ReorderPoint" integer NOT NULL DEFAULT 10;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}

// ── Migrate Products → CatalogItems (TPH), rename line-item columns ──────────
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '{slug}' AND table_name = 'Products')
                           AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '{slug}' AND table_name = 'CatalogItems')
                        THEN
                            ALTER TABLE "{slug}"."Products" RENAME TO "CatalogItems";
                        END IF;
                    END $$;
                    ALTER TABLE IF EXISTS "{slug}"."CatalogItems"
                        ADD COLUMN IF NOT EXISTS "Discriminator"              varchar(50)  NOT NULL DEFAULT 'Product',
                        ADD COLUMN IF NOT EXISTS "ServiceCode"                varchar(50)  NULL,
                        ADD COLUMN IF NOT EXISTS "UnitOfMeasure"              varchar(50)  NULL,
                        ADD COLUMN IF NOT EXISTS "EstimatedDurationMinutes"   integer      NULL;
                    ALTER TABLE IF EXISTS "{slug}"."CatalogItems"
                        ALTER COLUMN "Sku"           DROP NOT NULL,
                        ALTER COLUMN "StockQuantity" DROP NOT NULL,
                        ALTER COLUMN "ReorderPoint"  DROP NOT NULL;
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = '{slug}' AND table_name = 'CatalogItems' AND column_name = 'Type')
                           AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = '{slug}' AND table_name = 'CatalogItems' AND column_name = 'Discriminator')
                        THEN
                            ALTER TABLE "{slug}"."CatalogItems" RENAME COLUMN "Type" TO "Discriminator";
                        END IF;
                    END $$;
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_CatalogItems_ServiceCode_{slug}"
                        ON "{slug}"."CatalogItems"("ServiceCode") WHERE "ServiceCode" IS NOT NULL;
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = '{slug}' AND table_name = 'QuoteLineItems' AND column_name = 'ProductId') THEN
                            ALTER TABLE "{slug}"."QuoteLineItems" RENAME COLUMN "ProductId" TO "CatalogItemId";
                        END IF;
                    END $$;
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = '{slug}' AND table_name = 'QuoteLineItems' AND column_name = 'ProductName') THEN
                            ALTER TABLE "{slug}"."QuoteLineItems" RENAME COLUMN "ProductName" TO "ItemName";
                        END IF;
                    END $$;
                    ALTER TABLE IF EXISTS "{slug}"."QuoteLineItems"
                        ADD COLUMN IF NOT EXISTS "ItemType" varchar(20) NOT NULL DEFAULT 'Product';
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = '{slug}' AND table_name = 'OrderLineItems' AND column_name = 'ProductId') THEN
                            ALTER TABLE "{slug}"."OrderLineItems" RENAME COLUMN "ProductId" TO "CatalogItemId";
                        END IF;
                    END $$;
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = '{slug}' AND table_name = 'OrderLineItems' AND column_name = 'ProductName') THEN
                            ALTER TABLE "{slug}"."OrderLineItems" RENAME COLUMN "ProductName" TO "ItemName";
                        END IF;
                    END $$;
                    ALTER TABLE IF EXISTS "{slug}"."OrderLineItems"
                        ADD COLUMN IF NOT EXISTS "ItemType" varchar(20) NOT NULL DEFAULT 'Product';
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
}

// ── Ensure Roles/RolePermissions/UserRoles tables exist in every tenant schema ─
{
    using var scope = app.Services.CreateScope();
    var masterCtx = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var companySlugs = await masterCtx.Companies.Select(c => c.Slug).ToListAsync();

    if (companySlugs.Count > 0)
    {
        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            foreach (var slug in companySlugs)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    CREATE TABLE IF NOT EXISTS "{slug}"."Roles" (
                        "Id"          uuid         NOT NULL,
                        "Version"     integer      NOT NULL DEFAULT 0,
                        "Name"        varchar(100) NOT NULL,
                        "Description" varchar(500) NULL,
                        "CreatedAt"   timestamp    NOT NULL,
                        "UpdatedAt"   timestamp    NOT NULL,
                        CONSTRAINT "PK_Roles_{slug}" PRIMARY KEY ("Id")
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Roles_Name_{slug}"
                        ON "{slug}"."Roles" ("Name");

                    CREATE TABLE IF NOT EXISTS "{slug}"."RolePermissions" (
                        "Id"         uuid         NOT NULL,
                        "RoleId"     uuid         NOT NULL,
                        "Permission" varchar(100) NOT NULL,
                        CONSTRAINT "PK_RolePermissions_{slug}" PRIMARY KEY ("Id"),
                        CONSTRAINT "FK_RolePerm_Role_{slug}" FOREIGN KEY ("RoleId")
                            REFERENCES "{slug}"."Roles" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_RolePerm_RoleId_{slug}"
                        ON "{slug}"."RolePermissions" ("RoleId");

                    CREATE TABLE IF NOT EXISTS "{slug}"."UserRoles" (
                        "Id"         uuid      NOT NULL,
                        "UserId"     uuid      NOT NULL,
                        "RoleId"     uuid      NOT NULL,
                        "AssignedAt" timestamp NOT NULL,
                        CONSTRAINT "PK_UserRoles_{slug}" PRIMARY KEY ("Id"),
                        CONSTRAINT "FK_UserRoles_User_{slug}" FOREIGN KEY ("UserId")
                            REFERENCES "{slug}"."Users" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_UserRoles_Role_{slug}" FOREIGN KEY ("RoleId")
                            REFERENCES "{slug}"."Roles" ("Id") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserRoles_User_Role_{slug}"
                        ON "{slug}"."UserRoles" ("UserId", "RoleId");
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }
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
            await keycloak.EnsureSuperAdminAsync();
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
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

// ── Map endpoint groups ────────────────────────────────────────────────────
// Access requests: public submit + SuperAdmin management — registered on app directly
// so the anonymous POST bypasses the authenticated rate-limit group
app.MapAccessRequestEndpoints();

var api = app.MapGroup("/").RequireRateLimiting("authenticated");
api.MapCompanyEndpoints();
api.MapProductEndpoints();
api.MapCategoryEndpoints();
api.MapProductImportEndpoint();
api.MapServiceEndpoints();
api.MapServiceImportEndpoint();
api.MapCatalogEndpoints();
api.MapUserEndpoints();
api.MapRoleEndpoints();
api.MapUserRoleEndpoints();
api.MapContactEndpoints();
api.MapOpportunityEndpoints();
api.MapQuoteEndpoints();
api.MapOrderEndpoints();
api.MapNotificationEndpoints();
api.MapAuditEndpoints();
api.MapDashboardEndpoints();
api.MapSettingsEndpoints();

app.Run();
