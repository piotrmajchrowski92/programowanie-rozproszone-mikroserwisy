using WebClient;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.Name = "MicroservicesAssignment.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddHttpClient<GatewayClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GatewayUrl"]
        ?? throw new InvalidOperationException("Brak GatewayUrl."));
    client.Timeout = TimeSpan.FromSeconds(20);
});
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { service = "WebClient", status = "ok" }));
app.Run();
