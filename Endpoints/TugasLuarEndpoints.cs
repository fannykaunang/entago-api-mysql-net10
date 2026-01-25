using System.Security.Claims;
using Dapper;
using entago_api_mysql.Dtos;
using entago_api_mysql.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace entago_api_mysql.Endpoints;

public static class TugasLuarEndpoints
{
    public static RouteGroupBuilder MapTugasLuarEndpoints(this RouteGroupBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/tugas-luar");

        // GET /api/tugas-luar?start=2026-01-01&end=2026-02-01
        group.MapGet("/", async (
            HttpContext ctx,
            TugasLuarService svc,
            DateTime? start,
            DateTime? end,
            CancellationToken ct) =>
        {
            var pegawaiId = await ResolvePegawaiIdFromJwtAsync(ctx, ct);
            if (pegawaiId == 0) return Results.Unauthorized();

            // end eksklusif biar gampang (mis. end = 2026-02-01)
            var data = await svc.GetListByPegawaiAsync(pegawaiId, start, end, ct);

            return Results.Ok(new
            {
                success = true,
                message = "Daftar tugas luar berhasil dimuat",
                data
            });
        });

        // POST /api/tugas-luar (multipart/form-data)
        // fields: tujuan, keterangan_tugas, alamat, latitude, longitude, foto
        group.MapPost("/", async (
            HttpContext ctx,
            [FromForm] TugasLuarCreateForm form,
            TugasLuarService svc,
            FileUploadService upload,
            CancellationToken ct) =>
        {
            // ambil pegawaiId dari JWT (lebih aman)
            var pegawaiId = await ResolvePegawaiIdFromJwtAsync(ctx, ct);
            if (pegawaiId == 0) return Results.Unauthorized();

            // Tentukan tanggal "hari ini" untuk pengecekan (DATE)
            var now = DateTime.Now;
            var today = now.Date;

            var tugasTgl = form.Tugas_Tgl ?? now;

            // 1) Cek sudah absen hari ini? (shift_result)
            var hasAbsen = await svc.HasAnyAttendanceTodayAsync(pegawaiId, today, ct);
            if (hasAbsen)
                return Results.BadRequest(new { success = false, message = "Tidak bisa input tugas luar karena hari ini sudah absen." });

            // 2) Cek sudah input tugas luar hari ini?
            var alreadyTugasLuar = await svc.HasTugasLuarTodayAsync(pegawaiId, today, ct);
            if (alreadyTugasLuar)
                return Results.BadRequest(new { success = false, message = "Tugas luar hari ini sudah pernah diinput." });

            // 3) Simpan foto ke folder web
            var saved = await upload.SaveTugasLuarAsync(form.Foto, ct);

            // 4) Insert DB
            var id = await svc.InsertTugasLuarAsync(new TugasLuarDto
            {
                Pegawai_Id = pegawaiId,
                Tugas_Tgl = tugasTgl,
                Tujuan = form.Tujuan,
                Keterangan_Tugas = form.Keterangan_Tugas,
                Alamat = form.Alamat,
                Latitude = form.Latitude,
                Longitude = form.Longitude,
                Is_Verified = 2,
                File_Name = saved.fileName,
                File_Extension = saved.ext,
                File_Size = saved.size.ToString(),
                File_Path = saved.publicPath
            }, ct);

            return Results.Ok(new
            {
                success = true,
                message = "Tugas luar berhasil disimpan",
                data = new { tugas_luar_id = id, file_url = saved.publicPath }
            });
        })
        .Accepts<TugasLuarCreateForm>("multipart/form-data")
        .DisableAntiforgery();

        return group;
    }

    // ===========================
    // Helper: resolve pegawai_id
    // ===========================
    private static async Task<int> ResolvePegawaiIdFromJwtAsync(HttpContext ctx, CancellationToken ct)
    {
        // Opsi 1 (disarankan): token kamu sudah punya claim pin
        var pin = ctx.User.FindFirstValue("pin");
        if (string.IsNullOrWhiteSpace(pin)) return 0;

        // Cari pegawai_id by pin dari DB (biar user tidak bisa spoof pegawai_id lewat body)
        var factory = ctx.RequestServices.GetRequiredService<MySqlConnectionFactory>();
        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        const string sql = @"SELECT pegawai_id FROM pegawai WHERE pegawai_pin = @pin LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<int>(
            new Dapper.CommandDefinition(sql, new { pin }, cancellationToken: ct)
        );
    }
}
