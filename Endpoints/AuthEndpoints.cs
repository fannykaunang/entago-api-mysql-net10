using entago_api_mysql.Services;

namespace entago_api_mysql.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        // POST /api/auth/login
        _ = group.MapPost("/login", async (LoginRequest req, AuthService auth, CancellationToken ct) =>
        {
            var user = await auth.FindActiveUserByEmailAsync(req.Email, ct);
            if (user is null)
                return Results.Json(new { success = false, message = "Email / user tidak ditemukan atau tidak aktif" }, statusCode: 401);

            if (user.User_Status == 0)
                return Results.Json(new { success = false, message = "User tidak aktif" }, statusCode: 403);

            if (user.Is_Verified == 0)
                return Results.Json(new { success = false, message = "User belum diverifikasi" }, statusCode: 403);

            if (!auth.VerifyPassword(req.Password, user.Pwd))
                return Results.Json(new { success = false, message = "Password salah" }, statusCode: 401);

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
}
