using System.Security.Claims;
using LogService;
using Shared;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJwtSecurity(builder.Configuration);
builder.Services.AddSingleton(new SqliteDatabase(builder.Environment.ContentRootPath,
    builder.Configuration.Required("DatabaseFile")));
builder.Services.AddSingleton<LogRepository>();
var app = builder.Build();
app.Services.GetRequiredService<LogRepository>().Initialize();
app.UseApiErrors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { service = "LogService", status = "ok" }));
app.MapPost("/api/logs", (AuditEvent input, HttpRequest request, IConfiguration config, LogRepository logs) =>
{
    if (!request.HasInternalKey(config)) return Results.Unauthorized();
    if (input.UserId is null || input.UserId.Length > 100 || string.IsNullOrWhiteSpace(input.Service)
        || input.Service.Length > 100 || string.IsNullOrWhiteSpace(input.Action) || input.Action.Length > 100
        || input.Details is null || input.Details.Length > 2000)
        return Results.BadRequest(new { message = "Nieprawidłowe dane logu." });
    return Results.Json(new { id = logs.Add(input) }, statusCode: 201);
});
app.MapGet("/api/logs", (ClaimsPrincipal user, LogRepository logs, int? take) =>
    Results.Ok(logs.List(user.UserId(), take ?? 100))).RequireAuthorization();
app.Run();
