using System.Security.Claims;
using Shared;
using TicketService;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJwtSecurity(builder.Configuration);
builder.Services.AddSingleton(new SqliteDatabase(builder.Environment.ContentRootPath,
    builder.Configuration.Required("DatabaseFile")));
builder.Services.AddSingleton<TicketRepository>();
builder.Services.AddHttpClient<AuditClient>(client => client.Timeout = TimeSpan.FromSeconds(3));
var app = builder.Build();
app.Services.GetRequiredService<TicketRepository>().Initialize();
app.UseApiErrors();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { service = "TicketService", status = "ok" }));
var group = app.MapGroup("/api/tickets").RequireAuthorization();
group.MapGet("", async (ClaimsPrincipal user, TicketRepository tickets, AuditClient audit) =>
{
    var data = tickets.All(user.UserId());
    await audit.WriteAsync(user.UserId(), "TicketService", "READ_LIST", $"Odczyt listy: {data.Count} zgłoszeń.");
    return Results.Ok(data);
});
group.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, TicketRepository tickets, AuditClient audit) =>
{
    var ticket = tickets.Find(id, user.UserId());
    if (ticket is null) return Results.NotFound(new { message = "Nie znaleziono zgłoszenia." });
    await audit.WriteAsync(user.UserId(), "TicketService", "READ", $"Odczyt zgłoszenia #{id}.");
    return Results.Ok(ticket);
});
group.MapPost("", async (TicketInput input, ClaimsPrincipal user, TicketRepository tickets, AuditClient audit) =>
{
    input.Title = (input.Title ?? "").Trim();
    var errors = InputValidation.Errors(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var ticket = tickets.Add(user.UserId(), input);
    await audit.WriteAsync(user.UserId(), "TicketService", "CREATE", $"Dodano zgłoszenie #{ticket.Id}.");
    return Results.Created($"/api/tickets/{ticket.Id}", ticket);
});
group.MapPut("/{id:int}", async (int id, TicketInput input, ClaimsPrincipal user,
    TicketRepository tickets, AuditClient audit) =>
{
    input.Title = (input.Title ?? "").Trim();
    var errors = InputValidation.Errors(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var ticket = tickets.Update(id, user.UserId(), input);
    if (ticket is null) return Results.NotFound(new { message = "Nie znaleziono zgłoszenia." });
    await audit.WriteAsync(user.UserId(), "TicketService", "UPDATE", $"Zmieniono zgłoszenie #{id}.");
    return Results.Ok(ticket);
});
group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, TicketRepository tickets, AuditClient audit) =>
{
    if (!tickets.Delete(id, user.UserId()))
        return Results.NotFound(new { message = "Nie znaleziono zgłoszenia." });
    await audit.WriteAsync(user.UserId(), "TicketService", "DELETE", $"Usunięto zgłoszenie #{id}.");
    return Results.NoContent();
});
app.Run();
