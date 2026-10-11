using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared;
namespace WebClient.Pages;

public sealed class IndexModel(GatewayClient gateway) : PageModel
{
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public TicketInput Input { get; set; } = new();
    [BindProperty] public int TicketId { get; set; }
    public List<Ticket> Tickets { get; set; } = [];
    public List<AuditLog> Logs { get; set; } = [];
    public string? CurrentEmail => HttpContext.Session.GetString("Email");
    public string? Token => HttpContext.Session.GetString("Token");
    public string? Expires => HttpContext.Session.GetString("Expires");
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);
    [TempData] public string? Message { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(int? edit)
    {
        if (!IsLoggedIn) return;
        if (edit.HasValue)
        {
            var response = await gateway.SendAsync(HttpMethod.Get, $"/api/tickets/{edit}", token: Token);
            if (Accept(response) && response.Read<Ticket>() is { } ticket)
            {
                TicketId = ticket.Id;
                Input = new TicketInput { Title = ticket.Title, Description = ticket.Description,
                    Status = ticket.Status };
            }
        }
        await LoadAsync();
    }
    public async Task<IActionResult> OnPostRegisterAsync()
    {
        var response = await gateway.SendAsync(HttpMethod.Post, "/api/auth/register",
            new Credentials { Email = Email, Password = Password });
        if (Accept(response)) Message = "Konto zostało utworzone. Teraz zaloguj się.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostLoginAsync()
    {
        var response = await gateway.SendAsync(HttpMethod.Post, "/api/auth/login",
            new Credentials { Email = Email, Password = Password });
        if (Accept(response) && response.Read<AuthResponse>() is { } auth)
        {
            HttpContext.Session.SetString("Token", auth.Token);
            HttpContext.Session.SetString("Email", auth.Email);
            HttpContext.Session.SetString("Expires", auth.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            Message = "Zalogowano pomyślnie.";
        }
        return RedirectToPage();
    }
    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        Message = "Wylogowano. Token usunięto z sesji klienta.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!IsLoggedIn) { ErrorMessage = "Najpierw się zaloguj."; return RedirectToPage(); }
        var response = await gateway.SendAsync(TicketId == 0 ? HttpMethod.Post : HttpMethod.Put,
            TicketId == 0 ? "/api/tickets" : $"/api/tickets/{TicketId}", Input, Token);
        if (Accept(response))
        {
            Message = TicketId == 0 ? "Dodano zgłoszenie." : "Zapisano zmiany zgłoszenia.";
            return RedirectToPage();
        }
        await LoadAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!IsLoggedIn) { ErrorMessage = "Najpierw się zaloguj."; return RedirectToPage(); }
        var response = await gateway.SendAsync(HttpMethod.Delete, $"/api/tickets/{id}", token: Token);
        if (Accept(response)) Message = $"Usunięto zgłoszenie #{id}.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostValidateAsync()
    {
        if (!IsLoggedIn) { ErrorMessage = "Najpierw się zaloguj."; return RedirectToPage(); }
        var response = await gateway.SendAsync(HttpMethod.Get, "/api/auth/validate", token: Token);
        if (Accept(response)) Message = "JWT jest poprawny. Gateway i AuthService zweryfikowały token.";
        return RedirectToPage();
    }
    private bool Accept(ApiResponse response)
    {
        if (response.Success) return true;
        ErrorMessage = response.Error;
        if (response.StatusCode == 401) HttpContext.Session.Clear();
        return false;
    }
    private async Task LoadAsync()
    {
        if (!IsLoggedIn) return;
        var tickets = await gateway.SendAsync(HttpMethod.Get, "/api/tickets", token: Token);
        if (Accept(tickets)) Tickets = tickets.Read<List<Ticket>>() ?? [];
        if (!IsLoggedIn) return;
        var logs = await gateway.SendAsync(HttpMethod.Get, "/api/logs?take=100", token: Token);
        if (Accept(logs)) Logs = logs.Read<List<AuditLog>>() ?? [];
    }
}
