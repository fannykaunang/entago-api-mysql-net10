using System.Security.Claims;
using entago_api_mysql.Services;

namespace entago_api_mysql.Endpoints;

public static class PegawaiEndpoints
{
    public static RouteGroupBuilder MapPegawaiEndpoints(this RouteGroupBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/pegawai");

        // GET /api/pegawai  (butuh: X-Api-Key + Authorization: Bearer)
        group.MapGet("/", async (PegawaiService svc, CancellationToken ct) =>
        {
            var data = await svc.GetAllAsync(ct);
            return Results.Ok(new { success = true, message = "Data pegawai berhasil dimuat", data });
        });

        // GET /api/pegawai/pin
        group.MapGet("/{pegawai_pin}", async (
            HttpContext ctx,
            string pegawai_pin,
            PegawaiService svc,
            CancellationToken ct) =>
        {
            pegawai_pin = pegawai_pin?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(pegawai_pin))
                return Results.BadRequest(new { success = false, message = "PIN wajib diisi" });

            // Ambil pin & level dari JWT
            var jwtPin = ctx.User.FindFirstValue("pin") ?? "";
            var jwtLevelStr = ctx.User.FindFirstValue("level") ?? "0";
            _ = int.TryParse(jwtLevelStr, out var jwtLevel);

            // ✅ Security rule:
            // - level 0 (user biasa) hanya boleh request pin miliknya
            // - level >= 1 boleh request pin siapa saja
            if (jwtLevel == 0 && !string.Equals(jwtPin, pegawai_pin, StringComparison.Ordinal))
            {
                return Results.Json(new { success = false, message = "Forbidden" }, statusCode: 403);
            }

            var row = await svc.GetByPinAsync(pegawai_pin, ct);

            if (row is null)
                return Results.NotFound(new { success = false, message = "Pegawai tidak ditemukan", data = (object?)null });

            return Results.Ok(new { success = true, message = "Data pegawai berhasil dimuat", data = row });
        });

        group.MapGet("/device-check", async (
            string pegawai_pin,
            string no_rek,
            PegawaiService svc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(pegawai_pin) || string.IsNullOrWhiteSpace(no_rek))
                return Results.BadRequest(new { success = false, message = "pegawai_pin dan no_rek wajib diisi" });

            var row = await svc.CheckDeviceAsync(pegawai_pin.Trim(), no_rek.Trim(), ct);

            if (row is null)
                return Results.Ok(new { success = false, message = "Device tidak cocok", data = (object?)null });

            return Results.Ok(new { success = true, message = "Device cocok", data = row });
        });


        return group;
    }
}
