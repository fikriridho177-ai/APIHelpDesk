using Microsoft.Data.SqlClient;

namespace AuthUtility.Api.Configuration;

public static class SqlServerConnectionString
{
    public static string? Get(IConfiguration configuration, string name)
    {
        var raw = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var builder = new SqlConnectionStringBuilder(raw);

        // The production SQL Server is a legacy instance whose TLS pre-login
        // handshake fails when the client requires certificate encryption.
        // Preserve explicit deployment settings and only supply the documented
        // compatibility defaults when they are absent.
        if (!builder.ContainsKey("Encrypt"))
            builder.Encrypt = false;

        if (!builder.ContainsKey("TrustServerCertificate"))
            builder.TrustServerCertificate = true;

        return builder.ConnectionString;
    }
}
