using Dapper;
using entago_api_mysql.Dtos;

namespace entago_api_mysql.Services;

public sealed class PegawaiService(MySqlConnectionFactory factory)
{
    public async Task<object?> GetByPinAsync(string pegawaiPin, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
            p.pegawai_id, p.pegawai_pin, p.pegawai_nip, p.pegawai_nama,
            p.tempat_lahir, p.pegawai_privilege, p.pegawai_telp, p.pegawai_status,
            IFNULL(p.tgl_lahir, '0001-01-01') AS tgl_lahir,
            IFNULL(pb1.pembagian1_nama, '') AS jabatan,
            IFNULL(pb2.skpd, '') AS skpd,
            IFNULL(pb3.pembagian3_nama, '') AS sotk,
            p.tgl_mulai_kerja, p.gender,
            p.photo_path,
            p.no_rek AS deviceid,
            pb2.latitude,
            pb2.longitude,
            pb2.sn
            FROM pegawai p
            LEFT JOIN pembagian1 pb1 ON pb1.pembagian1_id = p.pembagian1_id
            LEFT JOIN e_skpd pb2 ON pb2.pembagian2_id = p.pembagian2_id
            LEFT JOIN pembagian3 pb3 ON pb3.pembagian3_id = p.pembagian3_id
            WHERE p.pegawai_pin = @pegawaiPin
            LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        // pakai object dulu biar fleksibel (atau bikin record PegawaiRow)
        return await conn.QueryFirstOrDefaultAsync(
            new CommandDefinition(sql, new { pegawaiPin }, cancellationToken: ct)
        );
    }

    public async Task<int> GetPegawaiIdByPinAsync(string pin, CancellationToken ct)
    {
        const string sql = @"SELECT pegawai_id FROM pegawai WHERE pegawai_pin = @pin LIMIT 1;";
        await using var conn = factory.Create();
        await conn.OpenAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(sql, new { pin }, cancellationToken: ct));
    }

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
