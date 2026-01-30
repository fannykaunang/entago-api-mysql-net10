namespace entago_api_mysql.Dtos;

public sealed class ShiftResultDto
{
    public int Pegawai_Id { get; set; }
    public int Pegawai_Pin { get; set; }
    public DateTime? Scan_In { get; set; }
    public DateTime? Scan_Out { get; set; }
    public string Hari { get; set; } = "";
}

public sealed class AttLogDto
{
    public string Sn { get; set; } = "";
    public string Scan_Date { get; set; } = "";
    public int Pin { get; set; }
    public int Verifymode { get; set; }
    public int Inoutmode { get; set; }
    public int Reserved { get; set; }
    public int Work_Code { get; set; }
    public string Att_Id { get; set; } = "";
}

public sealed class IzinResultDto
{
    public int Pegawai_Id { get; set; }
    public string Tgl_Shift { get; set; } = ""; // yyyyMMdd
    public int Izin_Jenis_Id { get; set; }
}

// Ini pengganti [FromForm] Checkin body pada POST lama
public sealed class CheckinCreateRequest
{
    public int Pegawai_Id { get; set; }
    public int Pin { get; set; }

    public string Sn { get; set; } = "";
    public int Verifymode { get; set; }
    public int Inoutmode { get; set; }
    public int Reserved { get; set; }
    public int Work_Code { get; set; }
    public string Att_Id { get; set; } = "";

    // tambahan utk SP_INSERT_CHECKIN
    public DateOnly? Tgl_Shift { get; set; }          // optional, kalau null -> pakai hari ini
    public int Jdw_Kerja_M_Id { get; set; }
    public int Jk_Id { get; set; }
    public string Att_Id_In { get; set; } = "";       // bisa sama dengan Att_Id kalau kamu belum punya khusus
    public int Late_Minute { get; set; }
    public int Late { get; set; }                     // 0/1

    public DateTime? Scan_Date { get; set; }          // optional, tapi SP kamu pakai NOW() untuk scan_date & scan_in
}

