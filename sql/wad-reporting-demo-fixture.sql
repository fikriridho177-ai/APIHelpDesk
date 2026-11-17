-- OPT-IN: khusus database pengujian; semua objek diberi label demo, bukan data operasional.
INSERT INTO "WadObject" ("Module", "Code", "Name", "ObjectType", "Wilayah", "Location", "IsDemo", "ReferenceValueCm", "MeasurementMethod", "VerifyDropCm", "CriticalDropCm", "AreaHa", "TargetQty", "Unit", "TargetDate")
VALUES
('WATER', 'DEMO-PA', 'Pintu air uji', 'PINTU_AIR', 'Wilayah Demo', 'Lokasi pengujian', true, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
('PEAT', 'DEMO-SG', 'Patok uji elevasi', 'PATOK', 'Wilayah Demo', 'Lokasi pengujian', true, 100, 'ELEVATION', 2, 4, NULL, NULL, NULL, NULL),
('PEAT', 'DEMO-SG-UNKNOWN', 'Patok uji tanpa acuan', 'PATOK', 'Wilayah Demo', 'Lokasi pengujian', true, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
('FLOOD', 'DEMO-P', 'Persil uji', 'PERSIL', 'Wilayah Demo', 'Lokasi pengujian', true, NULL, NULL, NULL, NULL, 4.5, NULL, NULL, NULL),
('PROJECT', 'DEMO-PRJ', 'Projek uji kuantitas', 'PROYEK', 'Wilayah Demo', 'Lokasi pengujian', true, NULL, NULL, NULL, NULL, NULL, 100, 'm3', '2026-12-31'),
('PROJECT', 'DEMO-PRJ-MANUAL', 'Projek uji manual', 'PROYEK', 'Wilayah Demo', 'Lokasi pengujian', true, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-12-31')
ON CONFLICT ("Module", "Code") DO NOTHING;
