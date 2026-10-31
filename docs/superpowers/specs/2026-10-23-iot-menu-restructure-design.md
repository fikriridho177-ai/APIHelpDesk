# IoT menu restructure design

## Intent

Make the IoT navigation match the operational distinction shown by the WAD flow: WAD remains the user-facing service flow, while IoT Management is a technical, read-only logger area for Super Admin. Existing menu codes stay stable so group permissions and route resolution do not break.

## Goals

- Rename and reorder the existing IoT menu tree into `IoT Management → Dashboard IoT / Vendor Logger / Logger Lokal`.
- Keep the WAD service flow unchanged.
- Make the existing internal-device and internal-monitoring pages reachable through the menu.
- Keep firmware, OTA, and update-history records available for future development but inactive until their pages/API exist.
- Keep IoT API access read-only and Super Admin-only as already enforced by the controllers.

## Non-goals

- No write operations against the vendor/logger database.
- No implementation of firmware repository, OTA upload, or update-history workflows.
- No renaming of existing menu codes used by permissions or frontend route resolution.

## Constraints

- Menu structure is stored in PostgreSQL `UtlMenu`, not in `IotDeviceController`.
- The menu migration must be idempotent and must not create duplicate records.
- Frontend route resolution is code-based, so supported IoT codes must remain mapped to working pages.
- Existing local changes in the IoT controllers must be preserved.

## Approaches

1. Change only menu names in the database. This is low risk but leaves internal logger menu links without pages.
2. Rebuild the entire IoT module and add firmware/OTA screens. This is too broad for the current read-only requirement.
3. **Selected:** apply an idempotent data-only menu restructure, then add only the two missing read-only internal logger route mappings. This keeps the WAD flow stable and avoids presenting unfinished write features.

## Resulting structure

- `IoT Management` (group)
  - `Dashboard IoT` (Super Admin, read-only)
  - `Vendor Logger` (group)
    - `Daftar Perangkat Vendor` (read-only)
    - `Monitoring Sensor Vendor` (read-only)
  - `Logger Lokal` (group)
    - `Daftar Logger Lokal` (read-only)
    - `Monitoring Logger Lokal` (read-only)
    - `Firmware Repository` (inactive)
    - `OTA Upload` (inactive)
    - `Riwayat Update Firmware` (inactive)

