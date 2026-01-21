using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class CheckinService(MySqlConnectionFactory factory)
{
    public async Task<ShiftResultDto?> FindLatestShiftResultByPinAsync(int pegawai_pin, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  p.pegawai_id AS Pegawai_Id,
  p.pegawai_pin AS Pegawai_Pin,
  sr.scan_in AS Scan_In,
  sr.scan_out AS Scan_Out
FROM shift_result sr
INNER JOIN pegawai p ON sr.pegawai_id = p.pegawai_id
WHERE p.pegawai_pin = @pegawai_pin
ORDER BY sr.tgl_shift DESC
LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<ShiftResultDto>(
            new CommandDefinition(sql, new { pegawai_pin }, cancellationToken: ct)
        );
    }

    public async Task<List<ShiftResultDto>?> FindShiftResultsByPinAsync(int pegawai_pin, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  p.pegawai_id AS Pegawai_Id,
  p.pegawai_pin AS Pegawai_Pin,
  sr.scan_in AS Scan_In,
  sr.scan_out AS Scan_Out
FROM shift_result sr
INNER JOIN pegawai p ON sr.pegawai_id = p.pegawai_id
WHERE p.pegawai_pin = @pegawai_pin
ORDER BY sr.tgl_shift DESC;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var rows = (await conn.QueryAsync<ShiftResultDto>(
            new CommandDefinition(sql, new { pegawai_pin }, cancellationToken: ct)
        )).AsList();

        return rows.Count == 0 ? null : rows;
    }

    public async Task<List<AttLogDto>?> FindAttLogByPinAndDateAsync(int pin, DateTime date, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  sn AS Sn,
  scan_date AS Scan_Date,
  pin AS Pin,
  verifymode AS Verifymode,
  inoutmode AS Inoutmode,
  reserved AS Reserved,
  work_code AS Work_Code,
  att_id AS Att_Id
FROM att_log
WHERE pin = @pin
  AND DATE(scan_date) = @scan_date;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var rows = (await conn.QueryAsync<AttLogDto>(
            new CommandDefinition(sql, new { pin, scan_date = date.Date }, cancellationToken: ct)
        )).AsList();

        return rows.Count == 0 ? null : rows;
    }

    public async Task<DateTime?> GetMorningCheckinByPinAndDateAsync(int pin, DateTime date, CancellationToken ct = default)
    {
        var start = date.Date.AddHours(6); // 06:00
        var end = date.Date.AddHours(9);   // 09:00 (eksklusif)

        const string sql = @"
SELECT MIN(scan_date)
FROM att_log
WHERE pin = @pin
  AND scan_date >= @start
  AND scan_date <  @end;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<DateTime?>(
            new CommandDefinition(sql, new { pin, start, end }, cancellationToken: ct)
        );
    }

    public async Task<List<IzinResultDto>?> FindIzinHariIniAsync(int pegawai_id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  pegawai_id AS Pegawai_Id,
  tgl_shift AS Tgl_Shift,
  izin_jenis_id AS Izin_Jenis_Id
FROM shift_result
WHERE pegawai_id = @pegawai_id
  AND tgl_shift = DATE_FORMAT(NOW(), '%Y%m%d')
  AND izin_jenis_id = 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var rows = (await conn.QueryAsync<IzinResultDto>(
            new CommandDefinition(sql, new { pegawai_id }, cancellationToken: ct)
        )).AsList();

        return rows.Count == 0 ? null : rows;
    }

    public async Task<int> InsertAttLogAsync(CheckinCreateRequest req, DateTime scanDate, CancellationToken ct = default)
    {
        // Sesuaikan kolom att_log kamu kalau beda (ini mengikuti field yang kamu pakai di ReadAllAsync)
        const string sql = @"
INSERT INTO att_log
(sn, scan_date, pin, verifymode, inoutmode, reserved, work_code, att_id)
VALUES
(@Sn, @Scan_Date, @Pin, @Verifymode, @Inoutmode, @Reserved, @Work_Code, @Att_Id);";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        return await conn.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                req.Sn,
                Scan_Date = scanDate,
                req.Pin,
                req.Verifymode,
                req.Inoutmode,
                req.Reserved,
                req.Work_Code,
                req.Att_Id
            }, cancellationToken: ct)
        );
    }
}
