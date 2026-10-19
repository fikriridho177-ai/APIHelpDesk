namespace AuthUtility.Domain.Entities;

public class UtlGroupCompany
{
    public int GroupId { get; set; }
    public UtlGroup? Group { get; set; }

    // External Company Code dari PKBPayroll.tblMstPerusahaan.Kode (kolom database bertipe integer)
    public int CompanyId { get; set; }
}
