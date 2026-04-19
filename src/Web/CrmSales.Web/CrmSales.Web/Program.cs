using CrmSales.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var keycloakBase = builder.Configuration["Keycloak:AdminUrl"] ?? "http://localhost:8080";
var keycloakAuthority = $"{keycloakBase}/realms/crm";
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7267";

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseAuthorization();

// Provides runtime config (API URL + Keycloak authority) to the WASM client
app.MapGet("/_app-config", () => Results.Json(new
{
    ApiBaseUrl = apiBaseUrl,
    KeycloakAuthority = keycloakAuthority
})).AllowAnonymous();

app.MapStaticAssets();

// AllowAnonymous on all server-mapped Blazor endpoints — auth is handled entirely in WASM
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CrmSales.Web.Client._Imports).Assembly)
    .AllowAnonymous();

app.Run();
