namespace AuthUtility.Domain.Entities;

public class MstDepartment
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public ICollection<UtlGroupDepartment> GroupDepartments { get; set; } = new List<UtlGroupDepartment>();
}
