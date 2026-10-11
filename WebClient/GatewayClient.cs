using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace WebClient;
public sealed record ApiResponse(bool Success, int StatusCode, string Body, string Error)
{
    public T? Read<T>() => JsonSerializer.Deserialize<T>(Body,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
public sealed class GatewayClient(HttpClient http)
{
    public async Task<ApiResponse> SendAsync(HttpMethod method, string path, object? body = null,
        string? token = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return new ApiResponse(response.IsSuccessStatusCode, (int)response.StatusCode,
                content, response.IsSuccessStatusCode ? "" : Error(content, (int)response.StatusCode));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ApiResponse(false, 0, "", "Brak połączenia z API Gateway. Uruchom wszystkie projekty.");
        }
    }
    private static string Error(string body, int status)
    {
        if (string.IsNullOrWhiteSpace(body))
            return status == 401 ? "Brak autoryzacji lub wygasły token. Zaloguj się ponownie."
                : $"Błąd HTTP {status}.";
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var message)) return message.GetString() ?? body;
            if (root.TryGetProperty("errors", out var errors))
                return string.Join(" ", errors.EnumerateObject().SelectMany(p =>
                    p.Value.EnumerateArray().Select(v => v.GetString())));
            if (root.TryGetProperty("detail", out var detail)) return detail.GetString() ?? body;
            if (root.TryGetProperty("title", out var title)) return title.GetString() ?? body;
        }
        catch (JsonException) { }
        return $"Błąd HTTP {status}.";
    }
}
