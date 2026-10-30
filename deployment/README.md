# Unified HelpDesk deployment

This stack keeps `APIHelpDesk` and `MyHelpDesk` in separate Git repositories while deploying them as one system:

- `web`: React/Vite assets served by Nginx on the public port.
- `api`: ASP.NET Core .NET 8 service on the private Compose network.
- `/api/`: REST requests proxied from Nginx to `api:8080`.
- `/hubs/chat` and `/hubs/presence`: SignalR requests and WebSocket upgrades proxied from Nginx to `api:8080`.

The stack does not create databases or run migrations. The API must be able to reach the existing PostgreSQL and SQL Server databases from the deployment host.

## Prerequisites

- Git.
- Docker Engine or Docker Desktop with Docker Compose v2 (`docker compose version`).
- Network access from the API container to the configured databases and external ECS/weather services.
- A free public TCP port for the frontend; the default is `8080`.

## Repository layout

The default `MYHELPDESK_CONTEXT` expects sibling checkouts:

```text
<parent>/
  APIHelpDesk/
    deployment/
  MyHelpDesk/
```

If the frontend is elsewhere, change `MYHELPDESK_CONTEXT` in `deployment/.env` to an absolute path or a path relative to `deployment/docker-compose.yml`.

## First deployment

Clone both repositories into the expected layout, or use the existing checkouts:

```powershell
git clone https://github.com/fikriridho177-ai/APIHelpDesk.git
git clone https://github.com/fikriridho177-ai/MyHelpDesk.git
Set-Location APIHelpDesk
```

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

Run the update from the parent directory containing both repositories. `--ff-only` prevents an accidental merge of unrelated local history:

```powershell
git -C APIHelpDesk pull --ff-only
git -C MyHelpDesk pull --ff-only
Set-Location APIHelpDesk
docker compose --env-file deployment/.env -f deployment/docker-compose.yml up -d --build --remove-orphans
docker compose --env-file deployment/.env -f deployment/docker-compose.yml ps
```

If either pull reports local changes or cannot fast-forward, stop and review that checkout before rebuilding. Do not discard local changes as part of an ordinary deployment update.

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

Record the known-good API and frontend commits before updating:

```powershell
git -C APIHelpDesk rev-parse HEAD
git -C MyHelpDesk rev-parse HEAD
```

For a rollback, make sure both worktrees are clean, fetch the repositories, and check out the matching known-good commits or tags:

```powershell
git -C APIHelpDesk fetch origin
git -C MyHelpDesk fetch origin
git -C APIHelpDesk switch --detach <known-good-api-commit>
git -C MyHelpDesk switch --detach <known-good-frontend-commit>
Set-Location APIHelpDesk
docker compose --env-file deployment/.env -f deployment/docker-compose.yml up -d --build --remove-orphans
```

There is no automatic database rollback. Check whether the selected application commits are compatible with the current database schema before switching production traffic.

## Configuration and security

- Keep `deployment/.env` on the deployment host; it is ignored by Git.
- Keep `VITE_API_URL=/api` and `VITE_IOT_API_URL=/api` for the integrated stack so browser requests remain same-origin.
- Do not put passwords, connection strings, JWT keys, or access tokens in Dockerfiles, Compose files, or committed frontend files.
- Verify the deployment file is ignored with `git check-ignore -v deployment/.env`.
- Rotate any credential that has been exposed outside the protected deployment host, even if the value is later moved into `.env`.
- Only publish the frontend port. Do not add database ports to this Compose stack.

## Troubleshooting

### Frontend build cannot find `MyHelpDesk`

Check `MYHELPDESK_CONTEXT` in `deployment/.env`. The default is `../../MyHelpDesk` relative to `deployment/docker-compose.yml`.

### Frontend loads but API calls fail

Confirm that the integrated build uses `/api`, then inspect the API log. A direct browser URL such as `http://localhost:5000/api` is a development configuration and is not used by this stack.

### Live Chat or online users do not update

Inspect the `web` log and confirm the request reaches `/hubs/chat` or `/hubs/presence`. The Nginx configuration must keep HTTP/1.1 and forward Upgrade/Connection headers; rebuild the web image after changing `nginx.conf`.

### API starts but cannot load data

Check the connection string names and network routes in `deployment/.env`, then inspect `docker compose logs api`. The stack does not create or migrate the external databases.
