namespace AuthUtility.Application.DTOs.Menus;

public class NavigationMenuDto
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
}