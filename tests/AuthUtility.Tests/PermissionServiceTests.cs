using System.Security.Claims;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using AuthUtility.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

public class PermissionServiceTests
{
    [Fact]
    public async Task IsSuperAdminAsync_RequiresActiveUserAndActiveSuperAdminGroup()
    {
        await using var db = CreateDb();
        var group = new UtlGroup { Id = 1, Code = "ADMIN", Name = "Administrator", IsSuperAdmin = true, Active = true };
        db.Groups.Add(group);
        db.Users.Add(new UtlUser { Id = 1, UserId = "admin", Name = "Admin", PasswordHash = "hash", GroupId = group.Id, Group = group, Active = true });
        await db.SaveChangesAsync();

        var service = new PermissionService(db);
        var principal = PrincipalFor(1);

        Assert.True(await service.IsSuperAdminAsync(principal));

        group.IsSuperAdmin = false;
        await db.SaveChangesAsync();
        Assert.False(await service.IsSuperAdminAsync(principal));

        group.IsSuperAdmin = true;
        group.Active = false;
        await db.SaveChangesAsync();
        Assert.False(await service.IsSuperAdminAsync(principal));
    }

    private static ClaimsPrincipal PrincipalFor(int userId) => new(
        new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            "test"));

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
