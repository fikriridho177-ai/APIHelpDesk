namespace AuthUtility.Domain.Entities;

public class MstItemSubCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public ICollection<UtlGroupItemSubCategory> GroupItemSubCategories { get; set; } = new List<UtlGroupItemSubCategory>();
}
