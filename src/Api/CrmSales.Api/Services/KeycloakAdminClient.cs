using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrmSales.Api.Services;

public class KeycloakAdminClient(HttpClient httpClient, IConfiguration config)
{
    private readonly string _adminUrl = config["Keycloak:AdminUrl"]
        ?? config.GetConnectionString("keycloak")
        ?? "http://localhost:8080";
    private readonly string _realm = config["Keycloak:Realm"] ?? "crm";
    private readonly string _adminUsername = config["Keycloak:AdminUsername"] ?? "admin";
    private readonly string _adminPassword = config["Keycloak:AdminPassword"] ?? "admin";

    private async Task<string> GetAdminTokenAsync()
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = _adminUsername,
            ["password"] = _adminPassword
        };

        var response = await httpClient.PostAsync(
            $"{_adminUrl}/realms/master/protocol/openid-connect/token",
            new FormUrlEncodedContent(form));

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Failed to retrieve Keycloak admin access token.");
    }

    /// <summary>
    /// Ensures the CRM realm, API scope, and WASM OIDC client are configured.
    /// Safe to call on every startup — skips creation if already exists.
    /// </summary>
    public async Task EnsureRealmExistsAsync()
    {
        var token = await GetAdminTokenAsync();

        // ── Ensure realm ───────────────────────────────────────────────────
        var realmCheck = new HttpRequestMessage(HttpMethod.Get, $"{_adminUrl}/admin/realms/{_realm}");
        realmCheck.Headers.Authorization = Bearer(token);
        var realmResp = await httpClient.SendAsync(realmCheck);

        if (realmResp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var create = new HttpRequestMessage(HttpMethod.Post, $"{_adminUrl}/admin/realms");
            create.Headers.Authorization = Bearer(token);
            create.Content = JsonContent.Create(new { realm = _realm, enabled = true, displayName = "CRM Sales" });
            (await httpClient.SendAsync(create)).EnsureSuccessStatusCode();

            token = await GetAdminTokenAsync();
        }

        // ── Configure realm settings (token lifetime + custom theme) ──────
        var updateRealm = new HttpRequestMessage(HttpMethod.Put, $"{_adminUrl}/admin/realms/{_realm}");
        updateRealm.Headers.Authorization = Bearer(token);
        updateRealm.Content = JsonContent.Create(new
        {
            realm = _realm,
            displayName = "CRM Sales",
            displayNameHtml = "CRM Sales",
            loginTheme = "crm",
            accessTokenLifespan = 28800
        });
        (await httpClient.SendAsync(updateRealm)).EnsureSuccessStatusCode();

        // ── Ensure crm-web-api-scope client scope with audience mapper ─────
        var scopeId = await EnsureApiScopeAsync(token);

        // ── Ensure crm-web-app-client (public PKCE WASM client) ────────────
        await EnsureWasmClientAsync(token, scopeId);

        // ── Ensure CRM realm roles exist ───────────────────────────────────
        await EnsureRolesExistAsync(token);

        // ── Ensure company_id claim mapper ─────────────────────────────────
        await EnsureCompanyClaimMapperAsync(token);

        // ── Wait until OIDC discovery endpoint is responsive ───────────────
        var discoveryUrl = $"{_adminUrl}/realms/{_realm}/.well-known/openid-configuration";
        for (var i = 0; i < 20; i++)
        {
            try
            {
                var probe = await httpClient.GetAsync(discoveryUrl);
                if (probe.IsSuccessStatusCode) break;
            }
            catch { }
            await Task.Delay(1000);
        }
    }

    // Creates (or finds) the crm-web-api-scope client scope and returns its ID.
    // The scope carries an audience mapper that injects "crm-web-api" into access tokens.
    private async Task<string> EnsureApiScopeAsync(string token)
    {
        var listReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/client-scopes");
        listReq.Headers.Authorization = Bearer(token);
        var listResp = await httpClient.SendAsync(listReq);
        listResp.EnsureSuccessStatusCode();

        var scopes = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var scope in scopes.EnumerateArray())
        {
            if (scope.GetProperty("name").GetString() == "crm-web-api-scope")
                return scope.GetProperty("id").GetString()!;
        }

        // Create the scope
        var createReq = new HttpRequestMessage(HttpMethod.Post,
            $"{_adminUrl}/admin/realms/{_realm}/client-scopes");
        createReq.Headers.Authorization = Bearer(token);
        createReq.Content = JsonContent.Create(new
        {
            name = "crm-web-api-scope",
            protocol = "openid-connect",
            attributes = new Dictionary<string, string>
            {
                ["include.in.token.scope"] = "true"
            }
        });
        var createResp = await httpClient.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();

        // Fetch the newly created scope to get its ID
        var fetchReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/client-scopes");
        fetchReq.Headers.Authorization = Bearer(token);
        var fetchResp = await httpClient.SendAsync(fetchReq);
        fetchResp.EnsureSuccessStatusCode();
        var allScopes = await fetchResp.Content.ReadFromJsonAsync<JsonElement>();
        var scopeId = "";
        foreach (var s in allScopes.EnumerateArray())
        {
            if (s.GetProperty("name").GetString() == "crm-web-api-scope")
            {
                scopeId = s.GetProperty("id").GetString()!;
                break;
            }
        }

        // Add audience mapper so the scope injects "crm-web-api" into aud claim
        var mapperReq = new HttpRequestMessage(HttpMethod.Post,
            $"{_adminUrl}/admin/realms/{_realm}/client-scopes/{scopeId}/protocol-mappers/models");
        mapperReq.Headers.Authorization = Bearer(token);
        mapperReq.Content = JsonContent.Create(new
        {
            name = "crm-web-api-audience",
            protocol = "openid-connect",
            protocolMapper = "oidc-audience-mapper",
            config = new Dictionary<string, string>
            {
                ["included.client.audience"] = "crm-web-api",
                ["add.to.access.token"] = "true",
                ["add.to.id.token"] = "false"
            }
        });
        (await httpClient.SendAsync(mapperReq)).EnsureSuccessStatusCode();

        return scopeId;
    }

    // Creates (or updates) the crm-web-app-client public PKCE client and assigns crm-web-api-scope.
    // Always patches webOrigins so CORS works even if the client was created on a previous run.
    private async Task EnsureWasmClientAsync(string token, string scopeId)
    {
        var checkReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/clients?clientId=crm-web-app-client");
        checkReq.Headers.Authorization = Bearer(token);
        var checkResp = await httpClient.SendAsync(checkReq);
        checkResp.EnsureSuccessStatusCode();

        var clients = await checkResp.Content.ReadFromJsonAsync<JsonElement>();

        string internalId;
        if (clients.GetArrayLength() > 0)
        {
            internalId = clients[0].GetProperty("id").GetString()!;
        }
        else
        {
            // Create public PKCE client
            var createReq = new HttpRequestMessage(HttpMethod.Post,
                $"{_adminUrl}/admin/realms/{_realm}/clients");
            createReq.Headers.Authorization = Bearer(token);
            createReq.Content = JsonContent.Create(new
            {
                clientId = "crm-web-app-client",
                enabled = true,
                protocol = "openid-connect",
                publicClient = true,
                standardFlowEnabled = true,
                directAccessGrantsEnabled = false,
                redirectUris = new[] { "*" },
                webOrigins = new[] { "*" },
                attributes = new Dictionary<string, string>
                {
                    ["pkce.code.challenge.method"] = "S256",
                    ["post.logout.redirect.uris"] = "+"
                }
            });
            (await httpClient.SendAsync(createReq)).EnsureSuccessStatusCode();

            var fetchReq = new HttpRequestMessage(HttpMethod.Get,
                $"{_adminUrl}/admin/realms/{_realm}/clients?clientId=crm-web-app-client");
            fetchReq.Headers.Authorization = Bearer(token);
            var fetchResp = await httpClient.SendAsync(fetchReq);
            fetchResp.EnsureSuccessStatusCode();
            var newClients = await fetchResp.Content.ReadFromJsonAsync<JsonElement>();
            internalId = newClients[0].GetProperty("id").GetString()!;
        }

        // Always ensure webOrigins = ["*"] so CORS works regardless of when the client was created
        var patchReq = new HttpRequestMessage(HttpMethod.Put,
            $"{_adminUrl}/admin/realms/{_realm}/clients/{internalId}");
        patchReq.Headers.Authorization = Bearer(token);
        patchReq.Content = JsonContent.Create(new
        {
            clientId = "crm-web-app-client",
            enabled = true,
            publicClient = true,
            standardFlowEnabled = true,
            redirectUris = new[] { "*" },
            webOrigins = new[] { "*" },
            attributes = new Dictionary<string, string>
            {
                ["pkce.code.challenge.method"] = "S256",
                ["post.logout.redirect.uris"] = "+"
            }
        });
        (await httpClient.SendAsync(patchReq)).EnsureSuccessStatusCode();

        await AssignScopeToClientAsync(token, internalId, scopeId);
    }

    private async Task AssignScopeToClientAsync(string token, string clientId, string scopeId)
    {
        var req = new HttpRequestMessage(HttpMethod.Put,
            $"{_adminUrl}/admin/realms/{_realm}/clients/{clientId}/default-client-scopes/{scopeId}");
        req.Headers.Authorization = Bearer(token);
        await httpClient.SendAsync(req); // 204 on success, 409 if already assigned — both are fine
    }

    // Ordered highest → lowest privilege; index is used for priority comparisons.
    private static readonly string[] CrmRoles = ["SuperAdmin", "Admin"];

    private async Task EnsureRolesExistAsync(string token)
    {
        var listReq = new HttpRequestMessage(HttpMethod.Get, $"{_adminUrl}/admin/realms/{_realm}/roles");
        listReq.Headers.Authorization = Bearer(token);
        var listResp = await httpClient.SendAsync(listReq);
        listResp.EnsureSuccessStatusCode();
        var existing = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var existingNames = existing.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString())
            .ToHashSet();

        foreach (var roleName in CrmRoles)
        {
            if (existingNames.Contains(roleName)) continue;
            var createReq = new HttpRequestMessage(HttpMethod.Post, $"{_adminUrl}/admin/realms/{_realm}/roles");
            createReq.Headers.Authorization = Bearer(token);
            createReq.Content = JsonContent.Create(new { name = roleName });
            await httpClient.SendAsync(createReq);
        }
    }

    private async Task EnsureCompanyClaimMapperAsync(string token)
    {
        // Add company_id user-attribute mapper to the crm-web-api-scope so it appears in JWTs
        var listReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/client-scopes");
        listReq.Headers.Authorization = Bearer(token);
        var listResp = await httpClient.SendAsync(listReq);
        listResp.EnsureSuccessStatusCode();
        var scopes = await listResp.Content.ReadFromJsonAsync<JsonElement>();

        string? scopeId = null;
        foreach (var s in scopes.EnumerateArray())
        {
            if (s.GetProperty("name").GetString() == "crm-web-api-scope")
            {
                scopeId = s.GetProperty("id").GetString();
                break;
            }
        }
        if (scopeId is null) return;

        var mapperConfig = new
        {
            name = "company_id",
            protocol = "openid-connect",
            protocolMapper = "oidc-usermodel-attribute-mapper",
            config = new Dictionary<string, string>
            {
                ["user.attribute"] = "company_id",
                ["claim.name"] = "company_id",
                ["jsonType.label"] = "String",
                ["id.token.claim"] = "true",
                ["access.token.claim"] = "true",
                ["userinfo.token.claim"] = "true",
                ["multivalued"] = "false",
                ["aggregate.attrs"] = "false"
            }
        };

        // Check if mapper already exists — if so, update it to ensure correct config
        var mappersReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/client-scopes/{scopeId}/protocol-mappers/models");
        mappersReq.Headers.Authorization = Bearer(token);
        var mappersResp = await httpClient.SendAsync(mappersReq);
        if (mappersResp.IsSuccessStatusCode)
        {
            var mappers = await mappersResp.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var m in mappers.EnumerateArray())
            {
                if (m.GetProperty("name").GetString() == "company_id")
                {
                    var mapperId = m.GetProperty("id").GetString();
                    var updateReq = new HttpRequestMessage(HttpMethod.Put,
                        $"{_adminUrl}/admin/realms/{_realm}/client-scopes/{scopeId}/protocol-mappers/models/{mapperId}");
                    updateReq.Headers.Authorization = Bearer(token);
                    updateReq.Content = JsonContent.Create(new
                    {
                        id = mapperId,
                        mapperConfig.name,
                        mapperConfig.protocol,
                        mapperConfig.protocolMapper,
                        mapperConfig.config
                    });
                    await httpClient.SendAsync(updateReq);
                    return;
                }
            }
        }

        var createReq = new HttpRequestMessage(HttpMethod.Post,
            $"{_adminUrl}/admin/realms/{_realm}/client-scopes/{scopeId}/protocol-mappers/models");
        createReq.Headers.Authorization = Bearer(token);
        createReq.Content = JsonContent.Create(mapperConfig);
        await httpClient.SendAsync(createReq);
    }

    /// <summary>Sets the company_id attribute on a Keycloak user.</summary>
    public async Task AssignCompanyAsync(string keycloakId, string companySlug)
    {
        var token = await GetAdminTokenAsync();

        // GET the full current user representation first
        var getReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/users/{keycloakId}");
        getReq.Headers.Authorization = Bearer(token);
        var getResp = await httpClient.SendAsync(getReq);
        getResp.EnsureSuccessStatusCode();
        var user = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        // Merge existing attributes with the new company_id
        var attrs = new Dictionary<string, List<string>>();
        if (user.TryGetProperty("attributes", out var existing))
        {
            foreach (var prop in existing.EnumerateObject())
                attrs[prop.Name] = prop.Value.EnumerateArray()
                    .Select(v => v.GetString()!)
                    .ToList();
        }
        attrs["company_id"] = [companySlug];

        // PUT back the full representation with updated attributes
        var putReq = new HttpRequestMessage(HttpMethod.Put,
            $"{_adminUrl}/admin/realms/{_realm}/users/{keycloakId}");
        putReq.Headers.Authorization = Bearer(token);
        putReq.Content = JsonContent.Create(new
        {
            username    = user.TryGetProperty("username",  out var u) ? u.GetString() : null,
            email       = user.TryGetProperty("email",     out var e) ? e.GetString() : null,
            firstName   = user.TryGetProperty("firstName", out var fn) ? fn.GetString() : null,
            lastName    = user.TryGetProperty("lastName",  out var ln) ? ln.GetString() : null,
            enabled     = user.TryGetProperty("enabled",   out var en) && en.GetBoolean(),
            attributes  = attrs
        });
        (await httpClient.SendAsync(putReq)).EnsureSuccessStatusCode();
    }

    /// <summary>Creates the SuperAdmin user if it doesn't already exist.</summary>
    public async Task EnsureSuperAdminAsync()
    {
        var email = config["SuperAdmin:Email"] ?? "superadmin@crm.local";
        var password = config["SuperAdmin:Password"] ?? "SuperAdmin@123";
        var token = await GetAdminTokenAsync();

        // Check if user already exists
        var searchReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/users?email={Uri.EscapeDataString(email)}&exact=true");
        searchReq.Headers.Authorization = Bearer(token);
        var searchResp = await httpClient.SendAsync(searchReq);
        searchResp.EnsureSuccessStatusCode();
        var found = await searchResp.Content.ReadFromJsonAsync<JsonElement>();
        if (found.GetArrayLength() > 0) return;

        // Create SuperAdmin user
        var createReq = new HttpRequestMessage(HttpMethod.Post, $"{_adminUrl}/admin/realms/{_realm}/users");
        createReq.Headers.Authorization = Bearer(token);
        createReq.Content = JsonContent.Create(new
        {
            username = email,
            email,
            firstName = "Super",
            lastName = "Admin",
            enabled = true,
            attributes = new Dictionary<string, string[]> { ["company_id"] = ["master"] }
        });
        var createResp = await httpClient.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();

        var keycloakId = createResp.Headers.Location!.ToString().Split('/').Last();
        await SetTemporaryPasswordAsync(keycloakId, password);
        await AssignRoleAsync(keycloakId, "SuperAdmin");
    }

    /// <summary>Assigns a CRM realm role to a user, replacing any existing lower-priority CRM role.
    /// If the user already holds the same role or a higher-priority one, the call is a no-op.</summary>
    public async Task AssignRoleAsync(string keycloakId, string roleName)
    {
        var token = await GetAdminTokenAsync();
        var targetPriority = Array.IndexOf(CrmRoles, roleName);

        // Fetch current CRM roles
        var getReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/users/{keycloakId}/role-mappings/realm");
        getReq.Headers.Authorization = Bearer(token);
        var getResp = await httpClient.SendAsync(getReq);
        if (getResp.IsSuccessStatusCode)
        {
            var current = await getResp.Content.ReadFromJsonAsync<JsonElement>();
            var existingCrmRoles = current.EnumerateArray()
                .Where(r => CrmRoles.Contains(r.GetProperty("name").GetString()))
                .Select(r => new { id = r.GetProperty("id").GetString(), name = r.GetProperty("name").GetString() })
                .ToList();

            // Skip if user already has an equal or higher-priority role (lower index = higher priority).
            if (existingCrmRoles.Any(r => Array.IndexOf(CrmRoles, r.name) <= targetPriority))
                return;

            if (existingCrmRoles.Count > 0)
            {
                var delReq = new HttpRequestMessage(HttpMethod.Delete,
                    $"{_adminUrl}/admin/realms/{_realm}/users/{keycloakId}/role-mappings/realm");
                delReq.Headers.Authorization = Bearer(token);
                delReq.Content = JsonContent.Create(existingCrmRoles);
                await httpClient.SendAsync(delReq);
            }
        }

        // Fetch the target role representation
        var roleReq = new HttpRequestMessage(HttpMethod.Get, $"{_adminUrl}/admin/realms/{_realm}/roles/{roleName}");
        roleReq.Headers.Authorization = Bearer(token);
        var roleResp = await httpClient.SendAsync(roleReq);
        roleResp.EnsureSuccessStatusCode();
        var roleData = await roleResp.Content.ReadFromJsonAsync<JsonElement>();
        var roleId = roleData.GetProperty("id").GetString();

        // Assign the role
        var assignReq = new HttpRequestMessage(HttpMethod.Post,
            $"{_adminUrl}/admin/realms/{_realm}/users/{keycloakId}/role-mappings/realm");
        assignReq.Headers.Authorization = Bearer(token);
        assignReq.Content = JsonContent.Create(new[] { new { id = roleId, name = roleName } });
        (await httpClient.SendAsync(assignReq)).EnsureSuccessStatusCode();
    }

    /// <summary>Creates a user in Keycloak and returns the Keycloak user ID.
    /// If a user with the same email already exists, returns that user's ID.</summary>
    public async Task<string> CreateUserAsync(string email, string firstName, string lastName,
        Dictionary<string, string[]>? attributes = null)
    {
        await EnsureRealmExistsAsync();

        var req = await AuthorizedRequest(HttpMethod.Post, "users", new
        {
            username = email,
            email,
            firstName,
            lastName,
            enabled = true,
            attributes
        });

        var response = await httpClient.SendAsync(req);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // User already exists — look them up by email and return their ID.
            var token = await GetAdminTokenAsync();
            var searchReq = new HttpRequestMessage(HttpMethod.Get,
                $"{_adminUrl}/admin/realms/{_realm}/users?email={Uri.EscapeDataString(email)}&exact=true");
            searchReq.Headers.Authorization = Bearer(token);
            var searchResp = await httpClient.SendAsync(searchReq);
            searchResp.EnsureSuccessStatusCode();
            var found = await searchResp.Content.ReadFromJsonAsync<JsonElement>();
            if (found.GetArrayLength() == 0)
                throw new InvalidOperationException($"Keycloak reported conflict for '{email}' but user was not found.");
            return found[0].GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Keycloak user missing id field.");
        }

        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Keycloak did not return a Location header after user creation.");

        return location.Split('/').Last();
    }

    /// <summary>Sets a temporary password for a Keycloak user.</summary>
    public async Task SetTemporaryPasswordAsync(string keycloakId, string password)
    {
        var req = await AuthorizedRequest(HttpMethod.Put, $"users/{keycloakId}/reset-password", new
        {
            type = "password",
            value = password,
            temporary = true
        });

        (await httpClient.SendAsync(req)).EnsureSuccessStatusCode();
    }

    /// <summary>Updates first/last name of a Keycloak user.</summary>
    public async Task UpdateUserAsync(string keycloakId, string firstName, string lastName)
    {
        var req = await AuthorizedRequest(HttpMethod.Put, $"users/{keycloakId}", new
        {
            firstName,
            lastName
        });

        (await httpClient.SendAsync(req)).EnsureSuccessStatusCode();
    }

    /// <summary>Enables or disables a Keycloak user.</summary>
    public async Task SetEnabledAsync(string keycloakId, bool enabled)
    {
        var req = await AuthorizedRequest(HttpMethod.Put, $"users/{keycloakId}", new { enabled });
        (await httpClient.SendAsync(req)).EnsureSuccessStatusCode();
    }

    /// <summary>Returns all users with the Admin role whose company_id attribute matches the given slug.</summary>
    public async Task<List<KeycloakUserInfo>> GetCompanyAdminsAsync(string companySlug)
    {
        var req = await AuthorizedRequest(HttpMethod.Get, "roles/Admin/users?max=200");
        var resp = await httpClient.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return [];

        var users = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var result = new List<KeycloakUserInfo>();

        foreach (var u in users.EnumerateArray())
        {
            if (!u.TryGetProperty("attributes", out var attrs)) continue;
            if (!attrs.TryGetProperty("company_id", out var companyIds)) continue;
            if (!companyIds.EnumerateArray().Any(v => v.GetString() == companySlug)) continue;

            result.Add(new KeycloakUserInfo(
                u.GetProperty("id").GetString() ?? "",
                u.TryGetProperty("email", out var email) ? email.GetString() ?? "" : "",
                u.TryGetProperty("firstName", out var fn) ? fn.GetString() ?? "" : "",
                u.TryGetProperty("lastName", out var ln) ? ln.GetString() ?? "" : "",
                u.TryGetProperty("enabled", out var en) && en.GetBoolean()
            ));
        }

        return result;
    }

    /// <summary>Removes all CRM realm roles from a user without assigning a new one.</summary>
    public async Task RemoveCrmRolesAsync(string keycloakId)
    {
        var token = await GetAdminTokenAsync();

        var getReq = new HttpRequestMessage(HttpMethod.Get,
            $"{_adminUrl}/admin/realms/{_realm}/users/{keycloakId}/role-mappings/realm");
        getReq.Headers.Authorization = Bearer(token);
        var getResp = await httpClient.SendAsync(getReq);
        if (!getResp.IsSuccessStatusCode) return;

        var current = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        var toRemove = current.EnumerateArray()
            .Where(r => CrmRoles.Contains(r.GetProperty("name").GetString()))
            .Select(r => new { id = r.GetProperty("id").GetString(), name = r.GetProperty("name").GetString() })
            .ToList();

        if (toRemove.Count == 0) return;

        var delReq = new HttpRequestMessage(HttpMethod.Delete,
            $"{_adminUrl}/admin/realms/{_realm}/users/{keycloakId}/role-mappings/realm");
        delReq.Headers.Authorization = Bearer(token);
        delReq.Content = JsonContent.Create(toRemove);
        await httpClient.SendAsync(delReq);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, $"{_adminUrl}/admin/realms/{_realm}/{path}");
        if (body is not null)
            req.Content = JsonContent.Create(body, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        return req;
    }

    private async Task<HttpRequestMessage> AuthorizedRequest(HttpMethod method, string path, object? body = null)
    {
        var token = await GetAdminTokenAsync();
        var req = BuildRequest(method, path, body);
        req.Headers.Authorization = Bearer(token);
        return req;
    }

    private static System.Net.Http.Headers.AuthenticationHeaderValue Bearer(string token)
        => new("Bearer", token);
}

public record KeycloakUserInfo(string KeycloakId, string Email, string FirstName, string LastName, bool Enabled);
