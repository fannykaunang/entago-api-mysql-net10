namespace entago_api_mysql.Dtos;

public sealed record ShiftResultResponse(
    int Pegawai_Id,
    int Pegawai_Pin,
    //DateTime? Scan_In,
    //DateTime? Scan_Out,
    string? Scan_In,
    string? Scan_Out
);
