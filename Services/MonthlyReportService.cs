using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class MonthlyReportService(MySqlConnectionFactory factory)
{
    public async Task<IReadOnlyList<MonthlyRecapDto>> GetMonthlyRecapAsync(
        int pegawaiId,
        DateTime startDate,
        DateTime endDate,
        bool excludeWeekend,
        CancellationToken ct = default)
    {
        // Pastikan date-only (tanpa time)
        startDate = startDate.Date;
        endDate = endDate.Date;

        // Catatan: filter weekend pakai 1=1 / kondisi dinamis supaya query tetap parameterized
        var weekendFilter = excludeWeekend ? "AND DAYOFWEEK(cal.tgl) NOT IN (1,7)" : "";

        var sql = $@"
SELECT
  p.pegawai_id   AS Pegawai_Id,
  p.pegawai_pin  AS Pegawai_Pin,
  p.pegawai_nama AS Pegawai_Nama,

  DATE_FORMAT(cal.tgl, '%Y-%m') AS Periode_Bulan,

  COUNT(*) AS Total_Hari_Kalender,

  SUM(CASE WHEN l.libur_tgl IS NOT NULL THEN 1 ELSE 0 END) AS Total_Hari_Libur,

  (COUNT(*) - SUM(CASE WHEN l.libur_tgl IS NOT NULL THEN 1 ELSE 0 END)) AS Total_Hari_Kerja,

  SUM(CASE WHEN l.libur_tgl IS NULL AND sr.scan_in IS NOT NULL THEN 1 ELSE 0 END) AS Hadir,

  SUM(CASE WHEN l.libur_tgl IS NULL AND iz.izin_tgl IS NOT NULL THEN 1 ELSE 0 END) AS Izin,

  SUM(CASE WHEN l.libur_tgl IS NULL AND sr.scan_in IS NULL AND iz.izin_tgl IS NULL THEN 1 ELSE 0 END) AS Alpa,

  SUM(
    CASE
      WHEN sr.durasi_minute > 0 THEN sr.durasi_minute
      WHEN sr.scan_in IS NOT NULL AND sr.scan_out IS NOT NULL THEN TIMESTAMPDIFF(MINUTE, sr.scan_in, sr.scan_out)
      ELSE 0
    END
  ) AS Total_Menit_Kerja,

  ROUND(
    SUM(
      CASE
        WHEN sr.durasi_minute > 0 THEN sr.durasi_minute
        WHEN sr.scan_in IS NOT NULL AND sr.scan_out IS NOT NULL THEN TIMESTAMPDIFF(MINUTE, sr.scan_in, sr.scan_out)
        ELSE 0
      END
    ) / 60
  , 2) AS Total_Jam_Kerja,

  ROUND(
    100 * SUM(CASE WHEN l.libur_tgl IS NULL AND sr.scan_in IS NOT NULL THEN 1 ELSE 0 END)
    / NULLIF((COUNT(*) - SUM(CASE WHEN l.libur_tgl IS NOT NULL THEN 1 ELSE 0 END)), 0)
  , 2) AS Persentase_Kehadiran

FROM
(
  -- kalender tanggal dari @start_date s/d @end_date (0..999 hari)
  SELECT DATE_ADD(@start_date, INTERVAL n.n DAY) AS tgl
  FROM (
    SELECT (a.i + b.i*10 + c.i*100) AS n
    FROM (SELECT 0 i UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
          UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) a
    CROSS JOIN
         (SELECT 0 i UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
          UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) b
    CROSS JOIN
         (SELECT 0 i UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
          UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) c
  ) n
  WHERE DATE_ADD(@start_date, INTERVAL n.n DAY) <= @end_date
) cal
JOIN pegawai p ON p.pegawai_id = @pegawai_id

LEFT JOIN (
  SELECT
    pegawai_id,
    tgl_shift,
    CASE WHEN scan_in  < '1000-01-01 00:00:00' THEN NULL ELSE scan_in  END AS scan_in,
    CASE WHEN scan_out < '1000-01-01 00:00:00' THEN NULL ELSE scan_out END AS scan_out,
    durasi_minute
  FROM shift_result
  WHERE pegawai_id = @pegawai_id
    AND tgl_shift BETWEEN @start_date AND @end_date
) sr ON sr.tgl_shift = cal.tgl

LEFT JOIN (
  SELECT libur_tgl
  FROM libur
  WHERE libur_tgl BETWEEN @start_date AND @end_date
    AND libur_status IN (1,2)
) l ON l.libur_tgl = cal.tgl

LEFT JOIN (
  SELECT pegawai_id, izin_tgl
  FROM e_izin
  WHERE pegawai_id = @pegawai_id
    AND izin_tgl BETWEEN @start_date AND @end_date
    AND izin_status = 1
) iz ON iz.izin_tgl = cal.tgl

WHERE 1=1
{weekendFilter}

GROUP BY
  p.pegawai_id, p.pegawai_pin, p.pegawai_nama,
  DATE_FORMAT(cal.tgl, '%Y-%m')

ORDER BY
  DATE_FORMAT(cal.tgl, '%Y-%m');";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var param = new
        {
            pegawai_id = pegawaiId,
            start_date = startDate,
            end_date = endDate
        };

        var rows = (await conn.QueryAsync<MonthlyRecapDto>(
            new CommandDefinition(sql, param, cancellationToken: ct)
        )).AsList();

        return rows;
    }

    public async Task<int?> ResolvePegawaiIdByPinAsync(int pin, CancellationToken ct = default)
    {
        const string sql = @"SELECT pegawai_id FROM pegawai WHERE pegawai_pin = @pin LIMIT 1;";
        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(sql, new { pin }, cancellationToken: ct)
        );
    }
}
