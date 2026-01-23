using entago_api_mysql.Services;
using Microsoft.Extensions.Caching.Memory;

namespace entago_api_mysql.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // POST /api/auth/login
        _ = group.MapPost("/login", async (
            HttpContext ctx,
            LoginRequest req,
            AuthService auth,
            IMemoryCache cache,
            CancellationToken ct) =>
        {

            var apiClientId = ctx.Items["ApiClientId"]?.ToString() ?? "0";
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var email = (req.Email ?? "").Trim().ToLowerInvariant();

            // key brute-force
            var failKey = $"login:fail:{apiClientId}:{ip}:{email}";
            var blockKey = $"login:block:{apiClientId}:{ip}:{email}";

            // kalau sedang diblok
            if (cache.TryGetValue(blockKey, out _))
                return Results.Json(new { success = false, message = "Terlalu banyak percobaan login. Coba lagi nanti." }, statusCode: 429);

            var user = await auth.FindActiveUserByEmailAsync(email, ct);
            if (user is null)
            {
                RegisterFail(cache, failKey, blockKey);
                return Results.Json(new { success = false, message = "Email / user tidak ditemukan atau tidak aktif" }, statusCode: 401);
            }

            if (user.User_Status == 0)
                return Results.Json(new { success = false, message = "User tidak aktif" }, statusCode: 403);

            if (user.Is_Verified == 0)
                return Results.Json(new { success = false, message = "User belum diverifikasi" }, statusCode: 403);

            if (!auth.VerifyPassword(req.Password, user.Pwd))
                return Results.Json(new { success = false, message = "Password salah" }, statusCode: 401);

            cache.Remove(failKey);
            cache.Remove(blockKey);

            var token = auth.CreateJwt(user);

            return Results.Ok(new
            {
                success = true,
                message = "Login berhasil",
                data = new
                {
                    token,
                    user = new
                    {
                        userid = user.Userid,
                        email = user.Email,
                        pin = user.Pin,
                        skpdid = user.Skpdid,
                        level = user.Level,
                        deviceid = user.Deviceid
                    }
                }
            });
        });

        return app;
    }

    static void RegisterFail(IMemoryCache cache, string failKey, string blockKey)
    {
        // window 10 menit, max 5 gagal
        var fails = cache.GetOrCreate(failKey, e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return 0;
        });

        fails++;
        cache.Set(failKey, fails, TimeSpan.FromMinutes(10));

        if (fails >= 5)
        {
            // blok 15 menit
            cache.Set(blockKey, true, TimeSpan.FromMinutes(15));
        }
    }
}
