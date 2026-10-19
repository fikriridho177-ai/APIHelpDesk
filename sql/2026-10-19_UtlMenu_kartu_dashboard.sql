-- Kartu dashboard dari database: nama lengkap, deskripsi, dan warna per menu.
-- (Kolom "Icon" sudah ada sebelumnya.)
-- Jalankan sekali di database HelpDesk (PostgreSQL).

ALTER TABLE "UtlMenu" ADD COLUMN IF NOT EXISTS "FullName"    varchar(150);
ALTER TABLE "UtlMenu" ADD COLUMN IF NOT EXISTS "Description" varchar(500);
ALTER TABLE "UtlMenu" ADD COLUMN IF NOT EXISTS "Color"       varchar(30);

-- Isi awal untuk divisi layanan (sebelumnya tertulis manual di frontend).
-- Hanya mengisi kolom yang masih kosong, supaya data yang sudah diubah admin tidak tertimpa.
UPDATE "UtlMenu" AS m
SET "FullName"    = COALESCE(m."FullName", v.full_name),
    "Description" = COALESCE(m."Description", v.description),
    "Icon"        = COALESCE(NULLIF(m."Icon", ''), v.icon),
    "Color"       = COALESCE(m."Color", v.color)
FROM (VALUES
    ('LYN_RNA', 'Research & Agronomy',
     'Sensus hama & penyakit tanaman (HPT), sensus kelapa dan nanas.', 'leaf', 'purple'),
    ('LYN_GIS', 'Geographic Information System',
     'Drone foto udara & semprot, pembuatan peta, analisis dan data spasial.', 'map', 'green'),
    ('LYN_WAD', 'Water Management',
     'Pantauan TMAT/TMAK, pintu air, gorong-gorong, subsiden gambut & curah hujan.', 'water', 'cyan'),
    ('LYN_QC', 'Quality Control',
     'Inspeksi panen & tanam, pengiriman hasil, field audit dan patok HGU.', 'check', 'teal'),
    ('LYN_NAT', 'Mekanisasi Pertanian',
     'Monitoring, riset & inovasi, serta evaluasi efektivitas alat mekanisasi.', 'tractor', 'orange')
) AS v(code, full_name, description, icon, color)
WHERE m."Code" = v.code;
