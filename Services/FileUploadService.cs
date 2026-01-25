using Microsoft.Extensions.Options;
using entago_api_mysql.Options;

namespace entago_api_mysql.Services;

public sealed class FileUploadService(IOptions<UploadOptions> opt)
{
    private readonly UploadOptions _o = opt.Value;

    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    { "image/jpeg", "image/png", "image/webp" };

    public async Task<(string fileName, string ext, long size, string publicPath)> SaveTugasLuarAsync(
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new InvalidOperationException("File tidak boleh kosong.");

        // ✅ Batasi ukuran (5MB)
        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes)
            throw new InvalidOperationException("Ukuran file terlalu besar (maks 5MB).");

        // ✅ Validasi extension
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExt.Contains(ext))
            throw new InvalidOperationException("Format file tidak didukung. Gunakan JPG/PNG/WEBP.");

        // ✅ Validasi content-type (opsional tapi bagus)
        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
            throw new InvalidOperationException("Content-Type tidak valid. Gunakan image/jpeg, image/png, atau image/webp.");

        // ✅ Validasi signature / magic bytes (lebih penting daripada extension)
        await EnsureLooksLikeImageAsync(file, ext, ct);

        var root = _o.TugasLuarPhysicalRoot;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Konfigurasi Uploads:TugasLuarPhysicalRoot belum di-set.");

        Directory.CreateDirectory(root);

        // ✅ Nama acak aman
        var safeName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(root, safeName);

        // ✅ Simpan file
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(fs, ct);
        }

        var publicBase = _o.TugasLuarPublicBaseUrl?.TrimEnd('/') ?? "/backend/skpd/files/tugas-luar";
        var publicPath = $"{publicBase}/{safeName}";

        return (safeName, ext.TrimStart('.').ToLowerInvariant(), file.Length, publicPath);
    }

    private static async Task EnsureLooksLikeImageAsync(IFormFile file, string ext, CancellationToken ct)
    {
        // Baca beberapa byte pertama saja
        byte[] header = new byte[32];
        await using var s = file.OpenReadStream();
        var read = await s.ReadAsync(header, 0, header.Length, ct);

        if (read < 12)
            throw new InvalidOperationException("File tidak valid / terlalu kecil.");

        // JPEG: FF D8 FF
        bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                     header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

        // WEBP: "RIFF" .... "WEBP"
        bool isWebp = header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                      header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';

        // Cocokkan minimal terhadap ekstensi yang diklaim
        ext = ext.ToLowerInvariant();
        if ((ext is ".jpg" or ".jpeg") && !isJpeg)
            throw new InvalidOperationException("File bukan JPEG yang valid.");
        if (ext == ".png" && !isPng)
            throw new InvalidOperationException("File bukan PNG yang valid.");
        if (ext == ".webp" && !isWebp)
            throw new InvalidOperationException("File bukan WEBP yang valid.");
    }
}
