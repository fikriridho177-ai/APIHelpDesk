# Docker Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menambahkan konfigurasi Docker yang memungkinkan API .NET 8 dibuild dan dijalankan ulang setelah source diperbarui melalui Git.

**Architecture:** Gunakan Dockerfile multi-stage yang hanya mempublish `AuthUtility.Api` dan Compose dengan satu service API. Runtime configuration masuk dari `.env` lokal; database tetap berada di infrastruktur eksternal dan source code tidak di-bind-mount ke container.

**Tech Stack:** Docker, Docker Compose, .NET 8 SDK/runtime, PowerShell, `dotnet test`.

**Spec:** `docs/superpowers/specs/2026-10-27-docker-deployment-design.md`

## Global Constraints

- Target framework API tetap `net8.0`.
- Container harus listen pada `http://+:8080`; port host default adalah `5100`.
- Secret dan connection string tidak boleh masuk ke Git atau Docker image.
- Compose tidak menambahkan database, migration, frontend, reverse proxy, atau CI/CD.
- File dan direktori untracked yang sudah ada di workspace tidak boleh dihapus atau diubah.

## Review Focus

- Build context berisi `appsettings.json` lokal atau `.env` sehingga secret ikut masuk image — diuji dengan inspeksi `.dockerignore` dan Docker build.
- API hanya listen pada loopback di dalam container — diuji dengan pemeriksaan `ASPNETCORE_URLS` dan port mapping Compose.
- `docker compose up` menghapus konfigurasi runtime karena env file tidak dipasang — diuji dengan `docker compose config` menggunakan env example.
- Image runtime menjalankan proses sebagai root — diuji dengan inspeksi instruksi `USER` pada Dockerfile.
- Perubahan source terbaru tidak masuk container lama — diuji dengan alur build/recreate terdokumentasi dan pemeriksaan image build.

### Task 1: Add Docker deployment configuration

**Files:**
- Create: `Dockerfile`
- Create: `docker-compose.yml`
- Create: `.dockerignore`
- Create: `.env.example`
- Reference: `docs/superpowers/specs/2026-10-27-docker-deployment-design.md`

**Interfaces:**
- Consumes: project files `src/AuthUtility.Api/AuthUtility.Api.csproj`, `src/AuthUtility.Application/AuthUtility.Application.csproj`, `src/AuthUtility.Domain/AuthUtility.Domain.csproj`, and `src/AuthUtility.Infrastructure/AuthUtility.Infrastructure.csproj`.
- Produces: Compose service `api`, container port `8080`, host port variable `API_PORT`, and .NET configuration variables using `ConnectionStrings__*`, `Jwt__*`, and `Ecs__*` names.

- [x] **Step 1: Define the multi-stage Dockerfile**

  Restore and publish only `src/AuthUtility.Api/AuthUtility.Api.csproj` with `mcr.microsoft.com/dotnet/sdk:8.0`, copy the published output into `mcr.microsoft.com/dotnet/aspnet:8.0`, expose port `8080`, set the non-root runtime user, and start `AuthUtility.Api.dll`.

- [x] **Step 2: Define Compose runtime configuration**

  Add one `api` service that builds from the repository root, loads `.env`, sets `ASPNETCORE_URLS=http://+:8080`, maps `${API_PORT:-5100}:8080`, and uses `restart: unless-stopped`. Do not add source or config bind mounts.

- [x] **Step 3: Define build-context exclusions and safe environment template**

  Exclude build output, local appsettings, `.env`, deployment artifacts, temporary probes, tests, docs, and Git metadata from `.dockerignore`. Add `.env.example` with placeholders for `API_PORT`, `ASPNETCORE_ENVIRONMENT`, all five connection strings, JWT settings, and ECS settings; include no real credentials.

- [x] **Step 4: Validate the deployment files**

  Run `docker compose config` with a temporary copy of `.env.example` as `.env`, verify the rendered service/port/env-file settings, and run `docker build -t apihelpdesk-api:verification .`.

- [x] **Step 5: Run the existing regression suite**

  Run `dotnet test AuthUtility.sln --no-restore` and record the exit code and test result. If Docker is unavailable, perform static checks and report that limitation without changing application source.

- [x] **Step 6: Review the diff and preserve unrelated workspace changes**

  Run `git diff --check`, inspect `git status --short`, and confirm only the planned deployment/design/plan files are newly added or modified by this task; leave pre-existing untracked artifacts untouched.

## Completion Criteria

- Dockerfile builds the API from the .NET 8 project references and uses a non-root runtime user.
- Compose renders one API service with `.env`, host port `5100` by default, and container URL `http://+:8080`.
- `.dockerignore` excludes local secrets and generated artifacts.
- `.env.example` contains no real secret values.
- Docker validation passes, or its unavailable prerequisite is explicitly reported.
- `dotnet test AuthUtility.sln --no-restore` passes.
- No pre-existing untracked workspace files are removed or altered.
