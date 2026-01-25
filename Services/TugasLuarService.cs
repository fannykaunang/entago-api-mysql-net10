using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class TugasLuarService
{
    private readonly MySqlConnectionFactory _factory;

    public TugasLuarService(MySqlConnectionFactory factory)
    {
        _factory = factory;
    }

    // =========================
    // GET list tugas luar
    // =========================
    public async Task<List<TugasLuarDto>> GetListByPegawaiAsync(
        int pegawaiId,
        DateTime? start,
        DateTime? end,
        CancellationToken ct)
    {
        // kalau start/end null -> ambil 30 hari terakhir (opsional)
        // kamu bisa ubah logic ini sesuai kebutuhan
        var s = start?.Date;
        var e = end?.Date;

        var sql = @"
SELECT
  tugas_luar_id      AS Tugas_Luar_Id,
  pegawai_id         AS Pegawai_Id,
  tugas_tgl          AS Tugas_Tgl,
  tujuan             AS Tujuan,
  keterangan_tugas   AS Keterangan_Tugas,
  alamat             AS Alamat,
  latitude           AS Latitude,
  longitude          AS Longitude,
  is_verified        AS Is_Verified,
  file_name          AS File_Name,
  file_extension     AS File_Extension,
  file_size          AS File_Size,
  file_path          AS File_Path
FROM e_tugas_luar
WHERE pegawai_id = @pegawaiId
  AND (@s IS NULL OR tugas_tgl >= @s)
  AND (@e IS NULL OR tugas_tgl <  @e)
ORDER BY tugas_tgl DESC;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<TugasLuarDto>(
            new CommandDefinition(sql, new { pegawaiId, s, e }, cancellationToken: ct)
        );

        return rows.AsList();
    }

    // =========================
    // Cek sudah absen hari ini?
    // =========================
    public async Task<bool> HasAnyAttendanceTodayAsync(
        int pegawaiId,
        DateTime tglShift,
        CancellationToken ct)
    {
        // shift_result.tgl_shift = DATE
        // scan_in/scan_out default 0000-00-00 00:00:00
        // pakai NULLIF supaya dianggap NULL kalau 0000...
        var sql = @"
        SELECT EXISTS(
            SELECT 1
            FROM shift_result
            WHERE pegawai_id = @pegawaiId
              AND tgl_shift = @tgl
              AND (att_id_in <> '0' OR att_id_out <> '0')
            LIMIT 1
        )";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        var exists = await conn.ExecuteScalarAsync<int>(
            new Dapper.CommandDefinition(sql, new { pegawaiId, tgl = tglShift.Date }, cancellationToken: ct)
        );

        return exists == 1;
    }

    // =========================
    // Cek sudah input tugas luar hari ini?
    // =========================
    public async Task<bool> HasTugasLuarTodayAsync(
        int pegawaiId,
        DateTime today,
        CancellationToken ct)
    {
        var start = today.Date;
        var end = start.AddDays(1);

        var sql = @"
SELECT EXISTS(
  SELECT 1
  FROM e_tugas_luar
  WHERE pegawai_id = @pegawaiId
    AND tugas_tgl >= @start
    AND tugas_tgl <  @end
) AS already_exists;";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { pegawaiId, start, end }, cancellationToken: ct)
        );
    }

    // =========================
    // INSERT tugas luar
    // =========================
    public async Task<int> InsertTugasLuarAsync(
        TugasLuarDto dto,
        CancellationToken ct)
    {
        var sql = @"
INSERT INTO e_tugas_luar
(pegawai_id, tugas_tgl, tujuan, keterangan_tugas, alamat, latitude, longitude,
 is_verified, file_name, file_extension, file_size, file_path)
VALUES
(@Pegawai_Id, @Tugas_Tgl, @Tujuan, @Keterangan_Tugas, @Alamat, @Latitude, @Longitude,
 @Is_Verified, @File_Name, @File_Extension, @File_Size, @File_Path);

SELECT LAST_INSERT_ID();";

        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);

        var id = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, dto, cancellationToken: ct)
        );

        return id;
    }
}
