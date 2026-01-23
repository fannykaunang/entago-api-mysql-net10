using Dapper;

namespace entago_api_mysql.Services;

public sealed class IzinListService(MySqlConnectionFactory factory)
{
    public async Task<List<IzinListDto>> GetListByPegawaiIdAsync(int pegawai_id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  e.izin_id                          AS Izin_Id,
  p.pegawai_id                       AS Pegawai_Id,
  e.izin_tgl_pengajuan               AS Izin_Tgl_Pengajuan,
  e.izin_tgl                         AS Izin_Tgl,
  j.izin_jenis_id                    AS Izin_Jenis_Id,
  j.izin_jenis_name                  AS Izin_Jenis_Name,
  k.kat_izin_nama                    AS Kat_Izin_Nama,
  e.izin_catatan                     AS Izin_Catatan,
  e.izin_status                      AS Izin_Status,
  e.izin_tinggal_t1                  AS Izin_Tinggal_T1,
  e.izin_tinggal_t2                  AS Izin_Tinggal_T2,
  e.cuti_n_id                        AS Cuti_N_Id,
  e.izin_ket_lain                    AS Izin_Ket_Lain,
  e.izin_noscan_time                 AS Izin_Noscan_Time,
  e.kat_izin_id                      AS Kat_Izin_Id,
  e.ket_status                       AS Ket_Status,
  e.file_name                        AS File_Name,
  e.file_extension                   AS File_Extension,
  e.file_size                        AS File_Size,
  e.file_path                        AS File_Path,
  e.izin_urutan                      AS Izin_Urutan
FROM e_izin e
INNER JOIN (
    SELECT izin_urutan, MAX(izin_id) AS izin_id
    FROM e_izin
    WHERE pegawai_id = @pegawai_id
    GROUP BY izin_urutan
) x ON x.izin_id = e.izin_id
INNER JOIN pegawai p      ON e.pegawai_id = p.pegawai_id
INNER JOIN pembagian2 pb2 ON p.pembagian2_id = pb2.pembagian2_id
INNER JOIN jns_izin j     ON e.izin_jenis_id = j.izin_jenis_id
LEFT JOIN kategori_izin k ON e.kat_izin_id = k.kat_izin_id
ORDER BY e.izin_urutan DESC;";


        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var rows = (await conn.QueryAsync<IzinListDto>(
            new CommandDefinition(sql, new { pegawai_id }, cancellationToken: ct)
        )).AsList();

        return rows;
    }
}
