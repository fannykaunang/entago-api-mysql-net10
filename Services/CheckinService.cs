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
            CASE WHEN sr.scan_in  < '1000-01-01' THEN NULL ELSE sr.scan_in  END AS Scan_In,
            CASE WHEN sr.scan_out < '1000-01-01' THEN NULL ELSE sr.scan_out END AS Scan_Out
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

    public async Task<int> InsertAttLogAsync(CheckinCreateRequest req, CancellationToken ct = default)
    {
        const string sp = "SP_INSERT_CHECKIN";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var tglShift = req.Tgl_Shift.HasValue
            ? req.Tgl_Shift.Value.ToDateTime(TimeOnly.MinValue).Date
            : DateTime.Today;

        var attIdIn = string.IsNullOrWhiteSpace(req.Att_Id_In) ? req.Att_Id : req.Att_Id_In;

        var args = new
        {
            p_sn = req.Sn,
            p_pin = req.Pin,
            p_verifymode = req.Verifymode,
            p_inoutmode = req.Inoutmode,
            p_reserved = req.Reserved,
            p_work_code = req.Work_Code,
            p_att_id = req.Att_Id,
            p_pegawai_id = req.Pegawai_Id,
            p_tgl_shift = tglShift,
            p_jdw_kerja_m_id = req.Jdw_Kerja_M_Id,
            p_jk_id = req.Jk_Id,
            p_att_id_in = attIdIn,
            p_late_minute = req.Late_Minute,
            p_late = req.Late
        };

        return await conn.ExecuteAsync(
            new CommandDefinition(sp, args, commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct)
        );
    }

}
