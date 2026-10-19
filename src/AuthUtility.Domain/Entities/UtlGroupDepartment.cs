namespace AuthUtility.Domain.Entities;

public class UtlGroupDepartment
{
    public int GroupId { get; set; }
    public UtlGroup? Group { get; set; }

    // External DeptID dari PKBPayroll.vwMstDept (kolom database bertipe integer)
    public int DepartmentId { get; set; }
}
