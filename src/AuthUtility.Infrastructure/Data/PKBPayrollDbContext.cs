using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Infrastructure.Data;

public class PKBPayrollDbContext : DbContext
{
    public PKBPayrollDbContext(
        DbContextOptions<PKBPayrollDbContext> options
    ) : base(options)
    {
    }

    // Contoh:
    // public DbSet<Employee> Employees => Set<Employee>();
}