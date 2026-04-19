using CrmSales.Web.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Net.Http.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Fetch runtime config from the server host
var bootstrapHttp = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var appConfig = await bootstrapHttp.GetFromJsonAsync<AppConfig>("/_app-config");
var apiBaseUrl = appConfig?.ApiBaseUrl ?? builder.HostEnvironment.BaseAddress;
var keycloakAuthority = appConfig?.KeycloakAuthority ?? "http://localhost:8080/realms/crm";

// OIDC authentication — PKCE code flow direct to Keycloak
builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = keycloakAuthority;
    options.ProviderOptions.ClientId = "crm-web-app-client";
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("crm-web-api-scope");
    options.AuthenticationPaths.LogOutSucceededPath = "authentication/login";
});

// HttpClient for API — AuthorizationMessageHandler attaches Bearer token automatically
builder.Services.AddHttpClient<CrmApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(
            authorizedUrls: [apiBaseUrl],
            scopes: ["crm-web-api-scope"]));

await builder.Build().RunAsync();

record AppConfig(string ApiBaseUrl, string KeycloakAuthority);
