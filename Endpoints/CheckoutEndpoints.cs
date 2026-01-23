using entago_api_mysql.Dtos;
using entago_api_mysql.Services;
using System.Security.Claims;

namespace entago_api_mysql.Endpoints;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/checkout");

        // POST /api/checkout
        group.MapPost("", async (
           HttpContext ctx,
           CheckoutDto body,
           CheckoutService checkoutSvc,
           CancellationToken ct) =>
        {
            var pinFromToken = ctx.User.FindFirstValue("pin"); // claim "pin"

            // Kalau token tidak punya claim pin -> unauthorized (token invalid / salah config)
            if (string.IsNullOrWhiteSpace(pinFromToken))
                return Results.Unauthorized();

            // Kalau pin body beda dengan pin token -> forbidden
            if (!string.Equals(body.Pin.ToString(), pinFromToken.Trim(), StringComparison.Ordinal))
                return Results.Forbid();

            // Basic validation
            if (string.IsNullOrWhiteSpace(body.Pin.ToString()) || string.IsNullOrWhiteSpace(body.Sn))
                return Results.Ok(new { result = 0, response = "Data tidak lengkap (pin/sn)" });

            // Validasi jam checkout (pakai waktu server)
            var now = DateTime.Now.TimeOfDay;

            var startLuarPagi = new TimeSpan(7, 30, 0);
            var endLuarPagi = new TimeSpan(15, 59, 0);

            var startLuarMalam = new TimeSpan(18, 1, 0);
            var endLuarMalam = new TimeSpan(24, 0, 0);

            var startLuarSubuh = new TimeSpan(0, 0, 0);
            var endLuarSubuh = new TimeSpan(7, 29, 0);

            if (now > startLuarPagi && now < endLuarPagi)
                return Results.Ok(new { success = false, result = 2, message = "Anda berada diluar jam absen (pagi-siang)!" });

            if (now > startLuarMalam && now < endLuarMalam)
                return Results.Ok(new { success = false, result = 3, message = "Anda berada diluar Jam Absen (malam)!" });

            if (now > startLuarSubuh && now < endLuarSubuh)
                return Results.Ok(new { success = false, result = 4, message = "Anda berada diluar Jam Absen (subuh)!" });

            // Cari pegawai_id dari pin (meniru result==null di sistem lama)
            var pegawai = await checkoutSvc.FindPegawaiByPinAsync(body.Pin, ct);
            if (pegawai is null)
                return Results.NotFound(new { success = false, result = 5, message = "pin tidak ditemukan!" });

            var pegawaiId = pegawai.Pegawai_Id;
            var tglShift = DateTime.Today;

            // Sudah checkout?
            if (await checkoutSvc.HasCheckoutTodayAsync(pegawaiId, tglShift, ct))
                return Results.Ok(new { success = false, result = 6, message = "Anda sudah absen pulang!" });

            // Sedang izin?
            if (await checkoutSvc.HasIzinTodayAsync(pegawaiId, tglShift, ct))
                return Results.Ok(new { success = false, result = 7, message = "Mohon maaf, hari ini Anda sedang mengajukan Izin!" });

            // Jika shift_result hari ini sudah ada -> update
            if (await checkoutSvc.HasShiftRowTodayAsync(pegawaiId, tglShift, ct))
            {
                await checkoutSvc.UpdateCheckoutIfExistsAsync(body, pegawaiId, tglShift, ct);
                return Results.Ok(new { success = true, result = 1, message = "Absen pulang berhasil di-update" });
            }

            // Jika belum ada shift_result hari ini -> insert
            await checkoutSvc.InsertCheckoutAsync(body, pegawaiId, tglShift, ct);
            return Results.Ok(new { success = true, result = 1, message = "Anda berhasil absen pulang!" });
        });

        return api;
    }
}
