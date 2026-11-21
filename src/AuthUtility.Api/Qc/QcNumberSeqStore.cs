using System.Collections.Concurrent;
using AuthUtility.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuthUtility.Api.Qc;

// Pengambil nomor urut atomik per scope. Implementasi Npgsql memakai
// INSERT ... ON CONFLICT DO UPDATE ... RETURNING (parameterized, satu statement)
// sehingga aman terhadap race condition tanpa lock aplikasi.
public interface IQcNumberSeqStore
{
    Task<int> NextAsync(string scopeKey, CancellationToken ct = default);
}

public sealed class NpgsqlQcNumberSeqStore(AppDbContext db) : IQcNumberSeqStore
{
    public async Task<int> NextAsync(string scopeKey, CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                INSERT INTO "MstQcNumberSeq" ("ScopeKey", "LastValue", "UpdatedAt")
                VALUES (@scopeKey, 1, now())
                ON CONFLICT ("ScopeKey") DO UPDATE
                SET "LastValue" = "MstQcNumberSeq"."LastValue" + 1, "UpdatedAt" = now()
                RETURNING "LastValue"
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@scopeKey";
            parameter.Value = scopeKey;
            command.Parameters.Add(parameter);
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }
}

// Fake in-memory untuk unit test (EF InMemory tidak mendukung raw SQL).
public sealed class InMemoryQcNumberSeqStore : IQcNumberSeqStore
{
    private readonly ConcurrentDictionary<string, int> _values = new();

    public Task<int> NextAsync(string scopeKey, CancellationToken ct = default) =>
        Task.FromResult(_values.AddOrUpdate(scopeKey, 1, (_, current) => current + 1));
}