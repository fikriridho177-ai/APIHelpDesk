# IoT Menu Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the IoT menu hierarchy with the WAD-first operating model while keeping IoT data read-only and Super Admin-only.

**Architecture:** Store the menu hierarchy changes in an idempotent PostgreSQL script keyed by existing menu codes. Extend the existing frontend IoT pages with configurable source/menu metadata so the internal logger menu uses the same read-only API without duplicating page logic.

**Tech Stack:** ASP.NET Core, PostgreSQL SQL scripts, React, TypeScript, Vite.

**Spec:** `docs/superpowers/specs/2026-10-23-iot-menu-restructure-design.md`

## Global Constraints

- Preserve existing IoT menu codes and routes where they are already used by permissions or frontend resolution.
- Keep IoT API access read-only and Super Admin-only.
- Do not implement firmware repository, OTA upload, or update-history behavior.
- Do not change the WAD service flow or the existing ECS menu codes.
- Preserve unrelated working-tree changes.

## Review Focus

- A partially populated database: the migration must update existing codes without creating duplicate menu records.
- A normal user reading navigation: inactive/admin IoT items must not become a new WAD entry.
- An internal logger route: it must use `INTERNAL` source filtering and the matching menu permission code.
- A vendor route: existing `VENDOR` behavior must remain unchanged.
- An unfinished development menu: firmware/OTA/history must not expose a dead clickable route.

### Task 1: Apply the IoT menu data restructure

**Files:**
- Create: `sql/2026-10-23_iot_menu_restructure.sql`

**Interfaces:**
- Consumes: existing `UtlMenu` rows keyed by the IoT menu codes shown in the spec.
- Produces: updated parent IDs, names, routes, sort order, and inactive development rows.

- [x] Write a transactional, idempotent update keyed by menu code.
- [x] Set the selected parent hierarchy and preserve all existing menu IDs and codes.
- [x] Disable only firmware/OTA/update-history rows whose pages are not implemented.
- [x] Add a verification `SELECT` showing the final IoT tree.
- [x] Review the SQL for duplicate creation and run a syntax/whitespace check.

### Task 2: Map read-only internal logger pages

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTVendorDevicesPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/routes/MenuRouteResolver.tsx`

**Interfaces:**
- Consumes: existing `IoTSourceType`, `useMenuPermission`, and IoT device API.
- Produces: configurable vendor/internal device list rendering and resolver mappings for `DAFTAR_PERANGKAT_INTERNAL` and `MONITORING_SENSOR`.

- [x] Add optional page props for menu code, source type, title, subtitle, and eyebrow while preserving vendor defaults.
- [x] Map internal device listing to `sourceType="INTERNAL"` and its own permission code.
- [x] Map internal monitoring to `IoTMonitoringPage` with `sourceType="INTERNAL"` and `menuCode="MONITORING_SENSOR"`.
- [x] Keep unfinished firmware routes unmapped and inactive.

### Task 3: Verify the restructure

**Files:**
- Test: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/AuthUtility.Api.csproj`
- Test: `E:/git-pkb/MyHelpDesk/package.json`

- [x] Run API diagnostics/build without restoring dependencies.
- [x] Run frontend typecheck/build using the repository's configured command.
- [x] Run `git diff --check` and inspect changed-file scope.
- [x] Confirm no WAD menu configuration or existing IoT controller access guard was reverted.

