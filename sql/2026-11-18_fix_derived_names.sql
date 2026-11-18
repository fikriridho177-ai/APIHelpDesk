-- Normalisasi nama index/constraint/sequence turunan setelah rename tabel.
-- Aman dijalankan berulang: hanya nama yang masih mengandung prefiks salah yang diubah.
BEGIN;
SET LOCAL lock_timeout = '10s';
SELECT pg_advisory_xact_lock(20261118, 2);

CREATE TEMP TABLE tmp_scope_tables(name text) ON COMMIT DROP;
INSERT INTO tmp_scope_tables(name) VALUES
  ('UtlChatGroup'), ('UtlChatRole'), ('UtlChatMember'), ('TrnChatMessage'),
  ('TrnLynRequest'), ('MstLynJenisPermintaan'), ('HistoryLynRequestLog'),
  ('MstSnsKebun'), ('MstSnsAfdeling'), ('MstSnsBlok'), ('MstHptParameter'),
  ('TrnSnsHeader'), ('TrnSnsDetail'), ('TrnSnsFoto'), ('HistorySnsLog'),
  ('MstQcForm'), ('TrnQcInspection'), ('TrnQcApproval'), ('TrnQcFinding'),
  ('HistoryQcInspection');

CREATE TEMP TABLE tmp_name_fixes(ord integer, bad_name text, good_name text) ON COMMIT DROP;
INSERT INTO tmp_name_fixes(ord, bad_name, good_name) VALUES
  (1, 'TrnTrnHistoryHistory', 'History'),
  (2, 'TrnHistoryLynRequestLog', 'HistoryLynRequestLog'),
  (3, 'TrnTrn', 'Trn'),
  (4, 'MstMst', 'Mst'),
  (5, 'UtlUtl', 'Utl'),
  (6, 'HistoryHistory', 'History'),
  (7, 'SnsParameter', 'MstHptParameter');

-- Constraint lebih dulu: rename constraint ikut menormalkan index pkey/unique.
DO $$
DECLARE
  r record;
  f record;
  fixed text;
BEGIN
  FOR r IN
    SELECT con.conname, con.conrelid, c.relname AS tbl
    FROM pg_constraint con
    JOIN pg_class c ON c.oid = con.conrelid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public'
      AND c.relname IN (SELECT name FROM tmp_scope_tables)
  LOOP
    fixed := r.conname;
    FOR f IN SELECT bad_name, good_name FROM tmp_name_fixes ORDER BY ord
    LOOP
      fixed := replace(fixed, f.bad_name, f.good_name);
    END LOOP;

    IF fixed <> r.conname
       AND length(fixed) <= 63
       AND NOT EXISTS (
         SELECT 1 FROM pg_constraint x
         WHERE x.conrelid = r.conrelid AND x.conname = fixed
       )
       AND to_regclass(format('public.%I', fixed)) IS NULL THEN
      EXECUTE format('ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I', r.tbl, r.conname, fixed);
    END IF;
  END LOOP;
END $$;

-- Index yang tidak terikat constraint (mis. IX_*).
DO $$
DECLARE
  r record;
  f record;
  fixed text;
BEGIN
  FOR r IN
    SELECT c.relname AS name, t.relname AS tbl
    FROM pg_class c
    JOIN pg_index i ON i.indexrelid = c.oid
    JOIN pg_class t ON t.oid = i.indrelid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public'
      AND t.relname IN (SELECT name FROM tmp_scope_tables)
  LOOP
    fixed := r.name;
    FOR f IN SELECT bad_name, good_name FROM tmp_name_fixes ORDER BY ord
    LOOP
      fixed := replace(fixed, f.bad_name, f.good_name);
    END LOOP;

    IF fixed <> r.name
       AND length(fixed) <= 63
       AND to_regclass(format('public.%I', fixed)) IS NULL THEN
      EXECUTE format('ALTER INDEX public.%I RENAME TO %I', r.name, fixed);
    END IF;
  END LOOP;
END $$;

-- Sequence milik tabel scope, termasuk identity/serial.
DO $$
DECLARE
  r record;
  f record;
  fixed text;
BEGIN
  FOR r IN
    SELECT DISTINCT c.relname AS name, t.relname AS tbl
    FROM pg_class c
    JOIN pg_depend d ON d.objid = c.oid AND d.deptype IN ('a', 'i')
    JOIN pg_class t ON t.oid = d.refobjid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public'
      AND c.relkind = 'S'
      AND t.relname IN (SELECT name FROM tmp_scope_tables)
  LOOP
    fixed := r.name;
    FOR f IN SELECT bad_name, good_name FROM tmp_name_fixes ORDER BY ord
    LOOP
      fixed := replace(fixed, f.bad_name, f.good_name);
    END LOOP;

    IF fixed <> r.name
       AND length(fixed) <= 63
       AND to_regclass(format('public.%I', fixed)) IS NULL THEN
      EXECUTE format('ALTER SEQUENCE public.%I RENAME TO %I', r.name, fixed);
    END IF;
  END LOOP;
END $$;

COMMIT;
