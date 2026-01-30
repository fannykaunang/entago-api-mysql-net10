namespace entago_api_mysql.Dtos;

public sealed class CheckoutCreateRequest
{
    // dari Android (mirip checkin)
    public string Att_Id { get; set; } = "";      // boleh dikirim, tapi server tetap generate att_id_out sendiri
    public int InoutMode { get; set; } = 1;       // checkout biasanya 1
    public int Pegawai_Id { get; set; }           // boleh dikirim, tapi akan divalidasi/diabaikan (pakai JWT)
    public int Pin { get; set; }                  // boleh dikirim, tapi akan divalidasi/diabaikan (pakai JWT)
    public int Reserved { get; set; } = 0;
    public DateTime Scan_Date { get; set; } = DateTime.Now;
    public string Sn { get; set; } = "";          // device id (usahakan pendek, bukan FCM token)
    public int VerifyMode { get; set; } = 5;      // 5 = verify by android (legacy)
    public int Work_Code { get; set; } = 0;
}

public sealed class CheckoutAttLogDto
{
    public string Sn { get; set; } = "";
    public string Scan_Date { get; set; } = "";
    public string Pin { get; set; } = "";
    public int VerifyMode { get; set; }
    public int InoutMode { get; set; }
    public int Reserved { get; set; }
    public int Work_Code { get; set; }
    public string Att_Id { get; set; } = "";
}
