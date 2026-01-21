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
