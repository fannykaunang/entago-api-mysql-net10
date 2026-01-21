using System.Globalization;
using System.Security.Claims;
using entago_api_mysql.Dtos;
using entago_api_mysql.Services;

namespace entago_api_mysql.Endpoints;

public static class CheckinEndpoints
{
    public static IEndpointRouteBuilder MapCheckinEndpoints(this IEndpointRouteBuilder api)
    {
        // api di sini sudah /api dan sudah RequireAuthorization()
        var group = api.MapGroup("/checkin");

        // GET /api/checkin/{pegawai_pin}
        group.MapGet("/{pegawai_pin:int}", async (int pegawai_pin, CheckinService svc, CancellationToken ct) =>
        {
            var result = await svc.FindShiftResultsByPinAsync(pegawai_pin, ct);
            if (result is null)
                return Results.NotFound(new { result = 0, response = "Data Absensi tidak ditemukan!" });

            return Results.Ok(result);
        });

        // GET /api/checkin/{pin}/{scan_date} (yyyyMMdd)
        group.MapGet("/{pin:int}/{scan_date}", async (int pin, string scan_date, CheckinService svc, CancellationToken ct) =>
        {
            if (!DateTime.TryParseExact(scan_date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return Results.BadRequest(new { success = false, message = "Format scan_date harus yyyyMMdd" });

            var result = await svc.FindAttLogByPinAndDateAsync(pin, dt, ct);
            if (result is null) return Results.NotFound();

            return Results.Ok(result);
        });

        // GET /api/checkin/morning-checkin?pin=1813&date=2025-01-08 (yyyy-MM-dd)
        group.MapGet("/morning-checkin", async (int pin, string date, CheckinService svc, CancellationToken ct) =>
        {
            if (pin <= 0 || string.IsNullOrWhiteSpace(date))
                return Results.Ok(new { success = false, checkin_time = (string?)null });

            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return Results.Ok(new { success = false, checkin_time = (string?)null });

            var morning = await svc.GetMorningCheckinByPinAndDateAsync(pin, dt, ct);
            if (morning.HasValue)
            {
                var formatted = morning.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                return Results.Ok(new { success = true, checkin_time = formatted });
            }

            return Results.Ok(new { success = false, checkin_time = (string?)null });
        });

        // POST /api/checkin
        group.MapPost("/", async (
            HttpContext ctx,
            CheckinCreateRequest body,
            CheckinService checkinSvc,
            PegawaiService pegawaiSvc,
            CancellationToken ct) =>
        {
            // (OPSIONAL) pastikan pin di JWT = pin di body
            var pinClaim = ctx.User.FindFirst("pin")?.Value;
            if (!string.IsNullOrWhiteSpace(pinClaim) && int.TryParse(pinClaim, out var jwtPin))
            {
                if (jwtPin != body.Pin)
                    return Results.Forbid();
            }

            // (OPSIONAL) cek deviceid via header
            if (ctx.Request.Headers.TryGetValue("X-Device-Id", out var deviceHeader))
            {
                var deviceId = deviceHeader.ToString();
                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    var ok = await pegawaiSvc.CheckDeviceAsync(body.Pin.ToString(), deviceId, ct);
                    if (ok is null)
                        return Results.Ok(new { result = 10, response = "Device tidak cocok!" });
                }
            }

            // Cek sudah absen hari ini
            var today = DateTime.Now.Date;
            var exists = await checkinSvc.FindAttLogByPinAndDateAsync(body.Pin, today, ct);
            if (exists is not null && exists.Count >= 1)
                return Results.Ok(new { result = 7, response = "Anda sudah absen!" });

            // Cek izin hari ini
            var izin = await checkinSvc.FindIzinHariIniAsync(body.Pegawai_Id, ct);
            if (izin is not null && izin.Count >= 1)
                return Results.Ok(new { result = 8, response = "Mohon maaf, hari ini Anda sedang mengajukan Izin!" });

            // Aturan jam seperti sistem lama
            var now = DateTime.Now.TimeOfDay;

            var start_terlambat = new TimeSpan(9, 0, 0);
            var end_terlambat = new TimeSpan(12, 0, 0);

            var start_luar_jam_siang = new TimeSpan(12, 1, 0);
            var end_luar_jam_siang = new TimeSpan(15, 59, 0);

            var start_luar_jam_checkout = new TimeSpan(16, 0, 0);
            var end_luar_jam_checkout = new TimeSpan(17, 59, 0);

            var start_luar_jam_malam = new TimeSpan(18, 0, 0);
            var end_luar_jam_malam = new TimeSpan(24, 0, 0);

            var start_luar_jam_subuh = new TimeSpan(0, 0, 0);
            var end_luar_jam_subuh = new TimeSpan(7, 29, 0);

            if ((now > start_terlambat) && (now < end_terlambat))
                return Results.Ok(new { result = 2, response = "Anda hari ini datang terlambat!" });

            if ((now > start_luar_jam_siang) && (now < end_luar_jam_siang))
                return Results.Ok(new { result = 3, response = "Anda berada diluar Jam Absen!" });

            if ((now > start_luar_jam_checkout) && (now < end_luar_jam_checkout))
                return Results.Ok(new { result = 9, response = "Anda berada diluar Jam Absen (checkout)!" });

            if ((now > start_luar_jam_malam) && (now < end_luar_jam_malam))
                return Results.Ok(new { result = 4, response = "Anda berada diluar Jam Absen!" });

            if ((now > start_luar_jam_subuh) && (now < end_luar_jam_subuh))
                return Results.Ok(new { result = 5, response = "Anda berada diluar Jam Absen!" });

            // Insert ke att_log
            var scanDate = body.Scan_Date ?? DateTime.Now;
            await checkinSvc.InsertAttLogAsync(body, scanDate, ct);

            return Results.Ok(new
            {
                result = 1,
                response = "Anda berhasil absen datang",
                scan_in_disimpan_di_storage_hp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        });

        return api;
    }
}
