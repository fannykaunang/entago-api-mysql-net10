using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using entago_api_mysql.Middleware;
using entago_api_mysql.Services;
using entago_api_mysql.Endpoints;
using Microsoft.AspNetCore.Http.Features;
using entago_api_mysql.Options;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// DI
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddScoped<ApiClientService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PegawaiService>();
builder.Services.AddScoped<CheckinService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<IzinListService>();
builder.Services.AddScoped<MonthlyReportService>();
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 5 * 1024 * 1024; // 5MB (sesuaikan)
});
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection("Uploads"));
builder.Services.AddScoped<TugasLuarService>();
builder.Services.AddScoped<FileUploadService>();

builder.Services.AddMemoryCache();

// JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key belum di-set");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseHttpsRedirection();
// app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        builder.Configuration["Uploads:TugasLuarPhysicalRoot"]!
    ),
    RequestPath = "/files/tugas-luar"
});

// API Key middleware dulu (untuk semua /api/* termasuk /api/auth/login)
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Auth endpoints (tidak butuh JWT)
app.MapAuthEndpoints();

// Protected endpoints (butuh JWT)
app.MapProtectedApiEndpoints();

app.Run();
