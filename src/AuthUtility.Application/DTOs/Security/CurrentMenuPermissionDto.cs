namespace AuthUtility.Application.DTOs.Security;

public class CurrentMenuPermissionDto
{
    public string MenuCode { get; set; } = "";
    public bool IsSuperAdmin { get; set; }
    public bool CanView { get; set; }
    public bool CanRead { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public bool CanApprove { get; set; }
    public bool CanExport { get; set; }
}
