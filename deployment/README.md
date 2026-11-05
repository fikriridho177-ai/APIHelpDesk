# MyHelpDesk deployment

This deployment keeps the Backend and Frontend in separate Git repositories while running them as one Compose stack:

- Backend: `APIHelpDesk` (`https://github.com/fikriridho177-ai/APIHelpDesk.git`)
- Frontend: `MyHelpDesk` (`https://github.com/fikriridho177-ai/MyHelpDesk.git`), linked into the Backend repository as a Git submodule

Dokploy reads the Backend repository for the API and Compose files, then initializes the Frontend submodule for the web build. This keeps FE and BE history, permissions, and changes separate without losing the same-origin `/api/` and SignalR proxy.

- `web`: React/Vite assets served by Nginx on the public port.
- `api`: ASP.NET Core .NET 8 service on the private Compose network.
- `/api/`: REST requests proxied from Nginx to `api:8080`.
- `/hubs/chat` and `/hubs/presence`: SignalR requests and WebSocket upgrades proxied from Nginx to `api:8080`.

The stack does not create databases or run migrations. The API must be able to reach the existing PostgreSQL and SQL Server databases from the deployment host.

## Prerequisites

- Git.
- Docker Engine or Docker Desktop with Docker Compose v2 (`docker compose version`).
- A GitHub provider credential that can read both the `APIHelpDesk` repository and its `MyHelpDesk` submodule when deploying from Dokploy.
- Network access from the API container to the configured databases and external ECS/weather services.
- A free public TCP port for the frontend; the default is `8080`.

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

For Dokploy, use the **GitHub** provider with repository `APIHelpDesk`, branch `main`, and Compose path `./deployment/docker-compose.yml`. Keep **Enable Submodules** enabled so Dokploy reads the separate `MyHelpDesk` repository into the Frontend folder.

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

Run the update from the `APIHelpDesk` checkout:

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

Record the known-good unified repository commit before updating:

```powershell
git rev-parse HEAD
```

For a rollback, make sure the worktree is clean, fetch the repository, and check out the known-good commit:

```powershell
git fetch origin
git switch --detach <known-good-api-commit>
docker compose --env-file deployment/.env -f deployment/docker-compose.yml up -d --build --remove-orphans
```

There is no automatic database rollback. Check whether the selected application commits are compatible with the current database schema before switching production traffic.

## Configuration and security

- Keep `deployment/.env` on the deployment host; it is ignored by Git.
- Keep `VITE_API_URL=/api` and `VITE_IOT_API_URL=/api` for the integrated stack so browser requests remain same-origin.
- Set `Cors__Origins__0` to the exact frontend origin used by the browser, including the scheme and port when present (for example `http://helpdesk.example.com`). This is required for SignalR WebSocket negotiation.
- For legacy SQL Server instances that fail during the pre-login TLS handshake, add `Encrypt=False;TrustServerCertificate=True` to each SQL Server connection string. Keep PostgreSQL connection strings in PostgreSQL format; do not add SQL Server `Encrypt` keywords to them.
- Do not put passwords, connection strings, JWT keys, or access tokens in Dockerfiles, Compose files, or committed frontend files.
- Verify the deployment file is ignored with `git check-ignore -v deployment/.env`.
- Rotate any credential that has been exposed outside the protected deployment host, even if the value is later moved into `.env`.
- Only publish the frontend port. Do not add database ports to this Compose stack.

## Troubleshooting

### Frontend build cannot find `MyHelpDesk`

Check that the `MyHelpDesk` submodule is initialized in the APIHelpDesk checkout:

```powershell
git submodule update --init --recursive
```

The default context is `../MyHelpDesk` relative to `deployment/docker-compose.yml`. **Enable Submodules** is required in Dokploy. If a custom checkout is intended, verify `MYHELPDESK_CONTEXT` in `deployment/.env`.

### Frontend loads but API calls fail

Confirm that the integrated build uses `/api`, then inspect the API log. A direct browser URL such as `http://localhost:5000/api` is a development configuration and is not used by this stack.

### Live Chat or online users do not update

Inspect the `api` log for a CORS message containing the browser origin. Add that exact origin under `Cors__Origins__0`, then redeploy. The Nginx configuration must keep HTTP/1.1 and forward Upgrade/Connection headers; rebuild the web image after changing `nginx.conf`.

### API starts but cannot load data

Check the connection string names and network routes in `deployment/.env`, then inspect `docker compose logs api`. The stack does not create or migrate the external databases.

### SQL Server pre-login TLS handshake fails

If the API log contains `Encryption(ssl/tls) handshake failed` or `Received an unexpected EOF`, confirm that the SQL Server connection string has `Encrypt=False;TrustServerCertificate=True`. If the error persists after a fresh deployment, the SQL Server may enforce encryption with an incompatible TLS configuration; fix the SQL Server certificate/TLS policy or use a private VPN route to a compatible instance. Do not publish SQL Server port `1433` to the public internet.
