namespace entago_api_mysql.Dtos;

public sealed record ShiftResultResponse(
    int Pegawai_Id,
    int Pegawai_Pin,
    //DateTime? Scan_In,
    //DateTime? Scan_Out,
    string? Scan_In,
    string? Scan_Out,
    string? Hari
);

public sealed class ShiftResultTodayDto
{
    public int Pegawai_Id { get; set; }
    public DateTime Tgl_Shift { get; set; }
    public string Checkin { get; set; } = "";
    public string Checkout { get; set; } = "";
}