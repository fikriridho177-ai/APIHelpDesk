namespace AuthUtility.Domain.Entities;

public class UtlUser
{
    public int Id { get; set; }

    public string UserId { get; set; } = "";

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public int GroupId { get; set; }

    public string? Phone { get; set; }

    public string? Nik { get; set; }

    public long? RegNo { get; set; }

    public string? DeptId { get; set; }

    public int? CompanyId { get; set; }

    public UtlGroup? Group { get; set; }

    public bool Active { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;

    public DateTime? LastPasswordChangedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
