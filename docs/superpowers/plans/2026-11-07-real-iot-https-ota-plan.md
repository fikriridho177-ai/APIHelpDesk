# Real IoT HTTPS OTA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the WAD device-management demo with a tested HTTPS control plane for real ESP8266 heartbeats, telemetry, firmware delivery, OTA progress, and post-reboot confirmation.

**Architecture:** The ASP.NET Core API stores the writable IoT control plane in the existing PostgreSQL `AppDbContext`, while the existing SQL Server `MyKTTATest` monitoring readers remain unchanged. User operations use JWT plus WAD permission; devices use a one-time per-device key in `X-Device-Key`. MyHelpDesk keeps the existing service interface and adds an API implementation selected by `VITE_WAD_DEVICE_MANAGEMENT_MODE`, with no fallback to Demo records.

**Tech Stack:** .NET 8, ASP.NET Core MVC, EF Core/Npgsql, PostgreSQL SQL migration, xUnit, React/Vite/TypeScript, Axios, Node test runner, ESP8266 Arduino HTTP/OTA client, Docker Compose, Nginx.

**Spec:** `docs/superpowers/specs/2026-11-07-real-iot-https-ota-design.md`

## Global Constraints

- Device API authentication uses `X-Device-Key`; plaintext keys are returned only once during provisioning and are never returned by list/detail endpoints.
- Production device traffic uses HTTPS; HTTP is not an accepted production firmware URL.
- OTA success requires a post-reboot heartbeat whose reported firmware version matches the target version.
- Firmware checksum is an integrity field, not an authenticity claim; signature status remains separate.
- ATmega2560 OTA is out of scope; the first hardware target is ESP8266 firmware.
- Existing read-only monitoring endpoints backed by `MyKTTATest` must remain behaviorally unchanged.
- API mode must surface request errors and must never silently load Demo/localStorage data.
- Firmware binaries are stored on a configured volume, never in git, the frontend bundle, or localStorage.
- Preserve the existing local `.gitignore` change and unrelated `IoT` workbook changes.

## Review Focus

- Invalid, revoked, mismatched, and duplicated device credentials must be rejected without leaking whether another device exists; cover in `IotDeviceCredentialRulesTests` and agent-controller tests.
- Retries and duplicate telemetry/heartbeat timestamps must not create unbounded duplicate reports; cover idempotency in `IotAgentControllerTests`.
- Out-of-order OTA events, version mismatch after reboot, timeout, and retry-as-new-attempt must never produce a false `succeeded`; cover in `IotOtaRulesTests` and OTA-controller tests.
- Firmware upload size/path/target validation and device-scoped download authorization must reject unsafe or cross-device access; cover in `IotFirmwareServiceTests`.
- Frontend API failures, empty responses, stale `lastSeen`, and API-mode refresh must remain explicit rather than falling back to Demo state; cover in `wadDeviceManagementApi.test.mjs`.

## File Map

### APIHelpDesk

- `src/AuthUtility.Domain/Entities/IotManagementEntities.cs`: PostgreSQL entities for agent identity, credentials, reports, sensor metadata/mappings, configuration, firmware, OTA jobs/events, and activity.
- `src/AuthUtility.Application/DTOs/IoT/IotManagementDtos.cs`: typed admin and device request/response contracts.
- `src/AuthUtility.Infrastructure/Data/AppDbContext.cs`: DbSets, table/column mappings, indexes, and relationships for the new entities.
- `src/AuthUtility.Api/IoT/IotDeviceCredentialService.cs`: key generation, PBKDF2 hashing/verification, revocation, and request authentication.
- `src/AuthUtility.Api/IoT/IotOtaRules.cs`: pure OTA state transition, confirmation, timeout, and compatibility rules.
- `src/AuthUtility.Api/IoT/IotFirmwareService.cs`: upload validation, storage, SHA-256 calculation, streaming, and path-safe file naming.
- `src/AuthUtility.Api/Controllers/IotAgentController.cs`: authenticated device heartbeat, telemetry, configuration ACK, OTA polling/events/confirmation, and firmware download.
- `src/AuthUtility.Api/Controllers/IotManagementController.cs`: JWT-protected operator CRUD, provisioning, firmware upload, OTA assignment, reports, and activity.
- `src/AuthUtility.Api/Program.cs`: service registration and configuration for storage/key limits.
- `sql/2026-11-07-iot-ota-control-plane.sql`: idempotent PostgreSQL schema migration and indexes.
- `tests/AuthUtility.Tests/IotOtaRulesTests.cs`, `IotDeviceCredentialRulesTests.cs`, `IotAgentControllerTests.cs`, `IotManagementControllerTests.cs`, `IotFirmwareServiceTests.cs`: focused backend tests.
- `.env.example`, `deployment/.env.example`, `deployment/docker-compose.yml`, `deployment/README.md`: storage path, size limits, volume, HTTPS/device provisioning, and rollout documentation.

### MyHelpDesk

- `src/features/ecs/deviceManagement/types.ts`: shared demo/API service types and real API response fields.
- `src/features/ecs/deviceManagement/apiService.ts`: Axios implementation of the device-management service contract.
- `src/features/ecs/deviceManagement/serviceFactory.ts`: mode selection without API-to-Demo fallback.
- `src/features/ecs/pages/WadDeviceManagementPage.tsx`: consume the selected service and render real status/provisioning/upload/OTA states.
- `tests/wadDeviceManagementApi.test.mjs`: API-mode contract, error propagation, and no-localStorage fallback checks.
- `.env.example` or deployment build configuration: `VITE_WAD_DEVICE_MANAGEMENT_MODE` and API base URL.

### Firmware

- `E:/git-pkb/IoTMegaESP/ESP8266_OTA_Client/ESP8266_OTA_Client.ino`: migrate the ESP8266 polling client to HTTPS, device key headers, new heartbeat/OTA/event/confirmation endpoints, and NVS configuration.
- `E:/git-pkb/IoTMegaESP/README.md` (create if absent): provisioning, CA certificate, endpoint contract, and physical verification steps.
- Do not touch the unrelated modified/untracked workbook files in `E:/git-pkb/IoT`.

---

### Task 1: Domain entities, DTOs, database schema, and pure OTA rules

**Files:**
- Create: `src/AuthUtility.Domain/Entities/IotManagementEntities.cs`
- Create: `src/AuthUtility.Application/DTOs/IoT/IotManagementDtos.cs`
- Create: `src/AuthUtility.Api/IoT/IotOtaRules.cs`
- Modify: `src/AuthUtility.Infrastructure/Data/AppDbContext.cs`
- Create: `sql/2026-11-07-iot-ota-control-plane.sql`
- Test: `tests/AuthUtility.Tests/IotOtaRulesTests.cs`

**Interfaces:**
- `IotDeviceAgent.DeviceId` is the unique stable device identity; database row IDs are `Guid`.
- `IotOtaRules.TryTransition(IotOtaStatus current, IotOtaEventType requested, int progress, out IotOtaStatus next, out string error)` rejects backward/out-of-range transitions.
- `IotOtaRules.TryConfirm(string targetVersion, string reportedVersion, out string error)` returns success only for an exact non-empty version match.
- Admin DTOs use camelCase JSON; C# properties use PascalCase. Device IDs are case-insensitive for lookup but preserve the registered display value.

- [ ] **Step 1: Write failing xUnit tests** for the allowed OTA sequence `QUEUED → DOWNLOADING → VERIFYING → INSTALLING → REBOOTING → WAITING_CONFIRMATION → SUCCEEDED`, rejected skips/backward transitions, progress bounds, version mismatch, timeout, and compatible ESP8266 target validation.
- [ ] **Step 2: Run the focused tests and verify they fail** because the rule type and entities do not exist.

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore --filter FullyQualifiedName~IotOtaRules`

- [ ] **Step 3: Implement the entities, DTOs, pure rules, EF mappings, indexes, and idempotent SQL migration.** Use `IotDeviceAgent`, `IotDeviceCredential`, `IotDeviceReport`, `IotSensorType`, `IotDeviceSensor`, `IotDeviceConfiguration`, `IotFirmwarePackage`, `IotOtaJob`, `IotOtaEvent`, and `IotActivityEvent`; keep report payloads bounded JSON and use unique indexes for `DeviceId`, credential prefix, firmware `(TargetMcu, Version)`, and OTA `(JobId, Sequence)`.
- [ ] **Step 4: Run the focused rules test and an EF model/build check.**

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore --filter FullyQualifiedName~IotOtaRules`

  Expected: all transition/confirmation tests pass and the solution compiles.

- [ ] **Step 5: Commit the API domain/schema slice** with `git add src/AuthUtility.Domain src/AuthUtility.Application src/AuthUtility.Infrastructure sql tests/AuthUtility.Tests/IotOtaRulesTests.cs` and `git commit -m "feat(iot): add OTA control-plane domain"`.

### Task 2: Device credentials and agent heartbeat/telemetry/configuration API

**Files:**
- Create: `src/AuthUtility.Api/IoT/IotDeviceCredentialService.cs`
- Create: `src/AuthUtility.Api/Controllers/IotAgentController.cs`
- Modify: `src/AuthUtility.Api/Program.cs`
- Test: `tests/AuthUtility.Tests/IotDeviceCredentialRulesTests.cs`, `IotAgentControllerTests.cs`

**Interfaces:**
- `IIotDeviceCredentialService.Create()` returns `{ plaintextKey, prefix, hash, salt }`; only `plaintextKey` is returned to the provisioning caller.
- `IotAgentController` exposes `POST /api/iot/agent/heartbeat`, `POST /api/iot/agent/telemetry`, `GET /api/iot/agent/configuration`, and `POST /api/iot/agent/configuration/{version}/ack`.
- Every agent action authenticates `X-Device-Key`, requires the body `deviceId` to match the authenticated device, and returns `401` for missing/invalid/revoked keys and `409` for identity mismatch.
- Heartbeat updates last-seen/status/firmware/IP/RSSI and returns server time plus pending desired configuration/OTA indicators. Telemetry is size-limited, validates finite numeric readings, and is idempotent for the same device timestamp and payload hash.

- [ ] **Step 1: Write failing tests** for key generation/verification, wrong-device isolation, revocation, heartbeat update, duplicate report idempotency, telemetry validation/size limit, configuration retrieval, ACK/NACK, and anonymous access rejection.
- [ ] **Step 2: Run focused tests and verify expected failures** for missing service/controller behavior.

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore --filter FullyQualifiedName~IotDeviceCredentialRules|FullyQualifiedName~IotAgentController`

- [ ] **Step 3: Implement constant-time credential verification, device-scoped request authentication, heartbeat persistence, telemetry persistence, configuration read/ACK, and safe error responses.** Do not log keys or full telemetry payloads.
- [ ] **Step 4: Run focused tests, then the full backend test suite.**

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore`

  Expected: all existing and new tests pass.

- [ ] **Step 5: Commit** with `git add src/AuthUtility.Api/IoT src/AuthUtility.Api/Controllers/IotAgentController.cs src/AuthUtility.Api/Program.cs tests/AuthUtility.Tests/IotDeviceCredentialRulesTests.cs tests/AuthUtility.Tests/IotAgentControllerTests.cs` and `git commit -m "feat(iot): add authenticated device agent API"`.

### Task 3: Operator management API, firmware storage, and OTA jobs

**Files:**
- Create: `src/AuthUtility.Api/IoT/IotFirmwareService.cs`
- Create: `src/AuthUtility.Api/Controllers/IotManagementController.cs`
- Modify: `src/AuthUtility.Api/Program.cs`
- Test: `tests/AuthUtility.Tests/IotFirmwareServiceTests.cs`, `IotManagementControllerTests.cs`

**Interfaces:**
- Management routes are under `/api/iot/management` and require JWT plus `WAD_ECS_PERANGKAT`; permission fallback accepts `WAD_ECS_STASIUN` for existing installations.
- `POST /devices` provisions a device and returns the plaintext key once; subsequent device responses expose only `keyPrefix` and credential status.
- Device/sensor/configuration routes support the methods consumed by the current frontend service: list/create/update/delete devices, sensor types, device sensors, configuration save/apply/reject, diagnostics, and activity.
- `POST /firmware` accepts multipart field `file` plus metadata, rejects files over `IOT_FIRMWARE_MAX_BYTES`, computes SHA-256 server-side, stores under a generated filename, and never trusts a client path.
- `POST /ota` creates one job per compatible target device; `GET /ota` lists jobs/events. `GET /api/iot/agent/ota/pending` is device-scoped, and firmware download is allowed only for the assigned active job.
- `POST /api/iot/agent/ota/{jobId}/events` and `/confirm` use `IotOtaRules`; a matching post-reboot heartbeat/confirmation is the only path to `SUCCEEDED`.

- [ ] **Step 1: Write failing tests** for permission enforcement, one-time key response, device CRUD, sensor/config CRUD, firmware hash/path/size validation, incompatible assignment rejection, cross-device download rejection, event ordering, version mismatch, and successful confirmation.
- [ ] **Step 2: Run focused tests and verify they fail** because the management controller and firmware service do not exist.

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore --filter FullyQualifiedName~IotFirmwareService|FullyQualifiedName~IotManagementController`

- [ ] **Step 3: Implement the service/controller layer, permission helper, one-time provisioning response, firmware streaming, OTA assignment, and activity events.** Keep existing `IotDeviceController` and `IotMonitoringController` contracts untouched.
- [ ] **Step 4: Run focused and full backend tests, then inspect generated API routes/OpenAPI in development.**

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore`

  Expected: all tests pass and no legacy IoT monitoring test changes are required.

- [ ] **Step 5: Commit** with `git add src/AuthUtility.Api/IoT src/AuthUtility.Api/Controllers/IotManagementController.cs src/AuthUtility.Api/Program.cs tests/AuthUtility.Tests/IotFirmwareServiceTests.cs tests/AuthUtility.Tests/IotManagementControllerTests.cs` and `git commit -m "feat(iot): add real device management and OTA jobs"`.

### Task 4: MyHelpDesk API service and real-mode management UI

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/ecs/deviceManagement/types.ts`
- Create: `E:/git-pkb/MyHelpDesk/src/features/ecs/deviceManagement/apiService.ts`
- Create: `E:/git-pkb/MyHelpDesk/src/features/ecs/deviceManagement/serviceFactory.ts`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/ecs/pages/WadDeviceManagementPage.tsx`
- Create: `E:/git-pkb/MyHelpDesk/tests/wadDeviceManagementApi.test.mjs`
- Modify: `E:/git-pkb/MyHelpDesk/.env.example` or deployment build environment documentation

**Interfaces:**
- `DeviceManagementService.mode` becomes `'demo' | 'api'`; all existing method names remain stable so Demo behavior is preserved.
- `createDeviceManagementService({ mode, storage, httpClient })` selects exactly one implementation. API mode never reads `DEVICE_MANAGEMENT_STORAGE_KEY` and propagates HTTP errors.
- API JSON mapping covers `devices`, `sensorTypes`, `deviceSensors`, `configurations`, `firmware`, `ota`, `diagnostics`, and `activity`, including `lastSeen`, `reportedFirmwareVersion`, SHA-256, OTA event history, and one-time provisioning key display.
- The page shows a clear `Mode API — Data Perangkat` state, loading/error/empty states, credential copy-once warning, real heartbeat age, and disables Demo-only `advanceOta`/sensor simulation controls in API mode.

- [ ] **Step 1: Write failing Node tests** for API mode selection, request path/method mapping, JWT header forwarding, no localStorage access in API mode, HTTP error propagation, one-time key rendering, and stale/offline status presentation.
- [ ] **Step 2: Run the focused test and verify it fails** because the API adapter/factory does not exist.

  Run from `E:/git-pkb/MyHelpDesk`: `node --test tests/wadDeviceManagementApi.test.mjs`

- [ ] **Step 3: Implement the Axios adapter/factory and update the page/types.** Keep `demoService.ts` intact for explicit local Demo mode; do not silently migrate existing stored Demo records into API data.
- [ ] **Step 4: Run focused tests, the full frontend test suite, and production build.**

  Run from `E:/git-pkb/MyHelpDesk`: `node --test tests/*.test.mjs` and `npm run build`

  Expected: all tests pass and TypeScript/Vite build succeeds.

- [ ] **Step 5: Commit the frontend slice in its own repository** with `git -C E:/git-pkb/MyHelpDesk add ...` and `git -C E:/git-pkb/MyHelpDesk commit -m "feat(wad): connect device management to real API"`. Do not stage `APIHelpDesk/.gitignore` from this commit.

### Task 5: Migrate the ESP8266 polling client to the new secure contract

**Files:**
- Modify: `E:/git-pkb/IoTMegaESP/ESP8266_OTA_Client/ESP8266_OTA_Client.ino`
- Create or modify: `E:/git-pkb/IoTMegaESP/README.md`
- Test: `E:/git-pkb/MyHelpDesk/tests/esp8266OtaContract.test.mjs` (source-contract checks only)

**Interfaces:**
- Configure `SERVER_BASE_URL` as an HTTPS API origin, `DEVICE_ID` as the NVS/provisioned identity, and `DEVICE_API_KEY` outside the source-controlled sketch when the board build supports secure build secrets.
- Every heartbeat, telemetry, OTA poll, event, and confirmation request sends `X-Device-Key` and JSON `deviceId`.
- Replace `/api/device_ping.php` and `/api/check.php` with `/api/iot/agent/heartbeat`, `/api/iot/agent/telemetry`, `/api/iot/agent/ota/pending`, `/api/iot/agent/ota/{jobId}/events`, and `/api/iot/agent/ota/{jobId}/confirm`.
- OTA response fields are `jobId`, `targetVersion`, `firmwareId`, `firmwareUrl`, `sizeBytes`, and `sha256`; the client reports downloading/verifying/installing/rebooting and sends post-reboot confirmation with the reported firmware version.
- Remove hardcoded Wi-Fi credentials and the old HTTP URL from the production sketch; document provisioning and CA trust configuration instead.

- [ ] **Step 1: Write source-contract tests** asserting old PHP endpoints/HTTP URL/hardcoded credentials are absent and the new HTTPS routes/header/event names are present.
- [ ] **Step 2: Run the contract test and verify it fails** against the current sketch.

  Run: `node --test E:/git-pkb/MyHelpDesk/tests/esp8266OtaContract.test.mjs`

- [ ] **Step 3: Implement the sketch migration** with non-blocking polling, NVS identity/key loading, TLS client setup, bounded JSON parsing, OTA event reporting, and confirmation after reboot. Preserve the existing Mega limitation: only ESP8266 code is updated.
- [ ] **Step 4: Run the source-contract test and Arduino/ESP8266 compile check if `arduino-cli` and the required board/core libraries are installed.** If the toolchain is unavailable, record that exact limitation; do not claim a hardware compile or OTA success.
- [ ] **Step 5: Commit firmware changes only if the firmware directory becomes a Git checkout; otherwise leave the external sketch changes clearly listed for the operator and do not overwrite its unrelated files.**

### Task 6: Deployment, environment, migration, and operator documentation

**Files:**
- Modify: `.env.example`, `deployment/.env.example`, `deployment/docker-compose.yml`, `deployment/README.md`
- Modify if needed: `Dockerfile`, `MyHelpDesk/Dockerfile`, `MyHelpDesk/nginx.conf`
- Create if needed: `deployment/firmware/.gitkeep` only if the volume convention requires a documented mount (never commit binaries)

**Interfaces:**
- `IOT_FIRMWARE_STORAGE_PATH`, `IOT_FIRMWARE_MAX_BYTES`, and allowed frontend mode/API variables are documented with safe non-secret defaults.
- The API container mounts a persistent firmware volume; the frontend/Nginx proxy preserves `/api/iot/*`, JSON errors, and binary download headers instead of rewriting them to `index.html`.
- Documentation gives the exact order: apply SQL migration, configure HTTPS/API origin, start services, provision device, flash migrated sketch, create firmware package, assign OTA, and verify post-reboot heartbeat.

- [ ] **Step 1: Write static deployment checks** for env names, persistent volume, no public database port, API proxy separation, and no firmware binaries in tracked files.
- [ ] **Step 2: Run the checks and verify they fail** for the missing storage/mode configuration.
- [ ] **Step 3: Implement Compose/Nginx/env/docs updates** without changing unrelated deployment behavior.
- [ ] **Step 4: Run static checks, `dotnet build AuthUtility.sln --no-restore`, frontend build, and backend tests.**
- [ ] **Step 5: Commit deployment/docs** with `git add .env.example deployment Dockerfile sql/2026-11-07-iot-ota-control-plane.sql` and `git commit -m "docs(deploy): document real IoT OTA rollout"`; keep unrelated `.gitignore` changes unstaged.

### Task 7: Integrated verification and handoff

**Files:**
- Modify only files required by failed verification.
- Create: `docs/superpowers/plans/2026-11-07-real-iot-https-ota-progress.md` as the execution ledger if task-by-task status needs persistence.

**Interfaces:**
- Uses the API, frontend, SQL, deployment, and firmware contracts from Tasks 1–6.

- [ ] **Step 1: Run backend verification.**

  Run: `dotnet test AuthUtility.sln --no-restore`, `dotnet build AuthUtility.sln --no-restore`, and `problems` for changed C# files.

- [ ] **Step 2: Run frontend verification.**

  Run from `E:/git-pkb/MyHelpDesk`: `node --test tests/*.test.mjs` and `npm run build`; run `problems` for changed TypeScript files.

- [ ] **Step 3: Run read-only API smoke checks** against a local configured database: missing key returns `401`, a provisioned key can heartbeat, duplicate report is idempotent, an operator can create an OTA job, and an unrelated device cannot download it.
- [ ] **Step 4: Run the SQL migration in a disposable/local database or validate it with the configured PostgreSQL client; record whether a real database was available.**
- [ ] **Step 5: If Arduino toolchain and a physical ESP8266 are available, perform one controlled OTA test and verify the post-reboot version; otherwise report the hardware boundary explicitly and provide the exact remaining flash/provision steps.**
- [ ] **Step 6: Review `git status` in `APIHelpDesk`, `MyHelpDesk`, and `IoT`; confirm unrelated local changes were preserved, then report commits, test output, and any hardware-only verification still pending.

## Self-Review

- **Spec coverage:** Tasks 1–3 cover storage, credentials, agent/admin API, OTA transitions, and firmware safety; Task 4 covers API-mode UI; Task 5 covers the ESP8266 contract; Task 6 covers deployment; Task 7 covers verification and the physical boundary.
- **Step scan:** Each task follows failing-test → expected failure → minimal implementation → focused/full verification → commit where the repository can safely commit.
- **Type consistency:** `DeviceId`, `FirmwareId`, `JobId`, `IotOtaStatus`, `IotOtaEventType`, and the service method names are shared from DTOs/API adapter and are explicitly named in the owning tasks.
- **Review focus:** Every listed failure class has an owning test task: credentials/identity (Task 2), event/version/order (Tasks 1 and 3), file safety (Task 3), and API-mode failure/staleness (Task 4).
- **Proportion:** The plan specifies interfaces, files, and verification commands without embedding implementation bodies; hardware-only proof remains explicitly separate from code completion.
