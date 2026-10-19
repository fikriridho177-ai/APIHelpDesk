namespace AuthUtility.Application.DTOs.Groups;

public record GroupDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsSuperAdmin,
    bool AllCompanies,
    bool AllDepartments,
    bool AllItemSubCategories,
    bool Active,
    string? CreatedBy,
    DateTime CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt
);

public class SaveGroupRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool Active { get; set; } = true;
}
