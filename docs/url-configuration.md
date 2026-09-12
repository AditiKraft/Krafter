# Configure application URLs

Set application URLs on the server. The browser reads the public values from the UI host at startup. You do not need to edit URL settings in `wwwroot` or rebuild the browser app when a deployment address changes.

## Choose your hosting mode

| Mode | Required settings | Where to set them |
|------|-------------------|-------------------|
| Single host | `Urls:RootUiUrl` | UI.Web, which hosts both the UI and API |
| Split host | `Urls:RootUiUrl` and `Urls:ApiBaseUrl` | Both Backend and UI.Web, with matching values |

`RootUiUrl` is the public address of the root UI. `ApiBaseUrl` is the public API address in split-host mode. Use complete origins, such as `https://app.example.com`: scheme, host, and optional port. Paths, credentials, query strings, and fragments are not supported. The app reports invalid settings during startup.

Single-host browser requests use the current page's origin, including its tenant subdomain. Omit `ApiBaseUrl` in single-host mode; startup rejects it to avoid conflicting configuration.

## Local development

The generated Development settings already contain the standard local addresses:

| Setting | Single host | Split host |
|---------|-------------|------------|
| `Urls:RootUiUrl` | `https://localhost:7291` | `https://localhost:7291` |
| `Urls:ApiBaseUrl` | Omitted | `https://localhost:5199` |

Start the Aspire AppHost as described in the [README](../README.md). Aspire supplies the server connection address automatically. You do not need to configure `services:api:*` yourself.

If you change a local port, update the public URL and the matching launch profile. Public URL settings do not change listening ports. Use `launchSettings.json`, `ASPNETCORE_URLS`, or your hosting platform to set those ports.

## Deployment

Replace the `example.com` sample addresses with your own domains. Use HTTPS for public deployments. Set environment variables on the application processes, or use your platform's configuration system. A double underscore separates configuration sections in environment variable names.

For **single host**, set this on UI.Web:

```text
Urls__RootUiUrl=https://app.example.com
```

For **split host**, set these on both Backend and UI.Web:

```text
Urls__RootUiUrl=https://app.example.com
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

The anonymous `/configuration/urls` endpoint returns only `RootUiUrl` and `ApiBaseUrl`, with `Cache-Control: no-store`. Internal server addresses and secrets are excluded. Keep this endpoint available to the browser when configuring a reverse proxy.

## Tenant domains and CORS

With multiple tenants enabled, the configured public addresses are the base domains. For tenant `blue`:

| Mode | UI address | Browser API address |
|------|------------|---------------------|
| Single host | `https://blue.app.example.com` | `https://blue.app.example.com` |
| Split host | `https://blue.app.example.com` | `https://blue.api.example.com` |

Configure DNS, TLS certificates, and your proxy for these tenant hosts. A tenant is one DNS label before the configured base domain. The `app` and `api` labels in the base addresses are not tenant names. IP addresses and `localhost` do not receive tenant prefixes.

In split-host mode, CORS controls which browser origins can call Backend. Backend allows the exact `RootUiUrl` origin automatically. The template also sets:

```json
"Cors": {
  "AllowTenantSubdomains": true,
  "AllowedOrigins": []
}
```

`AllowTenantSubdomains` allows one tenant label under the root UI domain, with the same scheme and port. It does not allow unrelated domains, nested subdomains, or a different port. Set it to `false` if your app does not use tenant subdomains. This rule does not apply to IP or localhost base addresses.

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

## Check your setup

- A startup URL error names the setting to correct. Check the scheme and remove any path.
- A browser configuration error can mean `/configuration/urls` is unavailable. Check the UI host and proxy route.
- A CORS error can mean the browser's scheme, hostname, or port differs from the allowed origin. Check Backend's `RootUiUrl` and CORS settings.
- An API connection error on the server can mean the public address is unreachable internally. Set `ServerApiBaseUrl` to the reachable API origin.
- A Google redirect mismatch means the registered redirect URI must match the derived callback exactly. Check both split-host processes use the same root UI URL.
