using AuthUtility.Api.IoT;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Tests;

public sealed class IotDeviceCredentialRulesTests
{
    [Fact]
    public async Task Create_and_verify_returns_the_registered_device_only()
    {
        await using var db = CreateDb();
        var device = new IotDeviceAgent
        {
            Id = Guid.NewGuid(),
            DeviceId = "esp8266-001",
            Name = "Greenhouse 1"
        };
        db.IotDeviceAgents.Add(device);

        var service = new IotDeviceCredentialService(db);
        var material = service.Create();
        db.IotDeviceCredentials.Add(new IotDeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            KeyPrefix = material.Prefix,
            Salt = material.Salt,
            KeyHash = material.Hash
        });
        await db.SaveChangesAsync();

        var authenticated = await service.AuthenticateAsync(material.PlaintextKey);

        Assert.NotNull(authenticated);
        Assert.Equal(device.Id, authenticated.Device.Id);
        Assert.NotEqual(material.PlaintextKey, Convert.ToBase64String(material.Hash));
    }

    [Fact]
    public async Task Wrong_or_revoked_key_is_rejected_without_device_lookup_leak()
    {
        await using var db = CreateDb();
        var device = new IotDeviceAgent
        {
            Id = Guid.NewGuid(),
            DeviceId = "esp8266-002",
            Name = "Greenhouse 2"
        };
        db.IotDeviceAgents.Add(device);

        var service = new IotDeviceCredentialService(db);
        var material = service.Create();
        var credential = new IotDeviceCredential
        {
            Id = Guid.NewGuid(),
            DeviceAgentId = device.Id,
            KeyPrefix = material.Prefix,
            Salt = material.Salt,
            KeyHash = material.Hash
        };
        db.IotDeviceCredentials.Add(credential);
        await db.SaveChangesAsync();

        Assert.Null(await service.AuthenticateAsync(material.PlaintextKey + "x"));

        credential.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        Assert.Null(await service.AuthenticateAsync(material.PlaintextKey));
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
