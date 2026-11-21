using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Qc;

// Mesin transisi status inspeksi QC. Semua transisi penting lewat sini agar
// aturan terpusat (bukan tersebar di controller/frontend). State-less.
public class QcStatusEngine
{
    // Boleh edit field umum / DataJson (oleh pembuat): DRAFT & REVISION.
    public bool CanEdit(string status) => QcInspectionStatus.Editable.Contains(status);

    public bool CanSubmit(string status) => status == QcInspectionStatus.Draft;

    // Boleh approve / request-revision: harus sedang menunggu approval.
    public bool CanApprove(string status) => QcInspectionStatus.PendingApproval.Contains(status);

    // Boleh resubmit (oleh pembuat) setelah dikembalikan untuk revisi.
    public bool CanResubmit(string status) => status == QcInspectionStatus.Revision;

    // Boleh membuat finding (temuan = bagian hasil pemeriksaan).
    public bool CanCreateFinding(string status) => QcInspectionStatus.FindingCreatable.Contains(status);

    // Boleh CLOSE inspection: status APPROVED + seluruh finding sudah VERIFIED.
    public bool CanClose(QcInspection x, IEnumerable<QcFinding> findings)
    {
        if (x.Status != QcInspectionStatus.Approved) return false;
        return !findings.Any(f => QcFindingStatus.Active.Contains(f.Status));
    }
}