using System.Net.NetworkInformation;
using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class MonitorService(MySqlConnectionFactory factory)
{
    private readonly MySqlConnectionFactory _factory = factory;

    public async Task<List<MachineEntity>> GetMachinesBySkpdIdAsync(int skpdid, CancellationToken ct)
    {
        // Sesuaikan field skpdid kalau namanya beda di tabel e_skpd
        const string sql = @"
SELECT
  d.dev_id      AS Dev_Id,
  COALESCE(s.skpd_alias, '') AS Skpd_Alias,
  d.device_name AS Device_Name,
  d.ip_address  AS Ip_Address
FROM device d
LEFT JOIN e_skpd s ON s.sn = d.sn
WHERE s.skpdid = @skpdid
ORDER BY d.device_name;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MachineEntity>(
            new CommandDefinition(sql, new { skpdid }, cancellationToken: ct)
        );

        return rows.AsList();
    }

    public async Task<List<MachineEntity>> GetMachinesAsync(int? skpdid, CancellationToken ct)
    {
        var sql = @"
SELECT
  d.dev_id      AS Dev_Id,
  COALESCE(s.skpd_alias, '') AS Skpd_Alias,
  d.device_name AS Device_Name,
  d.ip_address  AS Ip_Address
FROM device d
LEFT JOIN e_skpd s ON s.sn = d.sn
";

        // ✅ filter hanya kalau skpdid dikirim
        if (skpdid is not null && skpdid > 0)
            sql += " WHERE s.skpdid = @skpdid ";

        sql += " ORDER BY d.device_name;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<MachineEntity>(
            new CommandDefinition(sql, new { skpdid }, cancellationToken: ct)
        );

        return rows.AsList();
    }

    public async Task<MonitorDevicesResponse> GetDeviceStatusAsync(int? skpdid, CancellationToken ct)
    {
        var machines = await GetMachinesAsync(skpdid, ct);

        if (machines.Count == 0)
            return new MonitorDevicesResponse(0, 0, 0, Array.Empty<MachineStatusDto>());

        const int maxConcurrency = 50;
        const int timeoutMs = 1500;

        using var sem = new SemaphoreSlim(maxConcurrency);

        var tasks = machines.Select(async (m, idx) =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var (status, ms) = await PingOnceAsync(m.Ip_Address, timeoutMs, ct);

                return new MachineStatusDto(
                    No: idx + 1,
                    Waktu: DateTime.Now,
                    Ip_Address: m.Ip_Address,
                    Skpd_Alias: m.Skpd_Alias,
                    Device_Name: m.Device_Name,
                    Status: status,
                    RoundtripMs: ms
                );
            }
            finally { sem.Release(); }
        }).ToArray();

        var resultRows = await Task.WhenAll(tasks);

        var online = resultRows.Count(x => x.Status == "ONLINE");
        var offline = resultRows.Length - online;

        return new MonitorDevicesResponse(online, offline, resultRows.Length, resultRows);
    }

    private static async Task<(string status, long? ms)> PingOnceAsync(string ip, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();

            // SendPingAsync tidak punya CancellationToken, jadi kita pakai WaitAsync(ct)
            var reply = await ping.SendPingAsync(ip, timeoutMs).WaitAsync(ct);

            if (reply.Status == IPStatus.Success)
                return ("ONLINE", reply.RoundtripTime);

            return ("OFFLINE", null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // IP invalid / host unreachable / PingException -> anggap OFFLINE
            return ("OFFLINE", null);
        }
    }
}
