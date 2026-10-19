using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Infrastructure.Data;

// Database MyPAD (SQL Server) - hanya dibaca (master persil untuk halaman Master Kebun & Blok).
public class MyPADDbContext : DbContext
{
    public MyPADDbContext(
        DbContextOptions<MyPADDbContext> options
    ) : base(options)
    {
    }
}
