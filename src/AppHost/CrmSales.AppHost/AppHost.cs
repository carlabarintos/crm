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

// Keycloak — identity provider (start-dev mode, embedded H2 storage)
//
// Theme:
//   Dev  — bind-mount keycloak-themes/ so CSS edits apply on KC restart.
//   Prod — build a custom image (see keycloak-themes/Dockerfile) and set KEYCLOAK_IMAGE.
var keycloakImage = builder.Configuration["KEYCLOAK_IMAGE"] ?? "quay.io/keycloak/keycloak";
var keycloakTag   = builder.Configuration["KEYCLOAK_TAG"]   ?? "latest";

var keycloakThemesPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "keycloak-themes"));

var keycloak = builder.AddContainer("keycloak", keycloakImage, keycloakTag)
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithArgs("start-dev")
    .WithVolume("crm-keycloak-data", "/opt/keycloak/data");

// Theme: bind-mount in dev, baked into custom image in prod
if (keycloakImage == "quay.io/keycloak/keycloak")
    keycloak.WithBindMount(keycloakThemesPath, "/opt/keycloak/themes", isReadOnly: true);

var keycloakEndpoint = keycloak.GetEndpoint("http");

// ── Services ───────────────────────────────────────────────────────────────

// Web API
var api = builder.AddProject<Projects.CrmSales_Api>("crm-api")
    .WithReference(crmDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Keycloak__AdminUrl", keycloakEndpoint)
    .WaitFor(crmDb)
    .WaitFor(rabbitmq);

// Forward encryption key from host environment when set (overrides appsettings dev default).
// Production: set Encryption__Key=<base64-32-bytes> in the host or container environment.
var encryptionKey = builder.Configuration["Encryption__Key"];
if (!string.IsNullOrEmpty(encryptionKey))
    api.WithEnvironment("Encryption__Key", encryptionKey);

// Blazor WASM host — serves the WASM app and provides /_app-config.
// The browser calls the API directly; CORS is configured on the API side.
var web = builder.AddProject<Projects.CrmSales_Web>("crm-web")
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https").Property(EndpointProperty.Url))
    .WithEnvironment("Keycloak__AdminUrl", keycloakEndpoint)
    .WaitFor(api);

// Allow the WASM client (same origin as web host) to call the API directly
api.WithEnvironment("AllowedOrigins", web.GetEndpoint("https").Property(EndpointProperty.Url));

builder.Build().Run();
