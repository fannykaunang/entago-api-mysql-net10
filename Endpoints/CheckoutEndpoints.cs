using System.Security.Claims;
using entago_api_mysql.Dtos;
using entago_api_mysql.Services;

namespace entago_api_mysql.Endpoints;

public static class CheckoutEndpoints
{
    public static RouteGroupBuilder MapCheckoutEndpoints(this RouteGroupBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/checkout");

        // GET /api/checkout/{pin}?date=2026-01-27  (opsional)
        group.MapGet("/{pin}", async (
            string pin,
            DateTime? date,
            CheckoutService svc,
            CancellationToken ct) =>
        {
            var data = await svc.GetAttLogByPinAsync(pin, date, ct);
            return Results.Ok(new { success = true, message = "History checkout berhasil dimuat", data });
        });

        // POST /api/checkout  (JSON)
        group.MapPost("/", async (
            HttpContext ctx,
            CheckoutCreateRequest req,
            CheckoutService svc,
            PegawaiService pegawaiSvc,
            CancellationToken ct) =>
        {
            // ambil pin dari JWT (lebih aman daripada body)
            var pinClaim = svc.GetPinFromJwt(ctx.User);
            if (string.IsNullOrWhiteSpace(pinClaim))
                return Results.Unauthorized();

            if (ctx.Request.Headers.TryGetValue("X-Device-Id", out var deviceHeader))
            {
                var deviceId = deviceHeader.ToString();
                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    var ok = await pegawaiSvc.CheckDeviceAsync(req.Pin.ToString(), deviceId, ct);
                    if (ok is null)
                        return Results.Ok(new { success = false, result = 10, response = "Device tidak cocok!" });
                }
            }
            //TODO anda berada diluar jam absen
            // resolve pegawai_id dari pin
            var pegawaiId = await svc.ResolvePegawaiIdByPinAsync(pinClaim, ct);
            if (pegawaiId <= 0)
                return Results.Ok(new { success = false, result = 6, response = "pin tidak ditemukan!" });

            // validasi jam checkout (ikuti logic legacy)
            var now = DateTime.Now.TimeOfDay;

            var startLuarAbsenPagi = new TimeSpan(7, 30, 0);
            var endLuarAbsenPagi = new TimeSpan(15, 59, 0);

            var startLuarJamMalam = new TimeSpan(18, 1, 0);
            var endLuarJamMalam = new TimeSpan(24, 0, 0);

            var startLuarJamSubuh = new TimeSpan(0, 0, 0);
            var endLuarJamSubuh = new TimeSpan(7, 29, 0);

            if (now > startLuarAbsenPagi && now < endLuarAbsenPagi)
                return Results.Ok(new { success = false, result = 2, response = "Anda berada diluar jam absen (pagi-siang)!" });
            //if (now > startLuarJamMalam && now < endLuarJamMalam)
            // return Results.Ok(new { success = false, result = 4, response = "Anda berada diluar Jam Absen (malam)!" });
            if (now > startLuarJamSubuh && now < endLuarJamSubuh)
                return Results.Ok(new { success = false, result = 5, response = "Anda berada diluar Jam Absen (subuh)!" });

            var today = DateTime.Today;

            // sudah checkout?
            if (await svc.HasCheckoutTodayAsync(pegawaiId, today, ct))
                return Results.Ok(new { success = false, result = 7, response = "Anda sudah absen pulang!" });

            // sedang izin?
            if (await svc.HasIzinTodayAsync(pegawaiId, today, ct))
                return Results.Ok(new { success = false, result = 8, response = "Mohon maaf, hari ini Anda sedang mengajukan Izin!" });

            // ambil sn dari request (opsional)
            var sn = (req.Sn ?? "").Trim();

            // kalau sudah ada row shift_result hari ini → update, else insert
            if (await svc.HasShiftRowTodayAsync(pegawaiId, today, ct))
            {
                await svc.UpdateCheckoutIfExistsAsync(pegawaiId, pinClaim, sn, ct);
                return Results.Ok(new { success = true, result = 1, response = "Absen pulang berhasil di-update" });
            }

            await svc.InsertCheckoutAsync(pegawaiId, pinClaim, sn, ct);
            return Results.Ok(new { success = true, result = 1, response = "Anda berhasil absen pulang!", time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
        });

        return group;
    }
}
