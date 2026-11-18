using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Npgsql;
using NpgsqlTypes;

var repoRoot = FindRepoRoot();
string? zipPath = null;
string? extraSqlPath = null;
var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--zip" or "-z")
    {
        if (i + 1 >= args.Length) throw new ArgumentException("--zip membutuhkan path berkas ZIP sumber.");
        zipPath = Path.GetFullPath(args[++i]);
    }
    else if (args[i] is "--sql")
    {
        if (i + 1 >= args.Length) throw new ArgumentException("--sql membutuhkan path berkas SQL tambahan.");
        extraSqlPath = Path.GetFullPath(args[++i]);
    }
    else
    {
        positional.Add(args[i]);
    }
}

var dataPath = positional.Count > 0
    ? Path.GetFullPath(positional[0])
    : Path.Combine(repoRoot, "tools", "wad-import", "data.json");
var schemaPath = Path.Combine(repoRoot, "sql", "2026-11-17-wad-real-data.sql");
var appSettingsPath = Path.Combine(repoRoot, "src", "AuthUtility.Api", "appsettings.json");

if (!File.Exists(dataPath)) throw new FileNotFoundException($"JSON hasil parser tidak ditemukan: {dataPath}");
if (!File.Exists(schemaPath)) throw new FileNotFoundException($"Skema SQL tidak ditemukan: {schemaPath}");

var payload = JsonSerializer.Deserialize<ImportPayload>(
    await File.ReadAllTextAsync(dataPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("JSON impor tidak valid.");

var connectionString = ReadConnectionString(appSettingsPath, "DefaultConnection");
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

await ExecuteFileAsync(connection, schemaPath);
if (extraSqlPath != null)
{
    if (!File.Exists(extraSqlPath)) throw new FileNotFoundException($"SQL tambahan tidak ditemukan: {extraSqlPath}");
    await ExecuteFileAsync(connection, extraSqlPath);
}
await using var transaction = await connection.BeginTransactionAsync();

await UpsertMastersAsync(connection, transaction, payload.Overflow.Masters);
await UpsertLogsAsync(connection, transaction, payload.Overflow.Logs);
await UpsertSubsidenAsync(connection, transaction, payload.Subsiden.Stakes, payload.Subsiden.Observations);

object? photoCounts = null;
if (!string.IsNullOrWhiteSpace(zipPath))
{
    photoCounts = await WadPhotoImport.ImportAsync(
        connection,
        transaction,
        zipPath,
        WadPhotoImport.ResolvePhotoRoot(appSettingsPath, repoRoot),
        payload.Subsiden.Observations);
}

await transaction.CommitAsync();

var counts = await VerifyAsync(connection, zipPath != null);
Console.WriteLine(JsonSerializer.Serialize(counts, new JsonSerializerOptions { WriteIndented = true }));
if (photoCounts != null)
    Console.WriteLine(JsonSerializer.Serialize(photoCounts, new JsonSerializerOptions { WriteIndented = true }));

static void AddParameter(NpgsqlCommand command, string name, NpgsqlDbType type, object? value)
{
    var parameter = command.Parameters.Add(name, type);
    parameter.Value = value ?? DBNull.Value;
}

static async Task ExecuteFileAsync(NpgsqlConnection connection, string path)
{
    var sql = await File.ReadAllTextAsync(path);
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static async Task UpsertMastersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<Master> masters)
{
    const string sql = """
        INSERT INTO mst_wad_bangunan_air
            (kode_aset, wilayah, kanal, nama_bangunan, ukuran_dimensi, jumlah_papan, jenis_bangunan,
             ref_tmak_lokasi, ref_tpmat_lokasi, aktif, kondisi_terakhir, sort_order)
        VALUES
            (@kode_aset, @wilayah, @kanal, @nama_bangunan, @ukuran_dimensi, @jumlah_papan, @jenis_bangunan,
             @ref_tmak_lokasi, @ref_tpmat_lokasi, @aktif, @kondisi_terakhir, @sort_order)
        ON CONFLICT (kode_aset) DO UPDATE SET
            wilayah = EXCLUDED.wilayah,
            kanal = EXCLUDED.kanal,
            nama_bangunan = EXCLUDED.nama_bangunan,
            ukuran_dimensi = EXCLUDED.ukuran_dimensi,
            jumlah_papan = EXCLUDED.jumlah_papan,
            jenis_bangunan = EXCLUDED.jenis_bangunan,
            ref_tmak_lokasi = EXCLUDED.ref_tmak_lokasi,
            ref_tpmat_lokasi = EXCLUDED.ref_tpmat_lokasi,
            aktif = EXCLUDED.aktif,
            kondisi_terakhir = EXCLUDED.kondisi_terakhir,
            sort_order = EXCLUDED.sort_order,
            updated_at = now()
        """;

    await using var command = new NpgsqlCommand(sql, connection, transaction);
    AddParameter(command, "kode_aset", NpgsqlDbType.Varchar, null);
    AddParameter(command, "wilayah", NpgsqlDbType.Varchar, null);
    AddParameter(command, "kanal", NpgsqlDbType.Varchar, null);
    AddParameter(command, "nama_bangunan", NpgsqlDbType.Varchar, null);
    AddParameter(command, "ukuran_dimensi", NpgsqlDbType.Varchar, null);
    AddParameter(command, "jumlah_papan", NpgsqlDbType.Smallint, null);
    AddParameter(command, "jenis_bangunan", NpgsqlDbType.Varchar, null);
    AddParameter(command, "ref_tmak_lokasi", NpgsqlDbType.Varchar, null);
    AddParameter(command, "ref_tpmat_lokasi", NpgsqlDbType.Varchar, null);
    AddParameter(command, "aktif", NpgsqlDbType.Boolean, null);
    AddParameter(command, "kondisi_terakhir", NpgsqlDbType.Varchar, null);
    AddParameter(command, "sort_order", NpgsqlDbType.Integer, null);

    var index = 0;
    foreach (var master in masters.OrderBy(item => item.Wilayah).ThenBy(item => item.WilayahSequence))
    {
        command.Parameters["kode_aset"].Value = master.KodeAset;
        command.Parameters["wilayah"].Value = master.Wilayah;
        command.Parameters["kanal"].Value = master.Kanal;
        command.Parameters["nama_bangunan"].Value = master.NamaBangunan;
        command.Parameters["ukuran_dimensi"].Value = (object?)master.UkuranDimensi ?? DBNull.Value;
        command.Parameters["jumlah_papan"].Value = (object?)(short?)master.JumlahPapan ?? DBNull.Value;
        command.Parameters["jenis_bangunan"].Value = master.JenisBangunan;
        command.Parameters["ref_tmak_lokasi"].Value = (object?)master.RefTmakLokasi ?? DBNull.Value;
        command.Parameters["ref_tpmat_lokasi"].Value = (object?)master.RefTpmatLokasi ?? DBNull.Value;
        command.Parameters["aktif"].Value = master.Aktif;
        command.Parameters["kondisi_terakhir"].Value = (object?)master.KondisiTerakhir ?? DBNull.Value;
        command.Parameters["sort_order"].Value = ++index;
        await command.ExecuteNonQueryAsync();
    }
}

static async Task UpsertLogsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, List<LogRow> logs)
{
    const string sql = """
        INSERT INTO trx_wad_log_overflow
            (kode_aset, minggu, tanggal_log, status_operasi, detail_bukaan, kondisi_fisik, petugas_nama, pemeriksa_nama)
        VALUES
            (@kode_aset, @minggu, @tanggal_log, @status_operasi, @detail_bukaan, @kondisi_fisik, @petugas_nama, @pemeriksa_nama)
        ON CONFLICT (kode_aset, minggu, tanggal_log) DO UPDATE SET
            status_operasi = EXCLUDED.status_operasi,
            detail_bukaan = EXCLUDED.detail_bukaan,
            kondisi_fisik = EXCLUDED.kondisi_fisik,
            petugas_nama = EXCLUDED.petugas_nama,
            pemeriksa_nama = EXCLUDED.pemeriksa_nama
        """;

    await using var command = new NpgsqlCommand(sql, connection, transaction);
    AddParameter(command, "kode_aset", NpgsqlDbType.Varchar, null);
    AddParameter(command, "minggu", NpgsqlDbType.Varchar, null);
    AddParameter(command, "tanggal_log", NpgsqlDbType.Date, null);
    AddParameter(command, "status_operasi", NpgsqlDbType.Varchar, null);
    AddParameter(command, "detail_bukaan", NpgsqlDbType.Varchar, null);
    AddParameter(command, "kondisi_fisik", NpgsqlDbType.Varchar, null);
    AddParameter(command, "petugas_nama", NpgsqlDbType.Varchar, null);
    AddParameter(command, "pemeriksa_nama", NpgsqlDbType.Varchar, null);

    foreach (var log in logs)
    {
        command.Parameters["kode_aset"].Value = log.KodeAset;
        command.Parameters["minggu"].Value = log.Minggu;
        command.Parameters["tanggal_log"].Value = DateOnly.ParseExact(log.TanggalLog, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        command.Parameters["status_operasi"].Value = log.StatusOperasi;
        command.Parameters["detail_bukaan"].Value = (object?)log.DetailBukaan ?? DBNull.Value;
        command.Parameters["kondisi_fisik"].Value = (object?)log.KondisiFisik ?? DBNull.Value;
        command.Parameters["petugas_nama"].Value = (object?)log.PetugasNama ?? DBNull.Value;
        command.Parameters["pemeriksa_nama"].Value = (object?)log.PemeriksaNama ?? DBNull.Value;
        await command.ExecuteNonQueryAsync();
    }
}

static async Task UpsertSubsidenAsync(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    List<SubsidenStake> stakes,
    List<SubsidenObservation> observations)
{
    const string sql = """
        INSERT INTO trx_wad_subsiden
            (kode_patok, lintang_raw, lintang, bujur_raw, bujur, wilayah, blok, tahun_tanam,
             kedalaman_gambut, periode, periode_label, nilai_cm)
        VALUES
            (@kode_patok, @lintang_raw, @lintang, @bujur_raw, @bujur, @wilayah, @blok, @tahun_tanam,
             @kedalaman_gambut, @periode, @periode_label, @nilai_cm)
        ON CONFLICT (kode_patok, periode) DO UPDATE SET
            lintang_raw = EXCLUDED.lintang_raw,
            lintang = EXCLUDED.lintang,
            bujur_raw = EXCLUDED.bujur_raw,
            bujur = EXCLUDED.bujur,
            wilayah = EXCLUDED.wilayah,
            blok = EXCLUDED.blok,
            tahun_tanam = EXCLUDED.tahun_tanam,
            kedalaman_gambut = EXCLUDED.kedalaman_gambut,
            periode_label = EXCLUDED.periode_label,
            nilai_cm = EXCLUDED.nilai_cm
        """;

    await using var command = new NpgsqlCommand(sql, connection, transaction);
    AddParameter(command, "kode_patok", NpgsqlDbType.Varchar, null);
    AddParameter(command, "lintang_raw", NpgsqlDbType.Varchar, null);
    AddParameter(command, "lintang", NpgsqlDbType.Numeric, null);
    AddParameter(command, "bujur_raw", NpgsqlDbType.Varchar, null);
    AddParameter(command, "bujur", NpgsqlDbType.Numeric, null);
    AddParameter(command, "wilayah", NpgsqlDbType.Varchar, null);
    AddParameter(command, "blok", NpgsqlDbType.Varchar, null);
    AddParameter(command, "tahun_tanam", NpgsqlDbType.Integer, null);
    AddParameter(command, "kedalaman_gambut", NpgsqlDbType.Varchar, null);
    AddParameter(command, "periode", NpgsqlDbType.Varchar, null);
    AddParameter(command, "periode_label", NpgsqlDbType.Varchar, null);
    AddParameter(command, "nilai_cm", NpgsqlDbType.Numeric, null);

    var stakeByCode = stakes.ToDictionary(item => item.KodePatok, StringComparer.OrdinalIgnoreCase);
    foreach (var observation in observations)
    {
        if (!stakeByCode.TryGetValue(observation.KodePatok, out var stake))
            throw new InvalidOperationException($"Metadata patok {observation.KodePatok} tidak ditemukan.");

        command.Parameters["kode_patok"].Value = observation.KodePatok;
        command.Parameters["lintang_raw"].Value = (object?)stake.LintangRaw ?? DBNull.Value;
        command.Parameters["lintang"].Value = (object?)stake.Lintang ?? DBNull.Value;
        command.Parameters["bujur_raw"].Value = (object?)stake.BujurRaw ?? DBNull.Value;
        command.Parameters["bujur"].Value = (object?)stake.Bujur ?? DBNull.Value;
        command.Parameters["wilayah"].Value = stake.Wilayah;
        command.Parameters["blok"].Value = stake.Blok;
        command.Parameters["tahun_tanam"].Value = (object?)stake.TahunTanam ?? DBNull.Value;
        command.Parameters["kedalaman_gambut"].Value = (object?)stake.KedalamanGambut ?? DBNull.Value;
        command.Parameters["periode"].Value = observation.Periode;
        command.Parameters["periode_label"].Value = observation.PeriodeLabel;
        command.Parameters["nilai_cm"].Value = observation.NilaiCm;
        await command.ExecuteNonQueryAsync();
    }
}

static async Task<object> VerifyAsync(NpgsqlConnection connection, bool expectPhotos)
{
    async Task<long> Scalar(string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    var masters = await Scalar("SELECT count(*) FROM mst_wad_bangunan_air");
    var active = await Scalar("SELECT count(*) FROM mst_wad_bangunan_air WHERE aktif");
    var logs = await Scalar("SELECT count(*) FROM trx_wad_log_overflow");
    var subsiden = await Scalar("SELECT count(*) FROM trx_wad_subsiden");
    var overflowPhotos = await Scalar("SELECT count(*) FROM trx_wad_log_overflow_foto WHERE file_url LIKE 'overflow/import/%'");
    var subsidenPhotos = await Scalar("SELECT count(*) FROM trx_wad_subsiden_foto");
    var attention = await Scalar("""
        SELECT count(*) FROM (
            SELECT DISTINCT ON (kode_aset) kode_aset, kondisi_fisik
            FROM trx_wad_log_overflow
            ORDER BY kode_aset, tanggal_log DESC, id DESC
        ) latest
        WHERE kondisi_fisik IN (
            'Rusak Parah', 'Kurang baik', 'Dalam tahap perbaikan',
            'Kebocoran semakin besar', 'Longsor tanah samping dan jebol dinding overflow')
        """);
    var latestDate = await Scalar("SELECT count(*) FROM (SELECT max(tanggal_log) FROM trx_wad_log_overflow) x");

    if (masters != 17) throw new InvalidOperationException($"Master bangunan {masters}, harus 17 (16 aktif + KM 00).");
    if (active != 16) throw new InvalidOperationException($"Bangunan aktif {active}, harus 16.");
    if (logs != 5152) throw new InvalidOperationException($"Log overflow {logs}, harus 5152.");
    if (subsiden != 16) throw new InvalidOperationException($"Observasi subsiden {subsiden}, harus 16.");
    if (attention != 6) throw new InvalidOperationException($"Perlu perhatian {attention}, harus 6.");
    if (latestDate == 0) throw new InvalidOperationException("Tanggal log terbaru tidak ditemukan.");
    if (expectPhotos)
    {
        if (overflowPhotos != 736) throw new InvalidOperationException($"Foto overflow hasil impor {overflowPhotos}, harus 736 (16 unit x 46 minggu).");
        if (subsidenPhotos != 12) throw new InvalidOperationException($"Foto subsiden {subsidenPhotos}, harus 12 (4 patok x 3 periode terdokumentasi).");
    }

    return new
    {
        masters,
        active,
        logs,
        subsiden,
        attention,
        overflowPhotos,
        subsidenPhotos,
        latestM46Condition = "6 aset sesuai M46 9-15 November 2026",
    };
}

static string ReadConnectionString(string appSettingsPath, string name)
{
    using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
    return document.RootElement.GetProperty("ConnectionStrings").GetProperty(name).GetString()
        ?? throw new InvalidOperationException($"Connection string {name} kosong.");
}

static string FindRepoRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "AuthUtility.sln"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Root APIHelpDesk tidak ditemukan.");
}

// Foto tertanam pada workbook sumber (drawing OOXML) disalin ke folder unggahan
// lalu dicatat per unit-minggu (overflow) dan per patok-periode (subsiden).
static class WadPhotoImport
{
    private const string OverflowWorkbook = "2026_Laporan Buka-Tutup Overflow.xlsx";
    private const string SubsidenWorkbook = "September_Subsiden 2026.xlsx";
    private static readonly Regex SheetNamePattern = new("^M\\d{2}$", RegexOptions.CultureInvariant);
    private static readonly Regex PeriodePattern = new(@"Periode\s+(I{1,3})\b", RegexOptions.CultureInvariant);
    private static readonly Regex PatokPattern = new(@"(\d{2}-\d{2})\.\s*(\d+/\d+)", RegexOptions.CultureInvariant);
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static string ResolvePhotoRoot(string appSettingsPath, string repoRoot)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        var configured = document.RootElement.TryGetProperty("Wad", out var wad)
            && wad.TryGetProperty("Uploads", out var uploads)
            && uploads.TryGetProperty("Path", out var path)
            ? path.GetString()
            : null;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(repoRoot, "src", "AuthUtility.Api", "App_Data", "wad-photos")
            : Path.GetFullPath(configured);
    }

    public static async Task<object> ImportAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string zipPath,
        string photoRoot,
        List<SubsidenObservation> observations)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException($"ZIP sumber tidak ditemukan: {zipPath}");

        using var outer = ZipFile.OpenRead(zipPath);
        var overflowEntry = outer.Entries.FirstOrDefault(entry => entry.Name.Equals(OverflowWorkbook, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Workbook {OverflowWorkbook} tidak ditemukan di ZIP.");
        var subsidenEntry = outer.Entries.FirstOrDefault(entry => entry.Name.Equals(SubsidenWorkbook, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Workbook {SubsidenWorkbook} tidak ditemukan di ZIP.");

        using var overflowBook = new OoxmlBook(ReadEntryBytes(overflowEntry));
        using var subsidenBook = new OoxmlBook(ReadEntryBytes(subsidenEntry));

        var overflowPhotos = await ImportOverflowAsync(connection, transaction, overflowBook.Archive, photoRoot);
        var subsidenPhotos = await ImportSubsidenAsync(connection, transaction, subsidenBook.Archive, photoRoot, observations);
        return new { overflowPhotos, subsidenPhotos };
    }

    private static async Task<int> ImportOverflowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ZipArchive zip,
        string photoRoot)
    {
        var buildings = await ReadActiveBuildingsAsync(connection, transaction);
        if (buildings.Count != 16) throw new InvalidOperationException($"Bangunan aktif {buildings.Count}, harus 16 untuk pemetaan foto.");
        var lastLogs = await ReadLastLogIdsAsync(connection, transaction);
        var shared = ReadSharedStrings(zip);
        var sheets = ReadSheets(zip)
            .Where(sheet => SheetNamePattern.IsMatch(sheet.Name))
            .OrderBy(sheet => sheet.Name, StringComparer.Ordinal)
            .ToList();
        if (sheets.Count != 46) throw new InvalidOperationException($"Sheet mingguan {sheets.Count}, harus 46.");

        var inserted = 0;
        foreach (var sheet in sheets)
        {
            var cells = ReadCells(zip, sheet.Part, shared);
            var blockRows = Enumerable.Range(7, 200)
                .Where(row => !string.IsNullOrWhiteSpace(cells.Get(row, "C")) && !string.IsNullOrWhiteSpace(cells.Get(row, "D")))
                .ToList();
            if (blockRows.Count != buildings.Count)
                throw new InvalidOperationException($"{sheet.Name}: blok bangunan {blockRows.Count}, harus {buildings.Count}.");

            var anchors = ReadAnchors(zip, sheet.Part)
                .Where(anchor => anchor.MediaPart != null && anchor.Col == 5 && anchor.Row > 0)
                .ToList();
            if (anchors.Count != buildings.Count)
                throw new InvalidOperationException($"{sheet.Name}: foto {anchors.Count}, harus {buildings.Count}.");

            foreach (var anchor in anchors)
            {
                var blockRow = anchor.Row + 1;
                var index = blockRows.IndexOf(blockRow);
                if (index < 0)
                    throw new InvalidOperationException($"{sheet.Name}: foto baris {blockRow} tidak cocok dengan blok bangunan.");

                var building = buildings[index];
                if (!lastLogs.TryGetValue((building.KodeAset, sheet.Name), out var logId)) continue;

                var (relativePath, _) = SavePhoto(zip, photoRoot, $"overflow/import/{Slug(building.KodeAset)}", anchor.MediaPart!);
                inserted += await InsertOverflowPhotoAsync(
                    connection, transaction, logId, relativePath,
                    $"Dokumentasi {sheet.Name} — {building.NamaBangunan}");
            }
        }
        return inserted;
    }

    private static async Task<int> ImportSubsidenAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ZipArchive zip,
        string photoRoot,
        List<SubsidenObservation> observations)
    {
        var shared = ReadSharedStrings(zip);
        var inserted = 0;
        var slots = new HashSet<(string KodePatok, string Periode)>();

        foreach (var sheet in ReadSheets(zip))
        {
            var anchors = ReadAnchors(zip, sheet.Part);
            var photos = anchors.Where(anchor => anchor.MediaPart != null && anchor.Row > 3 && anchor.Col is 1 or 2 or 10).ToList();
            if (photos.Count == 0) continue;

            var cells = ReadCells(zip, sheet.Part, shared);
            var headers = cells.All
                .Where(cell => PeriodePattern.IsMatch(cell.Value))
                .Select(cell => new { cell.Key.Row, Roman = RomanValue(PeriodePattern.Match(cell.Value).Groups[1].Value) })
                .GroupBy(cell => cell.Row)
                .Select(group => group.First())
                .OrderBy(cell => cell.Row)
                .ToList();
            var labels = anchors.Where(anchor => anchor.MediaPart == null && anchor.Text.Contains(':')).ToList();

            foreach (var photo in photos)
            {
                var label = labels
                    .Where(candidate => candidate.Col == photo.Col && candidate.Row <= photo.Row && photo.Row - candidate.Row <= 4)
                    .OrderByDescending(candidate => candidate.Row)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException($"{sheet.Name}: label patok untuk foto baris {photo.Row + 1} tidak ditemukan.");
                var match = PatokPattern.Match(Regex.Replace(label.Text, @"\s+", " "));
                if (!match.Success)
                    throw new InvalidOperationException($"{sheet.Name}: label '{label.Text}' tidak memuat kode patok.");
                var kodePatok = $"{match.Groups[1].Value}. {match.Groups[2].Value}";
                var observation = observations.FirstOrDefault(item => item.KodePatok == kodePatok)
                    ?? throw new InvalidOperationException($"Kode patok '{kodePatok}' tidak ada pada data subsiden.");

                var header = headers.LastOrDefault(cell => cell.Row <= photo.Row)
                    ?? throw new InvalidOperationException($"{sheet.Name}: periode untuk foto baris {photo.Row + 1} tidak ditemukan.");
                var periode = ResolvePeriode(observations, header.Roman);

                var (relativePath, _) = SavePhoto(zip, photoRoot, $"subsiden/import/{Slug(kodePatok)}", photo.MediaPart!);
                if (!slots.Add((kodePatok, periode))) continue;
                inserted += await InsertSubsidenPhotoAsync(
                    connection, transaction, kodePatok, periode, relativePath,
                    $"Dokumentasi {observation.PeriodeLabel}");
            }
        }

        if (slots.Count != 12)
            throw new InvalidOperationException($"Slot foto subsiden {slots.Count}, harus 12 (4 patok x 3 periode).");
        return inserted;
    }

    private static async Task<int> InsertOverflowPhotoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long logId,
        string fileUrl,
        string keterangan)
    {
        const string sql = """
            INSERT INTO trx_wad_log_overflow_foto (log_id, file_url, keterangan)
            SELECT @log_id, @file_url, @keterangan
            WHERE NOT EXISTS (
                SELECT 1 FROM trx_wad_log_overflow_foto
                WHERE log_id = @log_id AND file_url = @file_url)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("log_id", logId);
        command.Parameters.AddWithValue("file_url", fileUrl);
        command.Parameters.AddWithValue("keterangan", keterangan);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> InsertSubsidenPhotoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string kodePatok,
        string periode,
        string fileUrl,
        string keterangan)
    {
        const string sql = """
            INSERT INTO trx_wad_subsiden_foto (kode_patok, periode, file_url, keterangan, sort_order)
            SELECT @kode_patok, @periode, @file_url, @keterangan, 0
            WHERE NOT EXISTS (
                SELECT 1 FROM trx_wad_subsiden_foto
                WHERE kode_patok = @kode_patok AND periode = @periode AND sort_order = 0)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("kode_patok", kodePatok);
        command.Parameters.AddWithValue("periode", periode);
        command.Parameters.AddWithValue("file_url", fileUrl);
        command.Parameters.AddWithValue("keterangan", keterangan);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<(string KodeAset, string NamaBangunan)>> ReadActiveBuildingsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        const string sql = """
            SELECT kode_aset, nama_bangunan
            FROM mst_wad_bangunan_air
            WHERE aktif
            ORDER BY sort_order, kode_aset
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var buildings = new List<(string, string)>();
        while (await reader.ReadAsync()) buildings.Add((reader.GetString(0), reader.GetString(1)));
        return buildings;
    }

    private static async Task<Dictionary<(string KodeAset, string Minggu), long>> ReadLastLogIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        const string sql = """
            SELECT DISTINCT ON (kode_aset, minggu) kode_aset, minggu, id
            FROM trx_wad_log_overflow
            ORDER BY kode_aset, minggu, tanggal_log DESC, id DESC
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var map = new Dictionary<(string, string), long>();
        while (await reader.ReadAsync()) map[(reader.GetString(0), reader.GetString(1))] = reader.GetInt64(2);
        return map;
    }

    private static string ResolvePeriode(List<SubsidenObservation> observations, int roman)
    {
        var month = roman switch
        {
            1 => "Maret",
            2 => "Juni",
            3 => "September",
            _ => throw new InvalidOperationException($"Periode romawi {roman} tidak dikenal."),
        };
        return observations.FirstOrDefault(item => item.PeriodeLabel.Contains(month, StringComparison.OrdinalIgnoreCase))?.Periode
            ?? throw new InvalidOperationException($"Periode {month} tidak ditemukan pada observasi subsiden.");
    }

    private static int RomanValue(string value) => value switch
    {
        "I" => 1,
        "II" => 2,
        "III" => 3,
        _ => 0,
    };

    private static (string RelativePath, string Extension) SavePhoto(
        ZipArchive zip,
        string photoRoot,
        string folder,
        string mediaPart)
    {
        var entry = zip.GetEntry(mediaPart)
            ?? throw new InvalidOperationException($"Media {mediaPart} tidak ditemukan di workbook.");
        var bytes = ReadEntryBytes(entry);
        var extension = Path.GetExtension(mediaPart).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()[..16];
        var relativePath = $"{folder}/{hash}{extension}";
        var fullPath = Path.Combine(photoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, bytes);
        }
        return (relativePath, extension);
    }

    private static string Slug(string value) =>
        Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static List<SheetPart> ReadSheets(ZipArchive zip)
    {
        var workbook = LoadXml(zip, "xl/workbook.xml");
        var relationships = ReadRelationships(zip, "xl/_rels/workbook.xml.rels")
            .ToDictionary(relationship => relationship.Id, StringComparer.Ordinal);
        return workbook.Descendants().Where(element => element.Name.LocalName == "sheet")
            .Select(element =>
            {
                var name = ((string?)element.Attribute("name") ?? "").Trim();
                var relationshipId = (string?)element.Attribute(RelNs + "id");
                var part = relationshipId != null && relationships.TryGetValue(relationshipId, out var relationship)
                    ? NormalizePart("xl", relationship.Target)
                    : "";
                return new SheetPart(name, part);
            })
            .Where(sheet => sheet.Name.Length > 0 && sheet.Part.Length > 0)
            .ToList();
    }

    private static List<OoxmlRelationship> ReadRelationships(ZipArchive zip, string part)
    {
        if (zip.GetEntry(part) == null) return new List<OoxmlRelationship>();
        var document = LoadXml(zip, part);
        return document.Descendants().Where(element => element.Name.LocalName == "Relationship")
            .Select(element => new OoxmlRelationship(
                (string?)element.Attribute("Id") ?? "",
                (string?)element.Attribute("Target") ?? "",
                (string?)element.Attribute("Type") ?? ""))
            .Where(relationship => relationship.Id.Length > 0 && relationship.Target.Length > 0)
            .ToList();
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        if (zip.GetEntry("xl/sharedStrings.xml") is not { } entry) return new List<string>();
        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants().Where(element => element.Name.LocalName == "si")
            .Select(item => string.Concat(item.Descendants()
                .Where(text => text.Name.LocalName == "t" && text.Ancestors().All(ancestor => ancestor.Name.LocalName != "rPh"))
                .Select(text => text.Value)))
            .ToList();
    }

    private static CellReader ReadCells(ZipArchive zip, string part, List<string> shared)
    {
        using var stream = zip.GetEntry(part)?.Open()
            ?? throw new InvalidOperationException($"Sheet {part} tidak ditemukan di workbook.");
        var document = XDocument.Load(stream);
        var cells = new Dictionary<(int Row, string Column), string>();
        foreach (var cell in document.Descendants().Where(element => element.Name.LocalName == "c"))
        {
            var reference = (string?)cell.Attribute("r");
            if (string.IsNullOrEmpty(reference)) continue;
            if (SplitCellReference(reference) is not { } cellReference) continue;
            var (row, column) = cellReference;

            var type = (string?)cell.Attribute("t");
            string? value = null;
            if (type == "s")
            {
                var raw = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    && index >= 0 && index < shared.Count)
                    value = shared[index];
            }
            else if (type == "inlineStr")
            {
                value = string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
            }
            else
            {
                value = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value;
            }

            if (!string.IsNullOrWhiteSpace(value)) cells[(row, column)] = value.Trim();
        }
        return new CellReader(cells);
    }

    private static List<DrawingAnchor> ReadAnchors(ZipArchive zip, string sheetPart)
    {
        var sheetDir = sheetPart[..sheetPart.LastIndexOf('/')];
        var drawingParts = ReadRelationships(zip, $"{sheetDir}/_rels/{Path.GetFileName(sheetPart)}.rels")
            .Where(relationship => relationship.Type.EndsWith("/drawing", StringComparison.Ordinal))
            .Select(relationship => NormalizePart(sheetDir, relationship.Target))
            .ToList();

        var anchors = new List<DrawingAnchor>();
        foreach (var drawingPart in drawingParts)
        {
            if (zip.GetEntry(drawingPart) == null) continue;
            var drawingDir = drawingPart[..drawingPart.LastIndexOf('/')];
            var drawingRels = ReadRelationships(zip, $"{drawingDir}/_rels/{Path.GetFileName(drawingPart)}.rels")
                .ToDictionary(relationship => relationship.Id, StringComparer.Ordinal);
            var document = LoadXml(zip, drawingPart);

            foreach (var anchor in document.Descendants()
                .Where(element => element.Name.LocalName is "twoCellAnchor" or "oneCellAnchor" or "absoluteAnchor"))
            {
                var from = anchor.Descendants().FirstOrDefault(element => element.Name.LocalName == "from");
                if (from == null) continue;
                if (!int.TryParse(
                    from.Descendants().FirstOrDefault(element => element.Name.LocalName == "row")?.Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)) continue;
                if (!int.TryParse(
                    from.Descendants().FirstOrDefault(element => element.Name.LocalName == "col")?.Value,
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out var column)) continue;

                var blip = anchor.Descendants().FirstOrDefault(element => element.Name.LocalName == "blip");
                var embed = (string?)blip?.Attribute(RelNs + "embed");
                var mediaPart = embed != null && drawingRels.TryGetValue(embed, out var relationship)
                    ? NormalizePart(drawingDir, relationship.Target)
                    : null;
                var text = string.Concat(anchor.Descendants()
                    .Where(element => element.Name.LocalName == "t")
                    .Select(element => element.Value));
                anchors.Add(new DrawingAnchor(row, column, mediaPart, text));
            }
        }
        return anchors;
    }

    private static XDocument LoadXml(ZipArchive zip, string part)
    {
        var entry = zip.GetEntry(part)
            ?? throw new InvalidOperationException($"Bagian OOXML {part} tidak ditemukan.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string NormalizePart(string basePart, string target)
    {
        var normalized = target.Replace('\\', '/');
        var segments = new List<string>();
        if (!normalized.StartsWith('/'))
            segments.AddRange(basePart.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var segment in normalized.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return string.Join('/', segments);
    }

    private static (int Row, string Column)? SplitCellReference(string reference)
    {
        var index = 0;
        while (index < reference.Length && char.IsLetter(reference[index])) index++;
        if (index == 0 || index == reference.Length) return null;
        if (!int.TryParse(reference[index..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)) return null;
        return (row, reference[..index].ToUpperInvariant());
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record SheetPart(string Name, string Part);

    private sealed record OoxmlRelationship(string Id, string Target, string Type);

    private sealed record DrawingAnchor(int Row, int Col, string? MediaPart, string Text);

    private sealed class CellReader(Dictionary<(int Row, string Column), string> cells)
    {
        public string? Get(int row, string column) => cells.TryGetValue((row, column), out var value) ? value : null;

        public IEnumerable<KeyValuePair<(int Row, string Column), string>> All => cells;
    }

    private sealed class OoxmlBook : IDisposable
    {
        private readonly MemoryStream stream;

        public OoxmlBook(byte[] bytes)
        {
            stream = new MemoryStream(bytes);
            Archive = new ZipArchive(stream, ZipArchiveMode.Read);
        }

        public ZipArchive Archive { get; }

        public void Dispose()
        {
            Archive.Dispose();
            stream.Dispose();
        }
    }
}

sealed class ImportPayload
{
    public OverflowPayload Overflow { get; set; } = new();
    public SubsidenPayload Subsiden { get; set; } = new();
}

sealed class OverflowPayload
{
    public List<Master> Masters { get; set; } = new();
    public List<LogRow> Logs { get; set; } = new();
}

sealed class Master
{
    public string KodeAset { get; set; } = "";
    public string Wilayah { get; set; } = "";
    public int WilayahSequence { get; set; }
    public string Kanal { get; set; } = "";
    public string NamaBangunan { get; set; } = "";
    public string? UkuranDimensi { get; set; }
    public int? JumlahPapan { get; set; }
    public string JenisBangunan { get; set; } = "OVERFLOW";
    public bool Aktif { get; set; }
    public string? RefTmakLokasi { get; set; }
    public string? RefTpmatLokasi { get; set; }
    public string? KondisiTerakhir { get; set; }
}

sealed class LogRow
{
    public string KodeAset { get; set; } = "";
    public string Minggu { get; set; } = "";
    public string TanggalLog { get; set; } = "";
    public string StatusOperasi { get; set; } = "";
    public string? DetailBukaan { get; set; }
    public string? KondisiFisik { get; set; }
    public string? PetugasNama { get; set; }
    public string? PemeriksaNama { get; set; }
}

sealed class SubsidenPayload
{
    public List<SubsidenStake> Stakes { get; set; } = new();
    public List<SubsidenObservation> Observations { get; set; } = new();
}

sealed class SubsidenStake
{
    public string KodePatok { get; set; } = "";
    public string? LintangRaw { get; set; }
    public decimal? Lintang { get; set; }
    public string? BujurRaw { get; set; }
    public decimal? Bujur { get; set; }
    public string Wilayah { get; set; } = "";
    public string Blok { get; set; } = "";
    public int? TahunTanam { get; set; }
    public string? KedalamanGambut { get; set; }
}

sealed class SubsidenObservation
{
    public string KodePatok { get; set; } = "";
    public string Periode { get; set; } = "";
    public string PeriodeLabel { get; set; } = "";
    public decimal NilaiCm { get; set; }
    [JsonIgnore] public SubsidenStake? Stake { get; set; }
}
