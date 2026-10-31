namespace AuthUtility.Domain.Entities;

public class UtlGroupMenu
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public UtlGroup? Group { get; set; }

    public int MenuId { get; set; }

    public UtlMenu? Menu { get; set; }

    public bool CanView { get; set; }

    public bool CanRead { get; set; }

    public bool CanCreate { get; set; }

    public bool CanUpdate { get; set; }

    public bool CanDelete { get; set; }

    public bool CanApprove { get; set; }

    public bool CanExport { get; set; }
}