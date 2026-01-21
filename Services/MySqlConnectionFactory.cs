using MySqlConnector;

namespace entago_api_mysql.Services;

public sealed class MySqlConnectionFactory(IConfiguration config)
{
    private readonly string _cs =
        config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default belum di-set di appsettings.json");

    public MySqlConnection Create() => new MySqlConnection(_cs);
}
