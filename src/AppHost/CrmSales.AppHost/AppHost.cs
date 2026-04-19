var builder = DistributedApplication.CreateBuilder(args);

// ── Infrastructure ─────────────────────────────────────────────────────────

// PostgreSQL — single DB, schemas per module
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("crm-postgres-data")
    .WithPgAdmin();

var crmDb = postgres.AddDatabase("crm-db");

// RabbitMQ
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume("crm-rabbitmq-data")
    .WithManagementPlugin();

// Keycloak — identity provider (start-dev mode, admin:admin)
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "latest")
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithArgs("start-dev")
    .WithVolume("crm-keycloak-data", "/opt/keycloak/data");

var keycloakEndpoint = keycloak.GetEndpoint("http");

// ── Services ───────────────────────────────────────────────────────────────

// Web API
var api = builder.AddProject<Projects.CrmSales_Api>("crm-api")
    .WithReference(crmDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Keycloak__AdminUrl", keycloakEndpoint)
    .WaitFor(crmDb)
    .WaitFor(rabbitmq);

// Blazor WASM host — serves the WASM app and provides /_app-config.
// The browser calls the API directly; CORS is configured on the API side.
var web = builder.AddProject<Projects.CrmSales_Web>("crm-web")
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https").Property(EndpointProperty.Url))
    .WithEnvironment("Keycloak__AdminUrl", keycloakEndpoint)
    .WaitFor(api);

// Allow the WASM client (same origin as web host) to call the API directly
api.WithEnvironment("AllowedOrigins", web.GetEndpoint("https").Property(EndpointProperty.Url));

builder.Build().Run();
