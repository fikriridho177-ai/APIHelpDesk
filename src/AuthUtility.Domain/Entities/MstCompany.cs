namespace AuthUtility.Domain.Entities;

public class MstCompany
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public ICollection<UtlGroupCompany> GroupCompanies { get; set; } = new List<UtlGroupCompany>();
}
