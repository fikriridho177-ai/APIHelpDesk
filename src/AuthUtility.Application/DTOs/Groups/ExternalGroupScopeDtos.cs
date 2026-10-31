namespace AuthUtility.Application.DTOs.Groups;

public class ExternalGroupScopeDto
{
    public bool All { get; set; }
    public List<string> SelectedIds { get; set; } = new();
    public List<object> Options { get; set; } = new();
}

public class SaveExternalGroupScopeRequest
{
    public bool All { get; set; }
    public List<string> SelectedIds { get; set; } = new();
}
