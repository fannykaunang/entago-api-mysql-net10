using System.Data;
using System.Globalization;
using System.Security.Claims;
using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class CheckoutService(MySqlConnectionFactory factory)
{
    private readonly MySqlConnectionFactory _factory = factory;

    public string? GetPinFromJwt(ClaimsPrincipal user)
        => user.FindFirstValue("pin"); // sesuai claim JWT kamu: pin, skpdid, level, email

    public async Task<int> ResolvePegawaiIdByPinAsync(string pin, CancellationToken ct)
    {
        const string sql = @"SELECT pegawai_id FROM pegawai WHERE pegawai_pin = @pin LIMIT 1;";
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(sql, new { pin }, cancellationToken: ct));
    }

    public async Task<bool> HasCheckoutTodayAsync(int pegawaiId, DateTime today, CancellationToken ct)
    {
        // Hindari error 0000-00-00 dengan CAST ke CHAR (tidak ada konversi ke DATETIME)
        const string sql = @"
SELECT EXISTS(
  SELECT 1
  FROM shift_result
  WHERE pegawai_id = @pegawaiId
    AND tgl_shift = @tgl
    AND izin_jenis_id = 0
    AND CAST(scan_out AS CHAR(19)) <> '0000-00-00 00:00:00'
) AS already_checkout;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { pegawaiId, tgl = today.Date }, cancellationToken: ct)
        );
    }

    public async Task<bool> HasIzinTodayAsync(int pegawaiId, DateTime today, CancellationToken ct)
    {
        const string sql = @"
SELECT EXISTS(
  SELECT 1
  FROM shift_result
  WHERE pegawai_id = @pegawaiId
    AND tgl_shift = @tgl
    AND izin_jenis_id <> 0
) AS has_izin;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { pegawaiId, tgl = today.Date }, cancellationToken: ct)
        );
    }

    public async Task<bool> HasShiftRowTodayAsync(int pegawaiId, DateTime today, CancellationToken ct)
    {
        const string sql = @"
SELECT EXISTS(
  SELECT 1
  FROM shift_result
  WHERE pegawai_id = @pegawaiId
    AND tgl_shift = @tgl
) AS has_row;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { pegawaiId, tgl = today.Date }, cancellationToken: ct)
        );
    }

    public async Task InsertCheckoutAsync(int pegawaiId, string pin, string sn, CancellationToken ct)
    {
        // Sama seperti BindParams() versi lama
        var attidtimestamp = DateTime.Now.ToString("ddMMyyyyHHmmssfff", CultureInfo.InvariantCulture);
        var attIdOut = attidtimestamp + pin + sn;

        var p = new DynamicParameters();
        p.Add("pegawai_ids", pegawaiId, DbType.Int32);
        p.Add("att_id_outs", attIdOut, DbType.String);
        p.Add("sns", sn, DbType.String);
        p.Add("pins", int.Parse(pin), DbType.Int32);
        p.Add("verifymodes", 5, DbType.Int32);
        p.Add("att_ids", attIdOut, DbType.String);
        p.Add("status_jk", -1, DbType.Int16);

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(
            new CommandDefinition("SP_INSERT_CHECKOUT", p, commandType: CommandType.StoredProcedure, cancellationToken: ct)
        );
    }

    public async Task UpdateCheckoutIfExistsAsync(int pegawaiId, string pin, string sn, CancellationToken ct)
    {
        var now = DateTime.Now;
        var tglShift = now.Date;

        var attidtimestamp = now.ToString("ddMMyyyyHHmmssfff", CultureInfo.InvariantCulture);
        var attIdOut = attidtimestamp + pin + sn;

        var p = new DynamicParameters();
        p.Add("pegawai_ids", pegawaiId, DbType.Int32);
        p.Add("att_id_outs", attIdOut, DbType.String);
        p.Add("sns", sn, DbType.String);
        p.Add("pins", int.Parse(pin), DbType.Int32);
        p.Add("verifymodes", 5, DbType.Int32);
        p.Add("att_ids", attIdOut, DbType.String);

        // ✅ INI YANG KURANG
        p.Add("p_tgl_shift", tglShift, DbType.Date);
        p.Add("p_checkout_time", now, DbType.DateTime);

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(
            new CommandDefinition("SP_INSERT_CHECKOUT_IF_EXISTS", p, commandType: CommandType.StoredProcedure, cancellationToken: ct)
        );
    }

    // opsional: GET history (att_log)
    public async Task<List<CheckoutAttLogDto>> GetAttLogByPinAsync(string pin, DateTime? date, CancellationToken ct)
    {
        // pakai CAST(scan_date AS CHAR) agar tidak meledak kalau ada zero-date
        var sql = @"
SELECT
  sn AS Sn,
  CAST(scan_date AS CHAR(19)) AS Scan_Date,
  pin AS Pin,
  verifymode AS VerifyMode,
  inoutmode AS InoutMode,
  reserved AS Reserved,
  work_code AS Work_Code,
  att_id AS Att_Id
FROM att_log
WHERE pin = @pin
";

        if (date is not null)
            sql += " AND DATE(scan_date) = @d ";

        sql += " ORDER BY scan_date DESC;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<CheckoutAttLogDto>(
            new CommandDefinition(sql, new { pin, d = date?.Date }, cancellationToken: ct)
        );

        return rows.AsList();
    }
}
