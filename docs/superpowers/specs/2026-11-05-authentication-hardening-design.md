# Authentication Hardening Design

## Intent

Menutup risiko authentication dan authorization tertinggi pada MyHelpDesk tanpa
memutus deployment production yang saat ini masih menggunakan Bearer JWT dan
frontend yang membaca token dari localStorage.

## Goals

- Tidak ada password default yang tertanam di source code atau ditampilkan di UI.
- Bootstrap administrator hanya dibuat bila password diberikan melalui konfigurasi
  deployment.
- Password baru memenuhi policy minimum dan tidak boleh memakai ulang hash password
  sebelumnya.
- User nonaktif, group nonaktif, dan token dari password lama tidak lagi diterima.
- User wajib mengganti password sebelum menggunakan endpoint aplikasi lain.
- Endpoint pengelolaan group, menu, dan security hanya dapat diubah oleh
  super-admin server-side.
- Endpoint lookup PKBPayroll hanya dapat dipanggil oleh user yang memiliki akses
  `MASTER_USER`.
- Error database eksternal tidak membocorkan exception detail ke client.

## Non-goals

- Tidak merotasi credential database production secara otomatis.
- Tidak mengubah domain/DNS atau menyalakan HTTPS tanpa keputusan domain dan
  certificate di Dokploy.
- Tidak melakukan migrasi penuh dari localStorage Bearer JWT ke cookie HttpOnly dan
  refresh-token dalam tahap ini.

## Approaches

1. **Compatibility-first hardening (dipilih).** Mempertahankan kontrak login dan
   Bearer JWT, lalu menambah validasi server, claim perubahan password, middleware
   wajib ganti password, expiry pendek, serta guard authorization. Dampaknya paling
   kecil terhadap deployment aktif.
2. **Full session migration.** Mengganti sesi menjadi cookie HttpOnly dengan
   refresh-token rotation dan mewajibkan HTTPS. Lebih kuat terhadap pencurian token
   XSS, tetapi membutuhkan perubahan frontend, reverse proxy, domain/TLS, dan proses
   migrasi sesi.
3. **Frontend-only validation.** Menambah validasi di form tanpa guard server-side.
   Ditolak karena request dapat dikirim langsung ke API dan tidak menyelesaikan
   privilege escalation.

## Constraints and decisions

- JWT access token berlaku 30 menit pada tahap ini.
- Claim token menyimpan waktu perubahan password; token lama menjadi tidak valid
  setelah password diganti.
- `BootstrapAdmin:Password` bersifat optional; bila kosong, startup tidak membuat
  admin baru.
- Password baru minimal 12 karakter, maksimal 128 karakter, serta harus memiliki
  huruf besar, huruf kecil, angka, dan simbol.
- Mutasi group/menu/security memakai status `UtlGroup.IsSuperAdmin`, bukan hanya
  route atau tombol frontend.

## Remaining follow-up

- Aktifkan HTTPS/WSS pada domain production dan pindahkan token ke cookie HttpOnly
  setelah domain/certificate diputuskan.
- Rotasi secret production melalui secret manager/Dokploy.
