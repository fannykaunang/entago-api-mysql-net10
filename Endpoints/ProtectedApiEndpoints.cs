namespace entago_api_mysql.Endpoints;

public static class ProtectedApiEndpoints
{
    public static IEndpointRouteBuilder MapProtectedApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api")
            .RequireAuthorization(); // JWT untuk semua /api/* (kecuali /api/auth/* karena beda group)

        api.MapPegawaiEndpoints();
        api.MapCheckinEndpoints();
        api.MapCheckoutEndpoints();
        api.MapIzinListEndpoints();

        return app;
    }
}
