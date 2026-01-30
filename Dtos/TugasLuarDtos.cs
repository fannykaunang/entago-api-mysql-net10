using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace entago_api_mysql.Dtos;

public sealed class TugasLuarDto
{
    public int Tugas_Luar_Id { get; set; }
    public int Pegawai_Id { get; set; }
    public DateTime Tugas_Tgl { get; set; }
    public string Tujuan { get; set; } = "";
    public string Keterangan_Tugas { get; set; } = "";
    public string Alamat { get; set; } = "";
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
    public sbyte Is_Verified { get; set; } // 0 ditolak, 1 diterima, 2 baru
    public string File_Name { get; set; } = "";
    public string File_Extension { get; set; } = "";
    public string File_Size { get; set; } = "";
    public string File_Path { get; set; } = "";
}

public sealed class TugasLuarCreateResultDto
{
    public int Tugas_Luar_Id { get; set; }
    public DateTime Tugas_Tgl { get; set; }
    public string File_Path { get; set; } = "";
}

public sealed class TugasLuarCreateForm
{
    public DateTime? Tugas_Tgl { get; set; } // ✅ nullable
    public string Tujuan { get; set; } = "";
    public string Keterangan_Tugas { get; set; } = "";
    public string Alamat { get; set; } = "";
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
    public IFormFile Foto { get; set; } = default!;
}

public sealed class TugasLuarUpdateForm
{
    public DateTime? Tugas_Tgl { get; set; }          // opsional (kalau mau bisa diubah)
    public string? Tujuan { get; set; }
    public string? Keterangan_Tugas { get; set; }
    public string? Alamat { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public IFormFile? Foto { get; set; }              // opsional
}
