using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace entago_api_mysql.Services;

public sealed record ApiClient(
    long Id,
    string Nama,
    string Prefix,
    byte[] Key_Hash,
    sbyte Status,
    string? Allowed_Origins,
    string? Allowed_Ips,
    int Rate_Limit
);

public sealed class ApiClientService(MySqlConnectionFactory factory)
{
    public static byte[] Sha256Bytes(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        return SHA256.HashData(bytes);
    }

    public async Task<ApiClient?> FindActiveClientByKeyAsync(string apiKey, CancellationToken ct = default)
    {
        var hash = Sha256Bytes(apiKey);

        const string sql = @"
            SELECT
            id, nama, prefix, key_hash AS Key_Hash, status,
            allowed_origins AS Allowed_Origins,
            allowed_ips AS Allowed_Ips,
            rate_limit AS Rate_Limit
            FROM e_api_clients
            WHERE key_hash = @hash AND status = 1
            LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        // MySQL BINARY(32) -> byte[]
        return await conn.QueryFirstOrDefaultAsync<ApiClient>(
            new CommandDefinition(sql, new { hash }, cancellationToken: ct)
        );
    }
}
