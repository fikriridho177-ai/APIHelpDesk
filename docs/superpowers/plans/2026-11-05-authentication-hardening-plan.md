# Authentication Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menutup risiko authentication dan authorization tertinggi pada MyHelpDesk tanpa memutus kontrak deployment Bearer JWT yang sedang berjalan.

**Architecture:** Gunakan helper policy terpusat, claim JWT untuk status wajib ganti dan waktu perubahan password, validasi user pada setiap token, middleware pembatasan endpoint, dan guard `IsSuperAdminAsync` pada endpoint security. Frontend menghapus default credential dan menyimpan token baru setelah change-password.

**Tech Stack:** ASP.NET Core 8, EF Core/Npgsql, JWT Bearer, xUnit, React/TypeScript/Vite.

**Spec:** `docs/superpowers/specs/2026-11-05-authentication-hardening-design.md`

## Global Constraints

- Tidak menghapus atau mengubah data bisnis production.
- Tidak menaruh credential nyata di source code, dokumentasi, atau output terminal.
- Tetap kompatibel dengan Bearer JWT selama tahap ini.
- Jangan menyentuh file/folder untracked yang sudah ada dan tidak terkait.
- Password baru minimal 12 dan maksimal 128 karakter dengan huruf besar, huruf kecil, angka, dan simbol.

## Review Focus

- Password bootstrap kosong harus membuat startup tetap aman tanpa membuat user default; diuji pada bootstrap decision/helper.
- Token user nonaktif atau group nonaktif harus ditolak; diuji pada token validation logic.
- Token dari password lama harus ditolak setelah timestamp password berubah; diuji pada claim comparison.
- `MustChangePassword=true` hanya boleh mengakses change-password/me; diuji pada middleware path allowlist.
- User biasa tidak boleh mengubah group/menu/security atau membaca payroll lookup tanpa permission; diuji pada authorization helper/controller guard.

---

### Task 1: Password policy and default credential removal

**Files:**
- Create: `src/AuthUtility.Application/Security/PasswordPolicy.cs`
- Modify: `src/AuthUtility.Api/Controllers/AuthController.cs`
- Modify: `src/AuthUtility.Api/Controllers/UsersController.cs`
- Modify: `src/AuthUtility.Api/Program.cs`
- Modify: `src/AuthUtility.Infrastructure/Security/JwtTokenService.cs`
- Test: `tests/AuthUtility.Tests/PasswordPolicyTests.cs`

**Interfaces:**
- Produces `PasswordPolicy.Validate(string?) -> string?`, returning `null` for a valid password or a client-safe error message otherwise.
- Uses `UtlUser.LastPasswordChangedAt` in token claims.

- [ ] Write failing tests for null/short/weak/reused-format passwords and a valid 12-character password.
- [ ] Run the focused test and verify it fails because `PasswordPolicy` does not exist.
- [ ] Implement `PasswordPolicy.Validate` and apply it to login change-password and admin user create/update paths.
- [ ] Add recent password-history checks before accepting a new password and return a generic validation message.
- [ ] Remove `User@123` fallback; require a password on user creation and validate any admin reset password.
- [ ] Make bootstrap admin conditional on `BootstrapAdmin:Password`; never create it with a source default.
- [ ] Run focused tests and backend build.

### Task 2: Server-side privilege and sensitive lookup guards

**Files:**
- Modify: `src/AuthUtility.Application/Interfaces/IPermissionService.cs`
- Modify: `src/AuthUtility.Infrastructure/Security/PermissionService.cs`
- Modify: `src/AuthUtility.Api/Controllers/GroupsController.cs`
- Modify: `src/AuthUtility.Api/Controllers/SecurityController.cs`
- Modify: `src/AuthUtility.Api/Controllers/MenusController.cs`
- Modify: `src/AuthUtility.Api/Controllers/PKBPayrollCompanyController.cs`
- Modify: `src/AuthUtility.Api/Controllers/PKBPayrollDepartmentController.cs`
- Modify: `src/AuthUtility.Api/Controllers/PKBPayrollEmployeeController.cs`

**Interfaces:**
- Produces `IPermissionService.IsSuperAdminAsync(ClaimsPrincipal) -> Task<bool>`.
- Existing `HasPermissionAsync` remains the contract for `MASTER_USER` lookup access.

- [ ] Add a failing unit-level check for an inactive/non-superadmin principal returning false from the new service method.
- [ ] Implement `IsSuperAdminAsync` with active user and active group checks.
- [ ] Guard all group CRUD/scope/menu endpoints and all security/menu mutation/read endpoints with super-admin status.
- [ ] Guard payroll company/department/employee endpoints with `MASTER_USER` read permission.
- [ ] Remove exception `detail` from payroll responses while logging server-side.
- [ ] Build and run the complete backend test suite.

### Task 3: JWT revocation and mandatory password-change enforcement

**Files:**
- Create: `src/AuthUtility.Api/Security/PasswordChangeRequiredMiddleware.cs`
- Create: `src/AuthUtility.Infrastructure/Security/AuthClaimNames.cs`
- Modify: `src/AuthUtility.Infrastructure/Security/JwtTokenService.cs`
- Modify: `src/AuthUtility.Api/Program.cs`
- Modify: `src/AuthUtility.Api/Controllers/AuthController.cs`
- Modify: `src/AuthUtility.Application/DTOs/Auth/LoginResponse.cs`
- Test: `tests/AuthUtility.Tests/AuthClaimTests.cs`

**Interfaces:**
- Token claims use `auth_must_change` and `auth_pwd_changed_ticks`.
- Middleware permits `/api/auth/change-password`, `/api/auth/me`, and `OPTIONS` while a password change is required.

- [ ] Write failing tests for claim-name constants and stamp mismatch behavior.
- [ ] Run focused tests and verify the intended failure.
- [ ] Add claims, reduce access-token lifetime to 30 minutes, and validate user/group active state plus password timestamp in `OnTokenValidated`.
- [ ] Add middleware after authentication and before authorization to block other requests for `MustChangePassword` users.
- [ ] Mark `LastPasswordChangedAt` on every accepted password change and return a fresh JWT.
- [ ] Run complete backend tests and build.

### Task 4: Frontend session and credential UX hardening

**Files:**
- Modify: `src/features/auth/pages/LoginPage.tsx`
- Modify: `src/features/auth/pages/ChangePasswordPage.tsx`
- Modify: `src/features/auth/api/authApi.ts`
- Modify: `src/features/auth/types/auth.ts`
- Modify: `src/routes/AppRoutes.tsx`

- [ ] Remove prefilled admin/password values and the displayed default credential text.
- [ ] Store the fresh server token returned by change-password.
- [ ] Redirect a logged-in user with `mustChangePassword` to `/change-password` and prevent normal route use until it is cleared.
- [ ] Update the password hint to match the server policy without exposing secrets.
- [ ] Run the frontend build.

### Task 5: Configuration/documentation and final verification

**Files:**
- Modify: `deployment/.env.example`
- Modify: `deployment/README.md`

- [ ] Document `BootstrapAdmin__Password` as an optional deployment secret and state that no default admin is created when absent.
- [ ] Document the required JWT key strength and HTTPS/WSS follow-up.
- [ ] Pin patched .NET 8 dependency versions and verify the production dependency graph has no vulnerable packages.
- [ ] Run `dotnet test AuthUtility.sln --no-restore`, backend build, and frontend build from clean current source.
- [ ] Inspect `git diff` and `git status` to ensure unrelated existing untracked files were not staged or modified.
- [ ] Report completed fixes, test evidence, and remaining production actions (HTTPS, secret rotation, dependency upgrades).
