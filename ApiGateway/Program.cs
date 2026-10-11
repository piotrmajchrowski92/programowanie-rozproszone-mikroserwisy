using System.Net;
using Shared;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJwtSecurity(builder.Configuration);
builder.Services.AddHttpClient("proxy", client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None });
var app = builder.Build();
app.UseApiErrors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { service = "ApiGateway", status = "ok" }));

app.MapPost("/api/auth/register", (HttpContext ctx, IHttpClientFactory clients, IConfiguration config) =>
    Proxy(ctx, clients, config.Required("Services:AuthService"), "/api/auth/register"));
app.MapPost("/api/auth/login", (HttpContext ctx, IHttpClientFactory clients, IConfiguration config) =>
    Proxy(ctx, clients, config.Required("Services:AuthService"), "/api/auth/login"));
app.MapGet("/api/auth/validate", (HttpContext ctx, IHttpClientFactory clients, IConfiguration config) =>
    Proxy(ctx, clients, config.Required("Services:AuthService"), "/api/auth/validate"))
    .RequireAuthorization();
app.MapMethods("/api/tickets/{**path}", ["GET", "POST", "PUT", "DELETE"],
    (HttpContext ctx, IHttpClientFactory clients, IConfiguration config) =>
        Proxy(ctx, clients, config.Required("Services:TicketService"), ctx.Request.Path.Value!))
    .RequireAuthorization();
app.MapGet("/api/logs", (HttpContext ctx, IHttpClientFactory clients, IConfiguration config) =>
    Proxy(ctx, clients, config.Required("Services:LogService"), "/api/logs"))
    .RequireAuthorization();
app.MapPost("/internal/logs", async (HttpContext ctx, IHttpClientFactory clients, IConfiguration config) =>
{
    if (!ctx.Request.HasInternalKey(config))
    {
        ctx.Response.StatusCode = 401;
        return;
    }
    await Proxy(ctx, clients, config.Required("Services:LogService"), "/api/logs",
        config.Required("InternalKey"));
});
app.Run();

static async Task Proxy(HttpContext context, IHttpClientFactory clients, string serviceUrl,
    string path, string? internalKey = null)
{
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method),
        serviceUrl.TrimEnd('/') + path + context.Request.QueryString);
    if (context.Request.Method is "POST" or "PUT" or "PATCH")
    {
        request.Content = new StreamContent(context.Request.Body);
        if (!string.IsNullOrEmpty(context.Request.ContentType))
            request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
    }
    if (context.Request.Headers.TryGetValue("Authorization", out var auth))
        request.Headers.TryAddWithoutValidation("Authorization", auth.ToString());
    // Nagłówek wewnętrzny nigdy nie jest kopiowany z publicznego żądania.
    if (internalKey is not null) request.Headers.Add("X-Internal-Key", internalKey);
    try
    {
        using var response = await clients.CreateClient("proxy").SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        if (response.Content.Headers.ContentType is not null)
            context.Response.ContentType = response.Content.Headers.ContentType.ToString();
        if (response.Headers.Location is not null)
            context.Response.Headers["Location"] = response.Headers.Location.ToString();
        if (response.Headers.WwwAuthenticate.Count > 0)
            context.Response.Headers["WWW-Authenticate"] = string.Join(", ", response.Headers.WwwAuthenticate);
        if (response.StatusCode != HttpStatusCode.NoContent)
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        if (context.Response.HasStarted) { context.Abort(); return; }
        await Results.Problem(statusCode: 502, title: "Mikroserwis jest niedostępny.",
            detail: "Uruchom wszystkie projekty i sprawdź konfigurację portów.").ExecuteAsync(context);
    }
}
