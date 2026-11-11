# MyHelpDesk deployment

This deployment keeps the Backend and Frontend in separate Git repositories and supports two runtime layouts:

- Backend: `APIHelpDesk` (`https://github.com/fikriridho177-ai/APIHelpDesk.git`)
- Frontend: `MyHelpDesk` (`https://github.com/fikriridho177-ai/MyHelpDesk.git`)

For production Dokploy, create two Applications inside the `MyHelpDesk` project. Each Application reads its own repository and deploys independently:

- `MyHelpDesk Backend`: repository `APIHelpDesk`, root `Dockerfile`, container port `8080`.
- `MyHelpDesk Frontend`: repository `MyHelpDesk`, root `Dockerfile`, container port `80`.

The `deployment/docker-compose.yml` file remains the local integrated-stack fallback. It keeps `MyHelpDesk` as a Git submodule so the two repositories can still be developed together locally.

- `web`: React/Vite assets served by Nginx on the public port.
- `api`: ASP.NET Core .NET 8 service on the private Compose network.
- `/api/`: REST requests proxied from Nginx to `api:8080`.
- `/hubs/chat` and `/hubs/presence`: SignalR requests and WebSocket upgrades proxied from Nginx to `api:8080`.

In the split Dokploy layout, the Frontend embeds the public Backend URL in `VITE_API_URL` and `VITE_IOT_API_URL`. Its Nginx `BACKEND_UPSTREAM` variable is also set to the public Backend hostname so the legacy `/api/` and `/hubs/` proxy paths remain valid.

The stack does not create databases or run migrations. The API must be able to reach the existing PostgreSQL and SQL Server databases from the deployment host.

## Prerequisites

- Git.
- Docker Engine or Docker Desktop with Docker Compose v2 (`docker compose version`).
- A GitHub provider credential authorized for both repositories. The split Dokploy Applications do not require Dokploy to clone a private submodule during deployment.
- Network access from the API container to the configured databases and external ECS/weather services.
- A free public TCP port for the frontend; the default is `8080`.

## Dokploy production setup

Use the Dokploy project name `MyHelpDesk` and create the following Applications under the `production` environment:

| Application | Repository | Build | Container port | Suggested domain |
| --- | --- | --- | --- | --- |
| `MyHelpDesk Backend` | `fikriridho177-ai/APIHelpDesk` | Dockerfile `/Dockerfile` | `8080` | `api.example.com` |
| `MyHelpDesk Frontend` | `fikriridho177-ai/MyHelpDesk` | Dockerfile `/Dockerfile` | `80` | `helpdesk.example.com` |

Backend configuration:

1. Select the GitHub provider, repository `APIHelpDesk`, branch `main`, build path `/`, and Dockerfile build type.
2. Copy the existing production API environment values into the Application environment. Keep database connection strings, JWT keys, and service credentials in Dokploy only.
3. Set `Cors__Origins__0` to the exact Frontend origin, including `http://` or `https://` and any port.
4. If this is the first deployment and the database has no administrator, set the optional `BootstrapAdmin__Password` secret (minimum 12 characters with upper/lowercase, number, and symbol). There is no source-code default administrator password; leave this variable unset after the first administrator exists.
5. Set `IOT_FIRMWARE_STORAGE_PATH=/app/firmware` and `IOT_FIRMWARE_MAX_BYTES=52428800` (or equivalent Dokploy environment values).
6. Mount persistent storage at `/app/firmware`; firmware metadata is stored in PostgreSQL but the binary is stored in this mounted directory.
7. Add a domain pointing to container port `8080`. Set **Path** to `/`, keep **Internal Path** at its default `/` (or empty), and disable **Strip Path** so both `/api/...` and `/hubs/...` reach the Backend unchanged. For `myhelpdesk-be.wanmixc.dev`, enable HTTPS. A domain restricted to `/api` serves REST but excludes SignalR.

Frontend configuration:

1. Select the GitHub provider, repository `MyHelpDesk`, branch `main`, build path `/`, and Dockerfile build type.
2. Set these build arguments to the public Backend URL:
   - `VITE_API_URL=http://api.example.com/api`
   - `VITE_IOT_API_URL=http://api.example.com/api`
3. Set `VITE_WAD_DEVICE_MANAGEMENT_MODE=api` so WAD device management loads the PostgreSQL-backed control plane.
4. Set the runtime environment variable `BACKEND_UPSTREAM=api.example.com`.
5. Add a domain pointing to container port `80`.

Deploy the Backend first, then the Frontend. Verify the Frontend login page, a normal authenticated API request, Live Chat, and the online-user list. Keep both domains on the same scheme in production; use HTTPS for both when TLS is configured.

Dokploy's GitHub provider can deploy each private repository directly, but a parent Compose deployment that clones a private submodule may not receive the same GitHub App credentials for the nested clone. That is why the production setup uses two Applications. See the [Dokploy GitHub provider documentation](https://docs.dokploy.com/docs/core/github) and [provider documentation](https://docs.dokploy.com/docs/core/providers).

## Repository layout

The deployment repository contains the frontend as a Git submodule:

```text
APIHelpDesk/
  MyHelpDesk/          # Git submodule -> fikriridho177-ai/MyHelpDesk
  deployment/
    docker-compose.yml
```

The default `MYHELPDESK_CONTEXT=../MyHelpDesk` is relative to `deployment/docker-compose.yml`. It therefore reads the Frontend submodule separately from the Backend source. If a different local FE checkout is required, change `MYHELPDESK_CONTEXT` in `deployment/.env` to an absolute path or another path relative to the Compose file.

## First deployment

Clone the Backend repository and initialize the separate Frontend repository:

```powershell
git clone --recurse-submodules https://github.com/fikriridho177-ai/APIHelpDesk.git
Set-Location APIHelpDesk
git submodule status
```

For a local integrated stack, use the Compose file below. Do not use this Compose application as the production Dokploy deployment unless the Dokploy Git provider is known to authenticate the private Frontend submodule.

Create the host-only environment file and fill in the real values for databases, JWT, ECS, and optional WebGIS settings:

```powershell
Copy-Item deployment/.env.example deployment/.env
notepad deployment/.env
```

Validate the Compose model before starting containers. Review the output locally and do not paste connection strings or JWT values into tickets or chat:

```powershell
docker compose --env-file deployment/.env -f deployment/docker-compose.yml config
```

Build and start the stack:

```powershell
docker compose --env-file deployment/.env -f deployment/docker-compose.yml up -d --build --remove-orphans
docker compose --env-file deployment/.env -f deployment/docker-compose.yml ps
```

The frontend is available at `http://localhost:8080/` by default. Set `WEB_PORT` in `deployment/.env` when the host uses another port.

## Update

For the local integrated stack, run the update from the `APIHelpDesk` checkout:

```powershell
git pull --ff-only
docker compose --env-file deployment/.env -f deployment/docker-compose.yml up -d --build --remove-orphans
docker compose --env-file deployment/.env -f deployment/docker-compose.yml ps
```

If the pull reports local changes or cannot fast-forward, stop and review the checkout before rebuilding. Do not discard local changes as part of an ordinary deployment update.

To update the Frontend from its separate upstream repository, run this from the `APIHelpDesk` root after reviewing the incoming changes:

```powershell
git submodule sync -- MyHelpDesk
git submodule update --remote --merge MyHelpDesk
git add .gitmodules MyHelpDesk
git commit -m "chore: update frontend submodule"
git push origin main
```

The Frontend repository remains independently editable and pushable from its own checkout. Dokploy deploys the exact Frontend commit recorded by the Backend repository, so update the submodule pointer in `APIHelpDesk` before deploying a new FE commit through this Compose stack.

For split Dokploy Applications, push changes independently:

- Push `APIHelpDesk` `main` to deploy the Backend Application.
- Push `MyHelpDesk` `main` to deploy the Frontend Application.
- Update the `MyHelpDesk` submodule pointer only when the local integrated Compose stack also needs the new Frontend commit.

## Status and logs

```powershell
docker compose --env-file deployment/.env -f deployment/docker-compose.yml ps
docker compose --env-file deployment/.env -f deployment/docker-compose.yml logs --tail=100 api
docker compose --env-file deployment/.env -f deployment/docker-compose.yml logs --tail=100 web
docker compose --env-file deployment/.env -f deployment/docker-compose.yml logs -f api web
```

The API is intentionally not published on a host port. To inspect its runtime URL without exposing secrets:

```powershell
docker compose --env-file deployment/.env -f deployment/docker-compose.yml exec api printenv ASPNETCORE_URLS
```

After signing in through the frontend, exercise one normal API-backed page, open Live Chat, and open the online-user list. Those paths verify REST, `/hubs/chat`, and `/hubs/presence` through the same origin.

## Rollback

Record the known-good Backend and Frontend commits before updating:

```powershell
git rev-parse HEAD
```

For a rollback, make sure the worktree is clean, fetch the repository, and check out the known-good commit:

```powershell
git fetch origin
git switch --detach <known-good-api-commit>
docker compose --env-file deployment/.env -f deployment/docker-compose.yml up -d --build --remove-orphans
```

For split Dokploy Applications, roll back the Backend and Frontend to compatible commits independently, then redeploy both Applications. There is no automatic database rollback. Check whether the selected application commits are compatible with the current database schema before switching production traffic.

## Configuration and security

- Keep `deployment/.env` on the deployment host; it is ignored by Git.
- Keep `VITE_API_URL=/api` and `VITE_IOT_API_URL=/api` for the integrated stack so browser requests remain same-origin.
- Keep `VITE_WAD_DEVICE_MANAGEMENT_MODE=api` in production; use `demo` only for an intentional local fixture environment.
- For split Dokploy Applications, set both `VITE_*` values to the public Backend URL and set Frontend runtime `BACKEND_UPSTREAM` to the Backend hostname without a scheme or path.
- Keep the API firmware directory on persistent storage. Recreating the API container without `/app/firmware` loses uploaded `.bin` packages even though their PostgreSQL metadata remains.
- Set `Cors__Origins__0` to the exact frontend origin used by the browser, including the scheme and port when present (for example `http://helpdesk.example.com`). This is required for SignalR WebSocket negotiation.
- For legacy SQL Server instances that fail during the pre-login TLS handshake, add `Encrypt=False;TrustServerCertificate=True` to each SQL Server connection string. Keep PostgreSQL connection strings in PostgreSQL format; do not add SQL Server `Encrypt` keywords to them.
- Do not put passwords, connection strings, JWT keys, or access tokens in Dockerfiles, Compose files, or committed frontend files.
- Set `BootstrapAdmin__Password` only as a Dokploy/runtime secret when a first administrator must be created; never commit its value or paste it into logs.
- Use a JWT key of at least 32 random characters and rotate it through Dokploy secrets when exposure is suspected.
- Verify the deployment file is ignored with `git check-ignore -v deployment/.env`.
- Rotate any credential that has been exposed outside the protected deployment host, even if the value is later moved into `.env`.
- Only publish the frontend port. Do not add database ports to this Compose stack.

## Troubleshooting

### Frontend build cannot find `MyHelpDesk` in the local stack

Check that the `MyHelpDesk` submodule is initialized in the APIHelpDesk checkout:

```powershell
git submodule update --init --recursive
```

The default context is `../MyHelpDesk` relative to `deployment/docker-compose.yml`. If a custom checkout is intended, verify `MYHELPDESK_CONTEXT` in `deployment/.env`. The split Dokploy Frontend Application uses the `MyHelpDesk` repository directly and does not use this submodule path.

### Frontend loads but API calls fail

Confirm that the integrated build uses `/api`, or that the split build has the public Backend URL embedded in `VITE_API_URL` and `VITE_IOT_API_URL`. A direct browser URL such as `http://localhost:5000/api` is a development configuration and is not used by the production split stack.

If the split Frontend returns `502 Bad Gateway`, inspect the Nginx logs first. An error such as `host not found in upstream "api"` means `BACKEND_UPSTREAM` was not set on the Frontend Application; set it to the public Backend hostname and redeploy.

### Live Chat or online users do not update

First check whether the hub requests reach the Backend. A browser error about a missing `Access-Control-Allow-Origin` header can also mean Traefik returned a 404 before the request reached ASP.NET Core. Adding origins to the API CORS policy cannot change that proxy response.

Compare REST and both SignalR preflight requests (replace the URLs for another deployment):

```powershell
$backend = 'https://myhelpdesk-be.wanmixc.dev'
$frontend = 'https://myhelpdesk.wanmixc.dev'
$paths = @('/api/auth/login', '/hubs/chat/negotiate?negotiateVersion=1', '/hubs/presence/negotiate?negotiateVersion=1')
foreach ($path in $paths) {
    Write-Output $path
    curl.exe --silent --show-error --max-time 15 --include --request OPTIONS "$backend$path" --header "Origin: $frontend" --header 'Access-Control-Request-Method: POST' --header 'Access-Control-Request-Headers: authorization,x-signalr-user-agent'
}
```

All three should return `204` with `Access-Control-Allow-Origin: https://myhelpdesk.wanmixc.dev`. If REST succeeds but the hubs return a plain `404 page not found` without `Server: Kestrel`, check **MyHelpDesk Backend > Domains** in Dokploy:

| Setting | Value |
| --- | --- |
| Host | `myhelpdesk-be.wanmixc.dev` |
| Path | `/` |
| Internal Path | `/` (default), or empty |
| Strip Path | Disabled |
| Container Port | `8080` |
| HTTPS | Enabled |

Save the domain and repeat the checks. Dokploy Application domain changes apply immediately; Docker Compose domain changes require redeployment. If the deployment intentionally restricts paths, keep the `/api` route and add a `/hubs` route to the same Backend service without stripping or adding a prefix. See the [Dokploy Domains documentation](https://docs.dokploy.com/docs/core/domains).

Then verify that both hub endpoints are mapped. Without an access token, these requests should return `401` with the frontend CORS header, rather than `404`:

```powershell
foreach ($hub in @('chat', 'presence')) {
    curl.exe --silent --show-error --max-time 15 --include --request POST "$backend/hubs/$hub/negotiate?negotiateVersion=1" --header "Origin: $frontend"
}
```

A successful preflight alone does not prove a hub exists: the API's global CORS middleware can answer OPTIONS for an unmapped path. Keep the frontend hub URLs at `/hubs/chat` and `/hubs/presence`; `/api/hubs/...` is not mapped by the Backend. After both checks pass, sign in and verify Live Chat and the online-user list in the browser.

If the requests reach Kestrel but the expected CORS header is still missing, inspect the `api` log for the browser origin. Add that exact origin under `Cors__Origins__0`, then redeploy the Backend. The CORS policy allows credentials for configured exact origins; the Nginx configuration must keep HTTP/1.1 and forward Upgrade/Connection headers. Rebuild the web image after changing `nginx.conf`.

### API starts but cannot load data

Check the connection string names and network routes in `deployment/.env`, then inspect `docker compose logs api`. The stack does not create or migrate the external databases.

### SQL Server pre-login TLS handshake fails

If the API log contains `Encryption(ssl/tls) handshake failed` or `Received an unexpected EOF`, confirm that the SQL Server connection string has `Encrypt=False;TrustServerCertificate=True`. If the error persists after a fresh deployment, the SQL Server may enforce encryption with an incompatible TLS configuration; fix the SQL Server certificate/TLS policy or use a private VPN route to a compatible instance. Do not publish SQL Server port `1433` to the public internet.
