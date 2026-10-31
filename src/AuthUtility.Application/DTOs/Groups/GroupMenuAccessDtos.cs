namespace AuthUtility.Application.DTOs.Groups;

public class GroupMenuAccessDto
{
    public int MenuId { get; set; }
    public int? ParentId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string MenuType { get; set; } = "MENU";
    public int SortOrder { get; set; }
    public bool CanView { get; set; }
    public bool CanRead { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
    public bool CanExport { get; set; }
}

public class SaveGroupMenuPermissionRequest
{
    public int MenuId { get; set; }
    public bool CanView { get; set; }
    public bool CanRead { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
    public bool CanExport { get; set; }
}

public class SaveGroupMenuAccessRequest
{
    public List<SaveGroupMenuPermissionRequest> Permissions { get; set; } = new();
}
