namespace AuthUtility.Application.DTOs.Users;

public class UpdateUserRequest
{
    // Opsional: kosong = User ID lama tetap dipakai.
    public string? UserId { get; set; }

    public string Name { get; set; } = "";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int GroupId { get; set; }

    public string? Nik { get; set; }

    public long? RegNo { get; set; }

    public string? DeptId { get; set; }

    public int? CompanyId { get; set; }

    public bool Active { get; set; }

    public string? Password { get; set; }
}

public class CreateUserRequest
{
    public string UserId { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int GroupId { get; set; }

    public string? Nik { get; set; }

    public long? RegNo { get; set; }

    public string? DeptId { get; set; }

    public int? CompanyId { get; set; }

    public bool Active { get; set; }

    public string Password { get; set; } = "";
}

public record UserDto(
    int Id,
    string UserId,
    string Name,
    string? Email,
    string? Phone,
    string? Nik,
    long? RegNo,
    string? DeptId,
    int? CompanyId,
    int GroupId,
    string GroupName,
    bool Active,
    bool MustChangePassword
);