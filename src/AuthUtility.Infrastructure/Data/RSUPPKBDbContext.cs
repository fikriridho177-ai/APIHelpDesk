using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Infrastructure.Data;

// Database RSUPPKB (SQL Server) - hanya dibaca (master persil untuk halaman Master Kebun & Blok).
public class RSUPPKBDbContext : DbContext
{
    public RSUPPKBDbContext(
        DbContextOptions<RSUPPKBDbContext> options
    ) : base(options)
    {
    }
}
