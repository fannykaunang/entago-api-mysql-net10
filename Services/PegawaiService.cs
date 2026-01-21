using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class PegawaiService(MySqlConnectionFactory factory)
{


    public async Task<IEnumerable<PegawaiDto>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                pegawai.pegawai_id,
                pegawai.pegawai_pin,
                pegawai.pegawai_nip,
                pegawai.pegawai_nama,
                pegawai.tempat_lahir,
                pegawai.pegawai_privilege,
                pegawai.pegawai_telp,
                pegawai.pegawai_status,

                IFNULL(DATE_FORMAT(pegawai.tgl_lahir, '%Y-%m-%d'), '') AS tgl_lahir,

                IFNULL(pembagian1.pembagian1_nama, '') AS jabatan,
                IFNULL(pembagian2.pembagian2_nama, '') AS skpd,
                IFNULL(pembagian3.pembagian3_nama, '') AS sotk,

                IFNULL(DATE_FORMAT(pegawai.tgl_mulai_kerja, '%Y-%m-%d'), '') AS tgl_mulai_kerja,

                pegawai.gender,
                pegawai.photo_path,
                pegawai.no_rek AS deviceid
            FROM pegawai
            LEFT JOIN pembagian1 ON pembagian1.pembagian1_id = pegawai.pembagian1_id
            LEFT JOIN pembagian2 ON pembagian2.pembagian2_id = pegawai.pembagian2_id
            LEFT JOIN pembagian3 ON pembagian3.pembagian3_id = pegawai.pembagian3_id
            ORDER BY pegawai.pegawai_pin;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        // Dapper otomatis map kolom pegawai_id -> Pegawai_Id, dll (underscore mapping)
        var rows = await conn.QueryAsync<PegawaiDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows;
    }


    public async Task<DeviceCheckDto?> CheckDeviceAsync(string pegawaiPin, string noRek, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
            no_rek AS No_Rek,
            pegawai_id AS Pegawai_Id,
            pegawai_pin AS Pegawai_Pin
            FROM pegawai
            WHERE pegawai_pin = @pegawai_pin
            AND no_rek = @no_rek
            LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        return await conn.QueryFirstOrDefaultAsync<DeviceCheckDto>(
            new CommandDefinition(sql, new { pegawai_pin = pegawaiPin, no_rek = noRek }, cancellationToken: ct)
        );
    }
}
