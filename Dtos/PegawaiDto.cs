namespace entago_api_mysql.Dtos;

public sealed class PegawaiDto
{
    public int Pegawai_Id { get; set; }
    public string? Pegawai_Pin { get; set; }
    public string? Pegawai_Nip { get; set; }
    public string? Pegawai_Nama { get; set; }
    public string? Tempat_Lahir { get; set; }
    public string? Pegawai_Privilege { get; set; }
    public string? Pegawai_Telp { get; set; }
    public string? Pegawai_Status { get; set; }

    public string? Tgl_Lahir { get; set; } // "yyyy-MM-dd" atau ""

    public string? Jabatan { get; set; }
    public string? Skpd { get; set; }
    public string? Sotk { get; set; }

    public string? Tgl_Mulai_Kerja { get; set; } // "yyyy-MM-dd" atau ""
    public string? Gender { get; set; }
    public string? Photo_Path { get; set; }
    public string? Deviceid { get; set; }
}
