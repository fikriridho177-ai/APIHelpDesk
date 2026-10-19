namespace AuthUtility.Application.DTOs.Groups;

public record ScopeOptionDto(int Id, string Code, string Name);

public class GroupScopeDto
{
    public bool All { get; set; }
    public List<int> SelectedIds { get; set; } = new();
    public List<ScopeOptionDto> Options { get; set; } = new();
}

public class SaveGroupScopeRequest
{
    public bool All { get; set; }
    public List<int> SelectedIds { get; set; } = new();
}
