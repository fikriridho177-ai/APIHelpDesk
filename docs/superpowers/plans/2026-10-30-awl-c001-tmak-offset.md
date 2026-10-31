# AWL-C001 TMAK Offset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menampilkan nilai TMAK pada logger `AWL-C001` sebagai nilai asli ditambah 10 hanya di halaman frontend Monitoring Sensor.

**Architecture:** Buat helper frontend murni untuk penyesuaian nilai berdasarkan kode logger dan parameter. Gunakan helper itu hanya saat halaman `IoTMonitoringPage` menyiapkan nilai kartu terbaru dan titik grafik, tanpa memutasi respons API.

**Tech Stack:** React, TypeScript, Vite, Node.js built-in test runner.

**Spec:** `docs/superpowers/specs/2026-10-30-awl-c001-tmak-offset-design.md`

## Global Constraints

- Perubahan hanya pada frontend.
- Hanya logger `AWL-C001` yang diproses.
- Hanya parameter `TMAK` atau kode alias telemetry `TMAK_DISTANCE` yang diproses.
- Nilai yang ditampilkan adalah nilai numerik asli `+ 10`; nilai nonnumerik/null tetap apa adanya.
- API dan database tidak boleh diubah.
- Layar frontend lain di luar `IoTMonitoringPage` tidak boleh ikut berubah.

## Review Focus

- Logger lain tidak boleh mendapat offset: diuji pada helper.
- Parameter selain TMAK pada `AWL-C001` tidak boleh mendapat offset: diuji pada helper.
- Kode parameter yang muncul sebagai `TMAK_DISTANCE` tetap dikenali: diuji pada helper.
- Nilai string numerik dan nilai invalid/null tidak boleh merusak tampilan: diuji pada helper.
- Nilai terbaru dan grafik harus memakai helper yang sama: diverifikasi melalui build dan pemeriksaan perubahan halaman.

### Task 1: Frontend TMAK adjustment helper

**Files:**
- Create: `E:/git-pkb/MyHelpDesk/src/features/iot/lib/tmakOffset.ts`
- Test: `E:/git-pkb/MyHelpDesk/tests/tmakOffset.test.mjs`

**Interfaces:**
- Produces `adjustTmakValue(loggerCode: string | null | undefined, parameterCode: string, value: SensorValue | undefined): SensorValue | undefined`.

- [ ] **Step 1: Write the failing test**

  Tambahkan test untuk `AWL-C001` + `TMAK` dengan nilai `-65.95` yang mengharapkan `-55.95`, test alias `TMAK_DISTANCE`, serta test bahwa logger/parameter lain tidak berubah.

- [ ] **Step 2: Run test to verify it fails**

  Run: `node --test --experimental-strip-types tests/tmakOffset.test.mjs`

  Expected: FAIL karena helper belum tersedia.

- [ ] **Step 3: Implement the minimal helper**

  Implementasikan pencocokan case-insensitive untuk `AWL-C001`, `TMAK`, dan `TMAK_DISTANCE`; tambahkan 10 hanya jika nilai dapat dikonversi menjadi angka finite.

- [ ] **Step 4: Run test to verify it passes**

  Run: `node --test --experimental-strip-types tests/tmakOffset.test.mjs`

  Expected: PASS untuk seluruh test helper.

### Task 2: Apply adjustment only in Monitoring Sensor

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`

**Interfaces:**
- Consumes `adjustTmakValue` from Task 1.

- [ ] **Step 1: Apply helper to history chart points**

  Teruskan `selectedDevice.loggerCode` ke `CategorySection`/`SensorCard`, lalu gunakan helper saat `getChartPoints` membaca nilai parameter.

- [ ] **Step 2: Apply helper to latest card value**

  Gunakan helper yang sama sebelum `formatSensorValue` pada nilai terbaru kartu sensor.

- [ ] **Step 3: Run frontend build**

  Run from `E:/git-pkb/MyHelpDesk`: `npm run build`

  Expected: TypeScript dan Vite selesai dengan exit code 0.

### Task 3: Final verification

- [ ] **Step 1: Run helper test and frontend build again**

  Run:
  - `node --test --experimental-strip-types tests/tmakOffset.test.mjs`
  - `npm run build`

- [ ] **Step 2: Inspect diff**

  Run: `git diff --check` and confirm only the design/plan docs plus the frontend helper, helper test, and monitoring page are in scope.

- [ ] **Step 3: Commit**

  Commit with: `feat: offset AWL-C001 TMAK in monitoring view`
