using System.Security.Cryptography;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.IoT;

public sealed class IotDeviceOptions
{
    public int MaxTelemetryBytes { get; set; } = 64 * 1024;

    public int MaxTelemetryReadings { get; set; } = 256;
}

public sealed record IotDeviceCredentialMaterial(
    string PlaintextKey,
    string Prefix,
    byte[] Salt,
    byte[] Hash);

public sealed record IotAuthenticatedDevice(
    IotDeviceAgent Device,
    IotDeviceCredential Credential);

public interface IIotDeviceCredentialService
{
    IotDeviceCredentialMaterial Create();

    Task<IotAuthenticatedDevice?> AuthenticateAsync(
        string? plaintextKey,
        CancellationToken cancellationToken = default);
}

public sealed class IotDeviceCredentialService(AppDbContext db) : IIotDeviceCredentialService
{
    private const int SaltBytes = 16;
    private const int SecretBytes = 32;
    private const int HashBytes = 32;
    private const int Iterations = 120_000;

    public IotDeviceCredentialMaterial Create()
    {
        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(6))
            .ToLowerInvariant();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = DeriveHash(secret, salt);

        return new IotDeviceCredentialMaterial(
            $"iot_{prefix}_{secret}",
            prefix,
            salt,
            hash);
    }

    public async Task<IotAuthenticatedDevice?> AuthenticateAsync(
        string? plaintextKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryParse(plaintextKey, out var prefix, out var secret))
            return null;

        var credential = await db.IotDeviceCredentials
            .Include(x => x.DeviceAgent)
            .SingleOrDefaultAsync(
                x => x.KeyPrefix == prefix && x.RevokedAt == null,
                cancellationToken);

        if (credential is null
            || credential.DeviceAgent is null
            || !credential.DeviceAgent.IsActive
            || credential.DeviceAgent.Status == IotDeviceStatus.Disabled)
        {
            return null;
        }

        var computedHash = DeriveHash(secret, credential.Salt);
        if (!CryptographicOperations.FixedTimeEquals(computedHash, credential.KeyHash))
            return null;

        return new IotAuthenticatedDevice(credential.DeviceAgent, credential);
    }

    private static bool TryParse(
        string? plaintextKey,
        out string prefix,
        out string secret)
    {
        prefix = "";
        secret = "";

        if (string.IsNullOrWhiteSpace(plaintextKey))
            return false;

        var parts = plaintextKey.Split('_', 3, StringSplitOptions.None);
        if (parts.Length != 3
            || !string.Equals(parts[0], "iot", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(parts[1])
            || string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        prefix = parts[1];
        secret = parts[2];
        return true;
    }

    private static byte[] DeriveHash(string secret, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            secret,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
}
