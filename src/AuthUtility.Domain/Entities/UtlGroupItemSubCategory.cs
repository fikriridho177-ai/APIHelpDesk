namespace AuthUtility.Domain.Entities;

public class UtlGroupItemSubCategory
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public UtlGroup? Group { get; set; }

    public int ItemSubCategoryId { get; set; }
    
    public MstItemSubCategory? ItemSubCategory { get; set; }
}
