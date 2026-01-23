using System.Data;
using System.Globalization;
using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class CheckoutService(MySqlConnectionFactory factory)
{
    private const string ZeroDt = "0000-00-00 00:00:00";

    public sealed class PegawaiByPinRow
    {
        public int Pegawai_Id { get; set; }
        public int Pin { get; set; }
    }

    public async Task<PegawaiByPinRow?> FindPegawaiByPinAsync(int pin, CancellationToken ct = default)
    {
        const string sql = @"SELECT pegawai_id AS Pegawai_Id, pegawai_pin AS Pin
                             FROM pegawai
                             WHERE pegawai_pin = @pin
                             LIMIT 1;";
        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<PegawaiByPinRow>(
            new CommandDefinition(sql, new { pin }, cancellationToken: ct)
        );
    }

    // Sudah checkout hari ini?
    public async Task<bool> HasCheckoutTodayAsync(int pegawaiId, DateTime tglShift, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 1
            FROM shift_result
            WHERE pegawai_id = @pegawaiId
            AND tgl_shift = @tglShift
            AND izin_jenis_id = 0
            AND scan_out >= '2000-01-01 00:00:00'
            LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var one = await conn.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(sql, new { pegawaiId, tglShift = tglShift.Date }, cancellationToken: ct)
        );

        return one.HasValue;
    }


    // Ada row shift_result hari ini? (berarti pernah checkin / pernah dibuat)
    public async Task<bool> HasShiftRowTodayAsync(int pegawaiId, DateTime tglShift, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 1
            FROM shift_result
            WHERE pegawai_id = @pegawaiId
            AND tgl_shift = @tglShift
            LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var one = await conn.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(sql, new { pegawaiId, tglShift = tglShift.Date }, cancellationToken: ct)
        );
        return one.HasValue;
    }

    // Sedang izin hari ini?
    public async Task<bool> HasIzinTodayAsync(int pegawaiId, DateTime tglShift, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 1
            FROM shift_result
            WHERE pegawai_id = @pegawaiId
            AND tgl_shift = @tglShift
            AND izin_jenis_id <> 0
            LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var one = await conn.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(sql, new { pegawaiId, tglShift = tglShift.Date }, cancellationToken: ct)
        );
        return one.HasValue;
    }

    private static string BuildAttId(int pin, string sn)
    {
        var ts = DateTime.Now.ToString("ddMMyyyyHHmmssfff", CultureInfo.InvariantCulture);
        return ts + pin + sn;
    }

    // Checkout INSERT (jika tidak ada row shift_result hari ini)
    public async Task InsertCheckoutAsync(CheckoutDto req, int pegawaiId, DateTime tglShift, CancellationToken ct = default)
    {
        var attId = BuildAttId(req.Pin, req.Sn);

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        // Kalau SP kamu masih versi lama (param: pegawai_ids, att_id_outs, sns, pins, verifymodes, att_ids)
        await conn.ExecuteAsync(new CommandDefinition(
            "SP_INSERT_CHECKOUT",
            new
            {
                pegawai_ids = pegawaiId,
                att_id_outs = attId,
                sns = req.Sn,
                pins = req.Pin,
                verifymodes = 5,
                att_ids = attId
                // kalau kamu sudah upgrade SP untuk menerima p_tgl_shift / p_checkout_time -> tinggal tambahkan di sini
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        ));
    }

    // Checkout UPDATE (jika row shift_result hari ini sudah ada)
    public async Task UpdateCheckoutIfExistsAsync(CheckoutDto req, int pegawaiId, DateTime tglShift, CancellationToken ct = default)
    {
        var attId = BuildAttId(req.Pin, req.Sn);
        var checkoutTime = DateTime.Now; // server time

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "SP_INSERT_CHECKOUT_IF_EXISTS",
           new
           {
               p_pegawai_id = pegawaiId,
               p_att_id_out = attId,
               p_sn = req.Sn,
               p_pin = req.Pin,          // pastikan type cocok (int atau string sesuai SP)
               p_verifymode = 5,
               p_att_id = attId,
               p_tgl_shift = tglShift.Date,     // kalau SP terima DATE
               p_checkout_time = checkoutTime   // kalau SP terima DATETIME
           },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        ));
    }
}
