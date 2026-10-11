using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJwtSecurity(builder.Configuration);
builder.Services.AddSingleton(new SqliteDatabase(builder.Environment.ContentRootPath,
    builder.Configuration.Required("DatabaseFile")));
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
builder.Services.AddHttpClient<AuditClient>(client => client.Timeout = TimeSpan.FromSeconds(3));
var app = builder.Build();
app.Services.GetRequiredService<UserRepository>().Initialize();
app.UseApiErrors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { service = "AuthService", status = "ok" }));

app.MapPost("/api/auth/register", async (Credentials input, UserRepository users,
    IPasswordHasher<ApplicationUser> hasher, AuditClient audit) =>
{
    input.Email = (input.Email ?? "").Trim().ToLowerInvariant();
    var errors = InputValidation.Errors(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), Email = input.Email };
    user.PasswordHash = hasher.HashPassword(user, input.Password);
    try { users.Add(user); }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
    { return Results.Conflict(new { message = "Użytkownik o tym adresie e-mail już istnieje." }); }
    await audit.WriteAsync(user.Id, "AuthService", "REGISTER", "Zarejestrowano konto.");
    return Results.Json(new UserInfo(user.Id, user.Email), statusCode: 201);
});

app.MapPost("/api/auth/login", async (Credentials input, UserRepository users,
    IPasswordHasher<ApplicationUser> hasher, AuditClient audit, IConfiguration config) =>
{
    input.Email = (input.Email ?? "").Trim().ToLowerInvariant();
    var errors = InputValidation.Errors(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var user = users.FindByEmail(input.Email);
    var verification = user is null ? PasswordVerificationResult.Failed
        : hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password);
    if (user is null || verification == PasswordVerificationResult.Failed)
    {
        await audit.WriteAsync(user?.Id ?? "", "AuthService", "LOGIN_FAILED", "Nieudane logowanie.");
        return Results.Json(new { message = "Nieprawidłowy e-mail lub hasło." }, statusCode: 401);
    }
    if (verification == PasswordVerificationResult.SuccessRehashNeeded)
    {
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        users.UpdateHash(user);
    }
    var expires = DateTime.UtcNow.AddMinutes(config.GetValue<int>("Jwt:LifetimeMinutes"));
    var token = new JwtSecurityToken(
        issuer: config.Required("Jwt:Issuer"), audience: config.Required("Jwt:Audience"),
        claims: [new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
        notBefore: DateTime.UtcNow, expires: expires,
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Required("Jwt:Key"))),
            SecurityAlgorithms.HmacSha256));
    await audit.WriteAsync(user.Id, "AuthService", "LOGIN", "Poprawne logowanie; wydano JWT.");
    return Results.Ok(new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token),
        user.Email, user.Id, expires));
});

app.MapGet("/api/auth/validate", (ClaimsPrincipal user) =>
    Results.Ok(new { valid = true, userId = user.UserId(), email = user.FindFirstValue("email") }))
    .RequireAuthorization();
app.Run();
