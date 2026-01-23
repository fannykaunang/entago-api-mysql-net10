using Dapper;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace entago_api_mysql.Services;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserRow(
    int Userid,
    int Skpdid,
    string Pin,
    string Email,
    string Pwd,
    int Level,
    sbyte User_Status,
    sbyte Is_Verified,
    string? Deviceid
);

public sealed class AuthService(MySqlConnectionFactory factory, IConfiguration config)
{
    private const string EncryptionKey = "MAKV2SPBNI99212";
    private static readonly byte[] SaltBytes = new byte[]
    {
        0x49, 0x76, 0x61, 0x6E, 0x20, 0x4D, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76
    };

    public async Task<UserRow?> FindActiveUserByEmailAsync(string email, CancellationToken ct = default)
    {
        const string sql = @"
                SELECT
                userid AS Userid,
                skpdid AS Skpdid,
                pin AS Pin,
                email AS Email,
                pwd AS Pwd,
                level AS Level,
                user_status AS User_Status,
                is_verified AS Is_Verified,
                deviceid AS Deviceid
                FROM e_user
                WHERE email = @email
                LIMIT 1;";

        await using var conn = factory.Create();
        await conn.OpenAsync(ct);

        var user = await conn.QueryFirstOrDefaultAsync<UserRow>(
            new CommandDefinition(sql, new { email }, cancellationToken: ct)
        );

        // status check minimal
        if (user is null) return null;
        if (user.User_Status != 1) return null;
        // kalau kamu mau wajib verified:
        if (user.Is_Verified != 1) return null;

        return user;
    }

    public bool VerifyPassword(string inputPassword, string storedPwdBase64)
    {
        var enc = EncryptLikeLegacy(inputPassword);

        // Compare aman (optional). Minimal: string equals.
        return string.Equals(enc, storedPwdBase64, StringComparison.Ordinal);
    }

    private static string EncryptLikeLegacy(string clearText)
    {
        // VB: Encoding.Unicode.GetBytes(clearText)
        var clearBytes = Encoding.Unicode.GetBytes(clearText);

        using var aes = Aes.Create();
        // VB default: PBKDF2 SHA1, iterations default (1000) -> di .NET sama kalau pakai constructor ini
        using var pdb = new Rfc2898DeriveBytes(EncryptionKey, SaltBytes);

        aes.Key = pdb.GetBytes(32);
        aes.IV = pdb.GetBytes(16);

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(clearBytes, 0, clearBytes.Length);
            cs.FlushFinalBlock();
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string CreateJwt(UserRow user)
    {
        var issuer = config["Jwt:Issuer"]!;
        var audience = config["Jwt:Audience"]!;
        var key = config["Jwt:Key"]!;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Userid.ToString()),
            new("pin", user.Pin),
            new("skpdid", user.Skpdid.ToString()),
            new("level", user.Level.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(6),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
