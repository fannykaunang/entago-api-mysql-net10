using System.Security.Claims;
using entago_api_mysql.Services;

namespace entago_api_mysql.Endpoints;

public static class MonthlyReportEndpoints
{
    public static RouteGroupBuilder MapMonthlyReportEndpoints(this RouteGroupBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/rekap-bulanan");

        // GET /api/rekap-bulanan?year=2026&excludeWeekend=true
        // atau GET /api/rekap-bulanan?start=2026-01-01&end=2026-12-31
        group.MapGet("/", async (
            HttpContext ctx,
            int? year,
            DateTime? start,
            DateTime? end,
            bool? excludeWeekend,
            MonthlyReportService svc,
            CancellationToken ct) =>
        {
            // Ambil PIN dari JWT claim
            var pinStr = ctx.User.FindFirstValue("pin");
            if (string.IsNullOrWhiteSpace(pinStr) || !int.TryParse(pinStr, out var pin))
                return Results.Unauthorized();

            var pegawaiId = await svc.ResolvePegawaiIdByPinAsync(pin, ct);
            if (pegawaiId is null)
                return Results.NotFound(new { success = false, message = "Pegawai tidak ditemukan" });

            // Tentukan range tanggal
            DateTime startDate;
            DateTime endDate;

            if (start.HasValue && end.HasValue)
            {
                startDate = start.Value.Date;
                endDate = end.Value.Date;
            }
            else
            {
                // fallback pakai year (default: tahun sekarang)
                var y = year ?? DateTime.Now.Year;
                startDate = new DateTime(y, 1, 1);
                endDate = new DateTime(y, 12, 31);
            }

            if (endDate < startDate)
                return Results.BadRequest(new { success = false, message = "end harus >= start" });

            var rows = await svc.GetMonthlyRecapAsync(
                pegawaiId.Value,
                startDate,
                endDate,
                excludeWeekend ?? true,
                ct
            );

            return Results.Ok(new
            {
                success = true,
                message = "Rekap bulanan berhasil dimuat",
                data = rows
            });
        });

        return group;
    }
}
