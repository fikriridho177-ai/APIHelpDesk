namespace AuthUtility.Domain.Entities;

public class UtlMenu
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public UtlMenu? Parent { get; set; }

    public ICollection<UtlMenu> Children { get; set; }
        = new List<UtlMenu>();

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Route { get; set; }

    public string? Icon { get; set; }

    public string MenuType { get; set; } = "MENU";

    public int SortOrder { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<UtlGroupMenu> GroupMenus { get; set; }
        = new List<UtlGroupMenu>();
}