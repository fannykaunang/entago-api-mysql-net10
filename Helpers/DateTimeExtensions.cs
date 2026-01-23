using System.Globalization;

namespace entago_api_mysql.Helpers;

public static class DateTimeExtensions
{
    private static readonly CultureInfo Id = new("id-ID");

    public static string? ToIndoText(this DateTime? dt, string format = "dddd, dd MMM yyyy HH:mm")
    {
        if (dt is null) return null;

        var s = dt.Value.ToString(format, Id);

        // optional: kapital huruf pertama ("senin" -> "Senin")
        return char.ToUpper(s[0], Id) + s[1..];
    }
}
