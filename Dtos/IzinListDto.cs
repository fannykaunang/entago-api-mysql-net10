namespace entago_api_mysql.Services;

public sealed class IzinListDto
{
    public long Izin_Id { get; set; }
    public int Pegawai_Id { get; set; }

    public DateTime? Izin_Tgl_Pengajuan { get; set; }
    public DateTime? Izin_Tgl { get; set; }

    public short Izin_Jenis_Id { get; set; }      // MySQL smallint -> short
    public string Izin_Jenis_Name { get; set; } = "";

    public string? Kat_Izin_Nama { get; set; }
    public string? Izin_Catatan { get; set; }

    public sbyte Izin_Status { get; set; }        // tinyint -> sbyte

    public TimeSpan? Izin_Tinggal_T1 { get; set; } // TIME -> TimeSpan
    public TimeSpan? Izin_Tinggal_T2 { get; set; }

    public int Cuti_N_Id { get; set; }
    public string? Izin_Ket_Lain { get; set; }
    public TimeSpan? Izin_Noscan_Time { get; set; } // TIME -> TimeSpan

    public int? Kat_Izin_Id { get; set; }
    public string? Ket_Status { get; set; }

    public string? File_Name { get; set; }
    public string? File_Extension { get; set; }
    public string? File_Size { get; set; }
    public string? File_Path { get; set; }

    public long Izin_Urutan { get; set; }
}
