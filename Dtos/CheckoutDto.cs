namespace entago_api_mysql.Dtos;

public sealed class CheckoutDto
{
    public int Pegawai_Id { get; set; }
    public string Sn { get; set; } = "";
    public int Pin { get; set; }
}
