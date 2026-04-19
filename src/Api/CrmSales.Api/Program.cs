using CrmSales.Api.Endpoints;
using CrmSales.Api.Master;
using CrmSales.Api.Middleware;
using CrmSales.Api.MultiTenancy;
using CrmSales.Api.Services;
using CrmSales.Contacts.Application;
using CrmSales.Contacts.Infrastructure;
using CrmSales.Opportunities.Application;
using CrmSales.Opportunities.Infrastructure;
using CrmSales.Orders.Application;
using CrmSales.Orders.Infrastructure;
using CrmSales.Products.Application;
using CrmSales.Products.Infrastructure;
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
builder.Services.AddTransient<IClaimsTransformation, KeycloakRolesTransformer>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantProvisioner>();
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

        if (!companiesExists)
        {
            app.Logger.LogInformation("master.Companies not found — creating tables.");
            await masterConn.CloseAsync();
            await masterCtx.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }
    }
    finally
    {
        if (masterConn.State == System.Data.ConnectionState.Open)
            await masterConn.CloseAsync();
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
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

// ── Map endpoint groups ────────────────────────────────────────────────────
app.MapCompanyEndpoints();
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapUserEndpoints();
app.MapContactEndpoints();
app.MapOpportunityEndpoints();
app.MapQuoteEndpoints();
app.MapOrderEndpoints();

app.Run();
