using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
namespace Shared;

public static class Infrastructure
{
    public static string Required(this IConfiguration config, string key) =>
        config[key] ?? throw new InvalidOperationException($"Brak konfiguracji: {key}");

    public static TokenValidationParameters ValidationParameters(IConfiguration config)
    {
        var key = config.Required("Jwt:Key");
        if (Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Jwt:Key musi mieć co najmniej 32 bajty.");
        return new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = config.Required("Jwt:Issuer"),
            ValidateAudience = true, ValidAudience = config.Required("Jwt:Audience"),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(10),
            NameClaimType = JwtRegisteredClaimNames.Email
        };
    }
    public static void AddJwtSecurity(this IServiceCollection services, IConfiguration config)
    {
        var validation = ValidationParameters(config);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = validation;
        });
        services.AddAuthorization();
    }
    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "";
    public static bool HasInternalKey(this HttpRequest request, IConfiguration config)
    {
        var expected = config.Required("InternalKey");
        if (expected.Length < 32) throw new InvalidOperationException("InternalKey jest za krótki.");
        var supplied = request.Headers["X-Internal-Key"].ToString();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
    public static void UseApiErrors(this WebApplication app)
    {
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = 500;
            await Results.Problem(statusCode: 500, title: "Błąd serwera.",
                detail: "Sprawdź konsolę odpowiedniego mikroserwisu.").ExecuteAsync(context);
        }));
    }
    public static string UtcNow() => DateTime.UtcNow.ToString("O");
}
public sealed class SqliteDatabase
{
    private readonly string connectionString;
    public SqliteDatabase(string contentRoot, string filename)
    {
        var dir = Path.Combine(contentRoot, "Data");
        Directory.CreateDirectory(dir);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dir, filename), DefaultTimeout = 10
        }.ToString();
    }
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
    public void Initialize(string schema)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;" + schema;
        command.ExecuteNonQuery();
    }
}
