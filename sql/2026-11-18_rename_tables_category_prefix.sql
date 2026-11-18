-- Rename tabel domain HelpDesk-ASD ke prefiks kategori.
-- Cakupan: Chat, Layanan, Sensus/RNA, dan QC.
-- Dikecualikan: seluruh objek WAD dan IoT.
-- Idempotent: objek yang sudah memakai nama baru dilewati.
BEGIN;
SET LOCAL lock_timeout = '10s';
SELECT pg_advisory_xact_lock(20261118, 1);

CREATE TEMP TABLE tmp_renamed_pairs(old_name text, new_name text) ON COMMIT DROP;

-- View kompatibilitas lama harus dilepas agar nama MstHptParameter dapat dipakai tabel.
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public' AND c.relname = 'MstHptParameter' AND c.relkind = 'v'
  ) THEN
    EXECUTE 'DROP VIEW public."MstHptParameter"';
  END IF;
END $$;

DO $$
DECLARE
  r record;
  kind "char";
BEGIN
  FOR r IN
    SELECT * FROM (VALUES
      ('ChatGroup',          'UtlChatGroup'),
      ('ChatRole',           'UtlChatRole'),
      ('ChatMember',         'UtlChatMember'),
      ('ChatMessage',        'TrnChatMessage'),
      ('LynRequest',         'TrnLynRequest'),
      ('LynJenisPermintaan', 'MstLynJenisPermintaan'),
      ('LynRequestLog',      'HistoryLynRequestLog'),
      ('SnsKebun',           'MstSnsKebun'),
      ('SnsAfdeling',        'MstSnsAfdeling'),
      ('SnsBlok',            'MstSnsBlok'),
      ('MstParameterHpt',    'MstHptParameter'),
      ('SnsHeader',          'TrnSnsHeader'),
      ('SnsDetail',          'TrnSnsDetail'),
      ('SnsFoto',            'TrnSnsFoto'),
      ('SnsLog',             'HistorySnsLog'),
      ('QcForm',             'MstQcForm'),
      ('QcInspection',       'TrnQcInspection'),
      ('QcApproval',         'TrnQcApproval'),
      ('QcFinding',          'TrnQcFinding'),
      ('QcHistory',          'HistoryQcInspection')
    ) AS v(old_name, new_name)
  LOOP
    SELECT c.relkind INTO kind
    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public' AND c.relname = r.old_name
      AND c.relkind IN ('r', 'p', 'v', 'm');

    IF kind IS NULL THEN
      IF EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname = r.new_name
          AND c.relkind IN ('r', 'p', 'v', 'm')
      ) THEN
        RAISE NOTICE 'Lewati %: sudah memakai nama %', r.old_name, r.new_name;
      ELSE
        RAISE NOTICE 'Lewati %: tidak ditemukan', r.old_name;
      END IF;
      CONTINUE;
    END IF;

    IF EXISTS (
      SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'public' AND c.relname = r.new_name
        AND c.relkind IN ('r', 'p', 'v', 'm')
    ) THEN
      RAISE EXCEPTION 'Rename dibatalkan: % dan % sama-sama ada', r.old_name, r.new_name;
    END IF;

    CASE
      WHEN kind IN ('r', 'p') THEN
        EXECUTE format('ALTER TABLE public.%I RENAME TO %I', r.old_name, r.new_name);
      WHEN kind = 'v' THEN
        EXECUTE format('ALTER VIEW public.%I RENAME TO %I', r.old_name, r.new_name);
      WHEN kind = 'm' THEN
        EXECUTE format('ALTER MATERIALIZED VIEW public.%I RENAME TO %I', r.old_name, r.new_name);
      ELSE
        RAISE EXCEPTION 'Objek % bertipe % dan tidak dapat di-rename otomatis', r.old_name, kind;
    END CASE;

    INSERT INTO tmp_renamed_pairs(old_name, new_name) VALUES (r.old_name, r.new_name);
  END LOOP;
END $$;

-- Nama sequence/index/constraint tidak ikut berubah saat tabel di-rename.
-- Ganti nama objek turunan secara best-effort agar konsisten dengan nama tabel baru.
DO $$
DECLARE
  r record;
  obj record;
  new_name text;
BEGIN
  FOR r IN SELECT p.old_name, p.new_name FROM tmp_renamed_pairs p
  LOOP
    IF NOT EXISTS (
      SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'public' AND c.relname = r.new_name AND c.relkind IN ('r', 'p')
    ) THEN
      CONTINUE;
    END IF;

    FOR obj IN
      SELECT c.relname AS name
      FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'public' AND c.relkind = 'S'
        AND position(r.old_name in c.relname) > 0
        AND position(r.new_name in c.relname) = 0
    LOOP
      new_name := replace(obj.name, r.old_name, r.new_name);
      IF length(new_name) <= 63 AND to_regclass(format('public.%I', new_name)) IS NULL THEN
        EXECUTE format('ALTER SEQUENCE public.%I RENAME TO %I', obj.name, new_name);
      END IF;
    END LOOP;

    FOR obj IN
      SELECT c.relname AS name
      FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'public' AND c.relkind IN ('i', 'I')
        AND position(r.old_name in c.relname) > 0
        AND position(r.new_name in c.relname) = 0
    LOOP
      new_name := replace(obj.name, r.old_name, r.new_name);
      IF length(new_name) <= 63 AND to_regclass(format('public.%I', new_name)) IS NULL THEN
        EXECUTE format('ALTER INDEX public.%I RENAME TO %I', obj.name, new_name);
      END IF;
    END LOOP;

    FOR obj IN
      SELECT con.conname AS name, con.conrelid
      FROM pg_constraint con
      JOIN pg_class c ON c.oid = con.conrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'public'
        AND position(r.old_name in con.conname) > 0
        AND position(r.new_name in con.conname) = 0
    LOOP
      new_name := replace(obj.name, r.old_name, r.new_name);
      IF new_name <> obj.name AND length(new_name) <= 63 AND NOT EXISTS (
        SELECT 1 FROM pg_constraint x WHERE x.conrelid = obj.conrelid AND x.conname = new_name
      ) THEN
        EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT %I TO %I', obj.conrelid::regclass, obj.name, new_name);
      END IF;
    END LOOP;
  END LOOP;
END $$;

COMMIT;
