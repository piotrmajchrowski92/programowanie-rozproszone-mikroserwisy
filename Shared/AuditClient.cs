using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace Shared;

public sealed class AuditClient(HttpClient http, IConfiguration config, ILogger<AuditClient> logger)
{
    public async Task WriteAsync(string userId, string service, string action, string details)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                config.Required("GatewayUrl").TrimEnd('/') + "/internal/logs");
            request.Headers.Add("X-Internal-Key", config.Required("InternalKey"));
            request.Content = JsonContent.Create(new AuditEvent(userId, service, action, details));
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Nie zapisano logu {Action} dla {UserId}. Operacja biznesowa nie jest wycofywana.",
                action, userId);
        }
    }
}
