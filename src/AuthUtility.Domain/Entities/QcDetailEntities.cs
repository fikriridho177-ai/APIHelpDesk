namespace AuthUtility.Domain.Entities;

/* =========================================================
   Detail typed per form QC (Fase 3+).
   Angka mentah saja; total/% dihitung di view PostgreSQL.
   ========================================================= */

// Panen Nenas (QC-PANEN-NNS, pola A) — satu baris per jalur sampel.
// Kapasitas 20 baris sesuai template 12.PANEN NENAS.xls sheet PNN NNS.
public class QcInspSampelPanenNenas
{
    public long Id { get; set; }

    public long InspectionId { get; set; }

    public QcInspection? Inspection { get; set; }

    // Nomor urut baris pada grid (1..20), stabil untuk export.
    public int RowNo { get; set; }

    public string NomorJalur { get; set; } = "";

    public int JumlahPohon { get; set; }

    public int BuahTidakDipetik { get; set; }

    public int BuahTelahDipetik { get; set; }

    public int BuahTerpotong { get; set; }

    public string? Keterangan { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}