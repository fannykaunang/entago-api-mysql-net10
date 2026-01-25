namespace entago_api_mysql.Dtos;

public sealed class MonthlyRecapDto
{
    public int Pegawai_Id { get; set; }
    public int Pegawai_Pin { get; set; }
    public string Pegawai_Nama { get; set; } = "";

    public string Periode_Bulan { get; set; } = ""; // "2026-01"

    public int Total_Hari_Kalender { get; set; }
    public int Total_Hari_Libur { get; set; }
    public int Total_Hari_Kerja { get; set; }

    public int Hadir { get; set; }
    public int Izin { get; set; }
    public int Alpa { get; set; }

    public int Total_Menit_Kerja { get; set; }
    public decimal Total_Jam_Kerja { get; set; }
    public decimal Persentase_Kehadiran { get; set; }
}
