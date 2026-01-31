using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using entago_api_mysql.Dtos;
using entago_api_mysql.Services;
using Microsoft.Extensions.Caching.Memory;

namespace entago_api_mysql.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");
        //var group = app.MapGroup("/auth");

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


        // PUT /api/auth/change-password
        // Header: Authorization: Bearer <token>
        _ = group.MapPut("/change-password", async (
            HttpContext ctx,
            ChangePasswordRequest req,
            AuthService auth,
            CancellationToken ct) =>
        {
            // ambil userid dari JWT (sub)
            var pin = ctx.User.FindFirstValue("pin");
            if (string.IsNullOrWhiteSpace(pin))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.OldPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
                return Results.BadRequest(new { success = false, message = "OldPassword dan NewPassword wajib diisi." });

            if (req.NewPassword.Length < 6)
                return Results.BadRequest(new { success = false, message = "Password baru minimal 6 karakter." });

            if (req.NewPassword == req.OldPassword)
                return Results.BadRequest(new { success = false, message = "Password baru tidak boleh sama dengan password lama." });

            // ambil user dari DB
            var user = await auth.FindActiveUserByPinAsync(pin, ct);

            if (user is null)
            {
                Console.WriteLine($"[ChangePassword] User tidak ditemukan. userId={pin}");
                return Results.Unauthorized();
            }

            //Console.WriteLine($"[ChangePassword] userId={user.Pin} email={user.Email} pin={user.Pin}");


            // verifikasi password lama
            if (!auth.VerifyPassword(req.OldPassword, user.Pwd))
                return Results.Json(new { success = false, message = "Password lama salah." }, statusCode: 401);

            // hash/encrypt password baru (legacy)
            var newPwd = auth.HashPasswordLikeLegacy(req.NewPassword);

            // update
            var affected = await auth.UpdatePasswordAsync(pin, newPwd, ct);
            if (affected <= 0)
                return Results.Problem("Gagal update password.", statusCode: 500);

            return Results.Ok(new { success = true, message = "Password berhasil diperbarui." });
        })
        .RequireAuthorization(); // ✅ wajib JWT

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
