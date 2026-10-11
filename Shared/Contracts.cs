using System.ComponentModel.DataAnnotations;
namespace Shared;

public sealed class Credentials
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = "";
}
public sealed record AuthResponse(string Token, string Email, string UserId, DateTime ExpiresAtUtc);
public sealed record UserInfo(string UserId, string Email);
public sealed class TicketInput
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = "";
    [StringLength(2000)]
    public string Description { get; set; } = "";
    [Required]
    public string Status { get; set; } = "Nowe";
}
public sealed record Ticket(int Id, string OwnerId, string Title, string Description,
    string Status, string CreatedAtUtc, string UpdatedAtUtc);
public sealed record AuditEvent(string UserId, string Service, string Action, string Details);
public sealed record AuditLog(long Id, string TimestampUtc, string UserId, string Service,
    string Action, string Details);
public static class InputValidation
{
    public static Dictionary<string, string[]> Errors(object input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, true);
        var errors = results.SelectMany(r => r.MemberNames.DefaultIfEmpty("input")
            .Select(n => (Name: n, Message: r.ErrorMessage ?? "Nieprawidłowe dane.")))
            .GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());
        if (input is TicketInput ticket && ticket.Status is not ("Nowe" or "W toku" or "Zamknięte"))
            errors["Status"] = ["Dozwolone statusy: Nowe, W toku, Zamknięte."];
        return errors;
    }
}
