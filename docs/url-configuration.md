# Configure application URLs

Set application URLs on the server. The browser reads the public values from the UI host at startup. You do not need to edit URL settings in `wwwroot` or rebuild the browser app when a deployment address changes.

## Choose your hosting mode

| Mode | Required settings | Where to set them |
|------|-------------------|-------------------|
| Single host | `Urls:RootUiUrl` | UI.Web, which hosts both the UI and API |
| Split host | `Urls:RootUiUrl` and `Urls:ApiBaseUrl` | Both Backend and UI.Web, with matching values |

`RootUiUrl` is the public address of the root tenant. `ApiBaseUrl` is the shared public API address in split-host mode. Use complete origins, such as `https://app.example.com`: scheme, host, and optional port. Paths, credentials, query strings, and fragments are not supported. The app reports invalid settings during startup.

`TenantBaseDomain` is the DNS domain used for tenant UI addresses. Set it to `example.com` or `https://example.com` to give tenant `blue` the address `https://blue.example.com`. A bare domain inherits the root UI's scheme and port. An HTTP or HTTPS origin must match the root UI's scheme and port; for example, `http://example.com:8080` matches `http://app.example.com:8080`. Paths, query strings, fragments, and credentials are not allowed. The root UI must use this domain or one subdomain directly below it. If this setting is empty or omitted, the root UI hostname is used instead, giving `https://blue.app.example.com`.

Single-host browser requests use the current page's origin, including its tenant subdomain. Omit `ApiBaseUrl` in single-host mode; startup rejects it to avoid conflicting configuration.

## Local development

The generated Development settings already contain the standard local addresses:

| Setting | Single host | Split host |
|---------|-------------|------------|
| `Urls:RootUiUrl` | `https://localhost:7291` | `https://localhost:7291` |
| `Urls:ApiBaseUrl` | Omitted | `https://localhost:5199` |
| `Urls:TenantBaseDomain` | Empty string | Empty string |

Development sets `TenantBaseDomain` to an empty string to override the production sample. IP addresses and `localhost` do not receive tenant prefixes.

Start the Aspire AppHost as described in the [README](../README.md). Aspire supplies the server connection address automatically. You do not need to configure `services:api:*` yourself.

If you change a local port, update the public URL and the matching launch profile. Public URL settings do not change listening ports. Use `launchSettings.json`, `ASPNETCORE_URLS`, or your hosting platform to set those ports.

## Deployment

Replace the `example.com` sample addresses with your own domains. Use HTTPS for public deployments. Set environment variables on the application processes, or use your platform's configuration system. A double underscore separates configuration sections in environment variable names.

For **single host**, set these on UI.Web:

```text
Urls__RootUiUrl=https://app.example.com
Urls__TenantBaseDomain=example.com
```

For **split host**, set these on both Backend and UI.Web:

```text
Urls__RootUiUrl=https://app.example.com
Urls__TenantBaseDomain=example.com
Urls__ApiBaseUrl=https://api.example.com
```

The same values can be set in each host's `appsettings.json` under the `Urls` section. Keep the two split-host configurations in sync through shared deployment variables. Generated single-host projects have no Backend settings files; UI.Web owns all server settings.

When running without Aspire, these public settings are enough for URL resolution if UI.Web can reach the API at its public address. Database, authentication, and other infrastructure settings are still required.

### Internal server connections

Normally, leave `Urls:ServerApiBaseUrl` unset. Set it on both Backend and UI.Web in split-host mode, or on UI.Web in single-host mode, when the server must connect through a different address, such as a private network behind a reverse proxy:

```text
# Split host: private Backend address
Urls__ServerApiBaseUrl=http://backend:8080

# Single host: combined app's local listening address
Urls__ServerApiBaseUrl=http://localhost:8080
```

Use only the example for your hosting mode, and use a reachable address. Keep `RootUiUrl` and `ApiBaseUrl` set to their public HTTPS addresses even when the internal connection uses HTTP.

UI.Web selects its server API address in this order:

1. Explicit `Urls:ServerApiBaseUrl`.
2. In split-host mode, the Aspire API HTTPS endpoint, then its HTTP endpoint.
3. `Urls:ApiBaseUrl` for split host, or `Urls:RootUiUrl` for single host.

In split-host mode, the matching Backend setting also marks that hostname as a connection endpoint. This prevents an internal hostname under the root domain from being mistaken for a tenant.

The single-host AppHost supplies `Urls:ServerApiBaseUrl` with the app's own endpoint. Server requests carry the tenant identifier in a header, so the internal hostname needs no tenant prefix.

The anonymous `/configuration/urls` endpoint returns public URL settings, including `TenantBaseDomain` and public reserved tenant identifiers, with `Cache-Control: no-store`. Internal server addresses and secrets are excluded. Keep this endpoint available to the browser when configuring a reverse proxy.

## Tenant domains and CORS

With multiple tenants enabled and `TenantBaseDomain=example.com`, tenant `blue` uses:

| Mode | UI address | Browser API address |
|------|------------|---------------------|
| Single host | `https://blue.example.com` | `https://blue.example.com` |
| Split host | `https://blue.example.com` | `https://api.example.com` |

The root tenant stays at `https://app.example.com`. A tenant is one DNS label before `TenantBaseDomain`. In split-host mode, all browser tenants use the exact shared `ApiBaseUrl`; the UI sends `x-tenant-identifier` with each API request. Server calls also send this header and use the internal server address. The Backend must enforce the user's access to the selected tenant; a hostname or header alone does not grant access.

For the Krafter Portainer deployment, set matching values on Backend and UI.Web:

```text
Urls__RootUiUrl=https://krafter.getkrafter.dev
Urls__TenantBaseDomain=getkrafter.dev
Urls__ApiBaseUrl=https://api.getkrafter.dev
Urls__ServerApiBaseUrl=http://backend_krafter:8080
```

This gives tenant `blue` the UI address `https://blue.getkrafter.dev`. Google login still uses the fixed root callback, `https://krafter.getkrafter.dev/google-callback`.

### Tenant identifiers

Tenant URL identifiers are separate from display names. Identifiers must contain 1–10 lowercase ASCII letters, numbers, or hyphens. They must start and end with a letter or number. Create and update operations check uniqueness without regard to letter case.

`root`, `api`, `www`, and `mail` are reserved. The root UI hostname's tenant label is also reserved automatically, such as `app` or `krafter`. API and internal server hostname labels under the tenant base domain are reserved too. Add service names specific to your deployment through `Urls:ReservedTenantIdentifiers`:

```json
"ReservedTenantIdentifiers": ["status", "support"]
```

For environment variables, use `Urls__ReservedTenantIdentifiers__0=status` and `Urls__ReservedTenantIdentifiers__1=support`. Keep this configuration identical on both split hosts. Backend validation is authoritative; the UI checks the public rules before submitting. Internal server addresses and their derived reserved labels stay on the server, so an internal-name conflict can appear only after submission. Unknown tenant identifiers return HTTP 404 instead of falling back to the root tenant. The existing root tenant remains available at `RootUiUrl`.

### Browser origins

In split-host mode, CORS controls which browser origins can call Backend. Backend allows the exact `RootUiUrl` origin automatically. The template also sets:

```json
"Cors": {
  "AllowTenantSubdomains": true,
  "AllowedOrigins": []
}
```

`AllowTenantSubdomains` allows one valid, non-reserved tenant label under `TenantBaseDomain` (or the root UI hostname when unset), with the root UI's scheme and port. It does not allow unrelated domains, nested subdomains, or a different port. Set it to `false` if your app does not use tenant subdomains. This rule does not apply to IP or localhost base addresses. CORS checks an origin's form, not whether the tenant exists; tenant resolution performs that check.

Add other trusted browser origins only when needed, as full origins in `Cors:AllowedOrigins`. For example, `https://admin.example.com`. The environment variable for the first entry is `Cors__AllowedOrigins__0`. Wildcards and comma-separated hostname lists are not supported. Development uses the same origin checks as production. Its settings also allow `http://localhost:5116` for the UI HTTP launch profile.

Single-host browser calls are same-origin and need no separate API CORS configuration.

## Google login

Both login initiation and Backend's code exchange derive the callback from `Urls:RootUiUrl` plus `/google-callback`. Configure this exact redirect URI in your Google OAuth client:

- Local: `https://localhost:7291/google-callback`
- Deployment example: `https://app.example.com/google-callback`

Tenant login also returns through this root UI callback. Keep the root UI setting identical in Backend and UI.Web for split host. Google client ID and client secret settings remain separate; keep the secret on the server.

## Move from the old settings

| Old setting | Replacement |
|-------------|-------------|
| `RootUiUrl` | `Urls:RootUiUrl` on the server |
| `RemoteHostUrl` | Split host: `Urls:ApiBaseUrl`; single host: remove it |
| Internal-only `RemoteHostUrl` override | Optional `Urls:ServerApiBaseUrl` on both split hosts, or UI.Web in single-host mode |
| Manually configured `services:api:https:0` | `Urls:ApiBaseUrl`, or `Urls:ServerApiBaseUrl` for an internal override; Aspire still supplies its own service keys |
| `Authentication:Google:RedirectUri` | Remove it; callback is derived from `Urls:RootUiUrl` |
| `AllowedCorsDomains` | Root UI is automatic; use `Cors:AllowTenantSubdomains` and `Cors:AllowedOrigins` for additional access |
| URL settings in browser `wwwroot/appsettings*.json` | Remove them; the browser loads the UI host's public URL settings |

Old keys are no longer read. Move deployment overrides as well as settings files. Add `https://` to old bare hostnames.

When moving from nested tenant UI addresses to sibling addresses, set `TenantBaseDomain` explicitly. Split-host clients now use the shared API address without a tenant prefix. Check existing tenant identifiers for reserved names or invalid characters before changing a live deployment. Update saved links and external integrations to use the chosen addresses.

The tenant database migration adds a unique index that compares identifiers without regard to letter case and excludes deleted tenants. It prevents concurrent requests from creating duplicate identifiers. If existing tenants that are not deleted have duplicate identifiers, the migration stops. Resolve those conflicts before applying the migration; the migration does not rename or delete tenant records.

## Check your setup

- A startup URL error names the setting to correct. Check the scheme and remove any path.
- A browser configuration error can mean `/configuration/urls` is unavailable. Check the UI host and proxy route.
- A CORS error can mean the browser's scheme, hostname, or port differs from the allowed origin. Check Backend's `RootUiUrl`, `TenantBaseDomain`, reserved identifiers, and CORS settings.
- An API connection error on the server can mean the public address is unreachable internally. Set `ServerApiBaseUrl` to the reachable API origin.
- A Google redirect mismatch means the registered redirect URI must match the derived callback exactly. Check both split-host processes use the same root UI URL.
