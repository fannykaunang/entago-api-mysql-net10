using entago_api_mysql.Services;

namespace entago_api_mysql.Endpoints;

public static class IzinListEndpoints
{
    public static RouteGroupBuilder MapIzinListEndpoints(this RouteGroupBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/izin");

        // GET /api/izin/pegawai/{pegawai_id}
        group.MapGet("/pegawai/{pegawai_id:int}", async (
            int pegawai_id,
            IzinListService svc,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (pegawai_id <= 0)
                return Results.BadRequest(new { success = false, message = "pegawai_id tidak valid" });

            // OPTIONAL GUARD (aktifkan kalau cocok dengan sistemmu):
            // Kalau "sub" di JWT kamu adalah userid e_user, ini bukan pegawai_id.
            // Jadi JANGAN aktifkan guard ini kalau mappingnya beda.
            //
            // var sub = http.User.FindFirst("sub")?.Value;
            // if (!string.IsNullOrEmpty(sub) && int.TryParse(sub, out var jwtUserId))
            // {
            //     // kalau kamu punya relasi e_user.userid -> pegawai_id, baru bisa divalidasi
            // }

            var data = await svc.GetListByPegawaiIdAsync(pegawai_id, ct);

            if (data.Count == 0)
                return Results.NoContent(); // 204, sama seperti controller lama

            return Results.Ok(new { success = true, message = "List izin berhasil dimuat", data });
        });

        return group;
    }
}
