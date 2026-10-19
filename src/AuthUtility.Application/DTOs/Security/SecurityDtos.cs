namespace AuthUtility.Application.DTOs.Security;

public record MenuDto(
    int Id,
    string Code,
    string Name,
    string? Route,
    int SortOrder,
    bool Active
);

public record GroupMenuDto(
    int MenuId,
    string Code,
    string Name,
    bool CanView,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete,
    bool CanApprove
);

public record SaveGroupMenuRequest(
    int MenuId,
    bool CanView,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete,
    bool CanApprove
);
