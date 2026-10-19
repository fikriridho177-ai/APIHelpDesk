namespace AuthUtility.Domain.Entities;

public class UtlPasswordHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public UtlUser? User { get; set; }

    public string PasswordHash { get; set; } = "";

    public DateTime ChangedAt { get; set; }
        = DateTime.UtcNow;
}