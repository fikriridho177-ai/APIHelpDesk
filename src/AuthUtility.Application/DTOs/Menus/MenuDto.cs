namespace AuthUtility.Application.DTOs.Menus;

public class MenuDto
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Route { get; set; }

    public string? Icon { get; set; }

    public string? FullName { get; set; }

    public string? Description { get; set; }

    public string? Color { get; set; }

    public string MenuType { get; set; } = "MENU";

    public int SortOrder { get; set; }

    public bool Active { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public List<MenuPermissionDto> Permissions { get; set; }
        = new();
}