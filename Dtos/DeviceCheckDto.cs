namespace entago_api_mysql.Dtos;

public sealed class DeviceCheckDto
{
    public string? No_Rek { get; set; }       // deviceid
    public int Pegawai_Id { get; set; }
    public string? Pegawai_Pin { get; set; }
}
