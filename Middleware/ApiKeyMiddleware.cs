using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using entago_api_mysql.Services;

namespace entago_api_mysql.Middleware;

public sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext ctx,
        ApiClientService clients,
        IMemoryCache cache)
    {
        var ct = ctx.RequestAborted; // ✅ token dari request

        // Lindungi hanya /api/*
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues) ||
            string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Missing X-Api-Key" }, ct);
            return;
        }

        var apiKey = apiKeyValues.ToString().Trim();
        var client = await clients.FindActiveClientByKeyAsync(apiKey, ct);

        if (client is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Invalid API key" }, ct);
            return;
        }

        // (opsional) Origin check
        var origin = ctx.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && !IsOriginAllowed(origin, client.Allowed_Origins))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Origin not allowed" }, ct);
            return;
        }

        // (opsional) IP allowlist
        var remoteIp = ctx.Connection.RemoteIpAddress;
        if (remoteIp is not null && !IsIpAllowed(remoteIp, client.Allowed_Ips))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { success = false, message = "IP not allowed" }, ct);
            return;
        }

        // Rate limit khusus login (lebih ketat)
        if (ctx.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var loginKey = $"rl:login:{client.Id}:{ip}:{DateTime.UtcNow:yyyyMMddHHmm}";

            var loginCount = cache.GetOrCreate(loginKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });

            loginCount++;
            cache.Set(loginKey, loginCount, TimeSpan.FromMinutes(1));

            // contoh: 10 request/menit per IP per client untuk login
            if (loginCount > 10)
            {
                ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Too many login attempts" }, ct);
                return;
            }
        }

        // Rate limit per menit (in-memory)
        if (client.Rate_Limit > 0)
        {
            var windowKey = $"rl:{client.Id}:{DateTime.UtcNow:yyyyMMddHHmm}";
            var count = cache.GetOrCreate(windowKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });

            count++;
            cache.Set(windowKey, count, TimeSpan.FromMinutes(1));

            if (count > client.Rate_Limit)
            {
                ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.Response.WriteAsJsonAsync(new { success = false, message = "Rate limit exceeded" }, ct);
                return;
            }
        }

        ctx.Items["ApiClient"] = client;
        ctx.Items["ApiClientId"] = client.Id;

        ctx.Items["ApiClientPrefix"] = client.Prefix;
        ctx.Items["ApiClientRateLimit"] = client.Rate_Limit;

        await next(ctx);
    }

    private static bool IsOriginAllowed(string origin, string? allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(allowedOrigins)) return true;
        var allowed = ParseList(allowedOrigins);
        return allowed.Any(x => string.Equals(x, origin, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsIpAllowed(IPAddress ip, string? allowedIps)
    {
        if (string.IsNullOrWhiteSpace(allowedIps)) return true;
        var allowed = ParseList(allowedIps);
        foreach (var item in allowed)
            if (IsMatchIpOrCidr(ip, item)) return true;
        return false;
    }

    private static List<string> ParseList(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("["))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(raw);
                return (arr ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            }
            catch { }
        }
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static bool IsMatchIpOrCidr(IPAddress ip, string allowed)
    {
        allowed = allowed.Trim();
        if (IPAddress.TryParse(allowed, out var single))
            return ip.Equals(single);

        var parts = allowed.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var network) && int.TryParse(parts[1], out var prefix))
            return IsInCidr(ip, network, prefix);

        return false;
    }

    private static bool IsInCidr(IPAddress ip, IPAddress network, int prefixLength)
    {
        var ipBytes = ip.GetAddressBytes();
        var netBytes = network.GetAddressBytes();
        if (ipBytes.Length != netBytes.Length) return false;

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
            if (ipBytes[i] != netBytes[i]) return false;

        if (remainingBits == 0) return true;

        var mask = (byte)(~(0xFF >> remainingBits));
        return (ipBytes[fullBytes] & mask) == (netBytes[fullBytes] & mask);
    }
}
