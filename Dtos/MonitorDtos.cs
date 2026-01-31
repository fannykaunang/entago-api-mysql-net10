namespace entago_api_mysql.Dtos;

public sealed class MachineEntity
{
    public int Dev_Id { get; set; }
    public string Skpd_Alias { get; set; } = "";
    public string Device_Name { get; set; } = "";
    public string Ip_Address { get; set; } = "";
}


public sealed record MachineStatusDto(
    int No,
    DateTime Waktu,
    string Ip_Address,
    string Skpd_Alias,
    string Device_Name,
    string Status,
    long? RoundtripMs
);

public sealed record MonitorDevicesResponse(
    int Online,
    int Offline,
    int Total,
    IReadOnlyList<MachineStatusDto> Data
);
