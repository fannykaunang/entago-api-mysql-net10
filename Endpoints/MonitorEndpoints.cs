using entago_api_mysql.Services;
using Microsoft.AspNetCore.Mvc;

namespace entago_api_mysql.Endpoints;

public static class MonitorEndpoints
{
    public static RouteGroupBuilder MapMonitorEndpoints(this RouteGroupBuilder apiGroup)
    {
        var group = apiGroup.MapGroup("/monitor");

        // GET /api/monitor/devices?skpdid=1
        group.MapGet("/devices", async (
            [FromQuery] int? skpdid, // <-- otomatis diambil dari querystring ?skpdid=...
            [FromServices] MonitorService svc,
            CancellationToken ct) =>
        {
            if (skpdid <= 0)
                return Results.BadRequest(new { success = false, message = "Parameter skpdid wajib dan harus > 0." });

            var data = await svc.GetDeviceStatusAsync(skpdid, ct);

            return Results.Ok(new
            {
                success = true,
                message = "Status perangkat berhasil dimuat",
                skpdid,
                online = data.Online,
                offline = data.Offline,
                total = data.Total,
                data = data.Data
            });
        });

        return group;
    }
}
