using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Infrastructure.Data;

// Database MyKTTATest (SQL Server) - hanya dibaca (data sensor ECS Monitoring, salinan GET dari APIMyECS).
public class MyKTTATestDbContext : DbContext
{
    public MyKTTATestDbContext(
        DbContextOptions<MyKTTATestDbContext> options
    ) : base(options)
    {
    }
}
