namespace AuthUtility.Domain.Entities;

public class UtlGroup
{
    public int Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public bool IsSuperAdmin { get; set; }

    public bool AllCompanies { get; set; }

    public bool AllDepartments { get; set; }

    public bool AllItemSubCategories { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<UtlUser> Users { get; set; } = new List<UtlUser>();

    public ICollection<UtlGroupMenu> GroupMenus { get; set; } = new List<UtlGroupMenu>();

    public ICollection<UtlGroupCompany> GroupCompanies { get; set; } = new List<UtlGroupCompany>();

    public ICollection<UtlGroupDepartment> GroupDepartments { get; set; } = new List<UtlGroupDepartment>();

    public ICollection<UtlGroupItemSubCategory> GroupItemSubCategories { get; set; } = new List<UtlGroupItemSubCategory>();
    
}
