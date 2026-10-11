using System.Net;
using System.Text;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:5200";
var directory = new DirectoryInfo(Environment.CurrentDirectory);
while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MicroservicesAssignment.sln")))
    directory = directory.Parent;
if (directory is null) throw new InvalidOperationException("Uruchom narzędzie wewnątrz katalogu rozwiązania.");
var root = directory.FullName;
var screenshotsDir = Path.Combine(root, "Screenshots");
var docsDir = Path.Combine(root, "Documentation");
Directory.CreateDirectory(screenshotsDir);
Directory.CreateDirectory(docsDir);
var captured = new List<(string Title, string Description, string Image)>();

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
{
    ViewportSize = new ViewportSize { Width = 1280, Height = 900 }, Locale = "pl-PL"
});
var page = await context.NewPageAsync();
page.SetDefaultTimeout(30000);
try
{
    await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    await Capture("01-start", "Ekran początkowy", "Klient udostępnia formularze rejestracji i logowania. Bez logowania panel CRUD nie jest wyświetlany.");
    var email = $"demo-{Guid.NewGuid():N}@example.com";
    const string password = "DemoPass123!";
    await page.Locator("#registerEmail").FillAsync(email);
    await page.Locator("#registerPassword").FillAsync(password);
    await page.GetByRole(AriaRole.Button, new() { Name = "Utwórz konto", Exact = true }).ClickAsync();
    await Expect(page.GetByText("Konto zostało utworzone. Teraz zaloguj się.", new() { Exact = true })).ToBeVisibleAsync();
    await Capture("02-register", "Rejestracja użytkownika", "AuthService zapisuje użytkownika i skrót hasła we własnej bazie. Działanie REGISTER trafia do LogService przez Gateway.");
    await page.Locator("#loginEmail").FillAsync(email);
    await page.Locator("#loginPassword").FillAsync(password);
    await page.GetByRole(AriaRole.Button, new() { Name = "Zaloguj się", Exact = true }).ClickAsync();
    await Expect(page.GetByText("Zalogowano pomyślnie.", new() { Exact = true })).ToBeVisibleAsync();
    await Capture("03-login", "Logowanie i sesja", "AuthService wydaje JWT. WebClient przechowuje token w sesji po stronie serwera i dodaje nagłówek Bearer do kolejnych wywołań API.");
    await page.Locator("#Input_Title").FillAsync("Brak dostępu do raportu Power BI");
    await page.Locator("#Input_Description").FillAsync("Proszę sprawdzić uprawnienia do raportu jakości danych.");
    await page.Locator("#Input_Status").SelectOptionAsync("Nowe");
    await page.GetByRole(AriaRole.Button, new() { Name = "Dodaj", Exact = true }).ClickAsync();
    await Expect(page.GetByText("Dodano zgłoszenie.", new() { Exact = true })).ToBeVisibleAsync();
    await Capture("04-create", "Dodanie i odczyt zgłoszenia", "POST /api/tickets dodaje rekord, a GET /api/tickets odświeża listę. Właściciel pochodzi z podpisanego JWT, nie z danych formularza.");
    await page.GetByRole(AriaRole.Link, new() { Name = "Edytuj", Exact = true }).ClickAsync();
    await page.Locator("#Input_Title").FillAsync("Dostęp do raportu Power BI - weryfikacja");
    await page.Locator("#Input_Status").SelectOptionAsync("W toku");
    await page.GetByRole(AriaRole.Button, new() { Name = "Zapisz zmiany", Exact = true }).ClickAsync();
    await Expect(page.GetByText("Zapisano zmiany zgłoszenia.", new() { Exact = true })).ToBeVisibleAsync();
    await Capture("05-update", "Aktualizacja zgłoszenia", "PUT /api/tickets/{id} zmienia tytuł i status. UPDATE jest zapisywane w dzienniku zdarzeń.");
    await page.GetByRole(AriaRole.Button, new() { Name = "Sprawdź JWT", Exact = true }).ClickAsync();
    await Expect(page.GetByText("JWT jest poprawny. Gateway i AuthService zweryfikowały token.", new() { Exact = true })).ToBeVisibleAsync();
    await Capture("06-jwt", "Weryfikacja JWT", "GET /api/auth/validate wymaga poprawnego podpisu, wystawcy, odbiorcy i czasu ważności tokenu. Weryfikacja działa również przy każdym autoryzowanym CRUD.");
    await page.GetByRole(AriaRole.Button, new() { Name = "Usuń", Exact = true }).ClickAsync();
    await Expect(page.GetByText("Brak zgłoszeń. Dodaj pierwszy rekord.", new() { Exact = true })).ToBeVisibleAsync();
    await Expect(page.GetByText("DELETE", new() { Exact = true })).ToBeVisibleAsync();
    await Capture("07-delete", "Usunięcie i podgląd logów", "DELETE /api/tickets/{id} usuwa rekord. Dziennik nadal zawiera REGISTER, LOGIN, CREATE, READ, READ_LIST, UPDATE i DELETE.");
    await page.GetByRole(AriaRole.Button, new() { Name = "Wyloguj", Exact = true }).ClickAsync();
    await Expect(page.GetByText("Wylogowano. Token usunięto z sesji klienta.", new() { Exact = true })).ToBeVisibleAsync();
    await Capture("08-logout", "Wylogowanie", "Usunięto token z sesji klienta. Nie zastosowano listy unieważnionych JWT; wydany wcześniej token wygasa zgodnie z exp.");

    var html = new StringBuilder("""
        <!doctype html><html lang="pl"><head><meta charset="utf-8"><style>
        @page { size:A4; margin:16mm; }
        body { font-family:Arial,sans-serif; color:#17243b; font-size:11pt; line-height:1.45; }
        h1 { font-size:25pt; } h2 { font-size:17pt; } h3 { font-size:13pt; }
        .page { break-before:page; } img { width:100%; max-height:235mm; object-fit:contain; object-position:top; border:1px solid #dbe2ee; }
        table { width:100%; border-collapse:collapse; font-size:10pt; } td,th { border:1px solid #ccc; padding:8px; text-align:left; }
        code,pre { background:#f3f5f9; font-size:10pt; } pre { padding:14px; white-space:pre-wrap; }
        .note { background:#e9f1fb; padding:12px; }
        </style></head><body><h1>System zgłoszeń w architekturze mikroserwisowej</h1>
        <p>C# / ASP.NET Core 8 / Visual Studio / JWT / SQLite / Razor Pages</p>
        <h2>1. Cel i zakres</h2>
        <p>Realizacja zadania: trzy mikroserwisy, centralny API Gateway i klient webowy. Każdy użytkownik może rejestrować konto, logować się, wykonywać CRUD swoich zgłoszeń i przeglądać własne logi.</p>
        <h2>2. Architektura</h2>
        <pre>Przeglądarka → WebClient :5200 → ApiGateway :5100
                                      ├→ AuthService :5101 → auth.db
                                      ├→ TicketService :5102 → tickets.db
                                      └→ LogService :5103 → logs.db
        AuthService/TicketService → Gateway /internal/logs → LogService</pre>
        <p>Shared jest biblioteką kontraktów i wspólnej infrastruktury, a nie dodatkowym mikroserwisem. Serwisy nie odczytują baz pozostałych serwisów.</p>
        <h2>3. Składniki</h2>
        <table><tr><th>Projekt</th><th>Odpowiedzialność</th></tr>
        <tr><td>AuthService</td><td>Rejestracja, logowanie, skróty haseł PasswordHasher, wydawanie i weryfikacja JWT.</td></tr>
        <tr><td>TicketService</td><td>Pełny CRUD: tworzenie, lista i pojedynczy odczyt, aktualizacja, usunięcie.</td></tr>
        <tr><td>LogService</td><td>Trwały zapis działań i odczyt własnych logów użytkownika.</td></tr>
        <tr><td>ApiGateway</td><td>Routing HTTP, weryfikacja JWT, chroniona ścieżka zapisu logów.</td></tr>
        <tr><td>WebClient</td><td>Interfejs Razor Pages, obsługa formularzy i wywołania Gateway w C#.</td></tr></table>
        <div class="page"><h2>4. Uruchomienie w Visual Studio</h2>
        <ol><li>Zainstaluj Visual Studio 2022 z aktualizacjami, komponent ASP.NET i tworzenie aplikacji internetowych oraz SDK .NET 8.</li>
        <li>Otwórz MicroservicesAssignment.sln i przywróć pakiety NuGet.</li>
        <li>W konfiguracji wielu projektów startowych ustaw Start: LogService, AuthService, TicketService, ApiGateway i WebClient. Shared nie jest uruchamiany.</li>
        <li>Uruchom F5 i otwórz http://127.0.0.1:5200.</li></ol>
        <p>Bazy SQLite tworzą się automatycznie w katalogu Data każdego mikroserwisu. Nie jest potrzebny SQL Server.</p>
        <h2>5. Publiczne API Gateway</h2>
        <table><tr><th>Metoda i adres</th><th>Autoryzacja / rezultat</th></tr>
        <tr><td>POST /api/auth/register</td><td>Publiczne; 201 lub 400/409.</td></tr>
        <tr><td>POST /api/auth/login</td><td>Publiczne; 200 z JWT lub 400/401.</td></tr>
        <tr><td>GET /api/auth/validate</td><td>Bearer JWT; 200 lub 401.</td></tr>
        <tr><td>GET /api/tickets; GET /api/tickets/{id}</td><td>Bearer JWT; własne dane, 200 lub 404.</td></tr>
        <tr><td>POST /api/tickets</td><td>Bearer JWT; 201.</td></tr>
        <tr><td>PUT /api/tickets/{id}</td><td>Bearer JWT; 200 lub 404.</td></tr>
        <tr><td>DELETE /api/tickets/{id}</td><td>Bearer JWT; 204 lub 404.</td></tr>
        <tr><td>GET /api/logs?take=100</td><td>Bearer JWT; do 200 własnych wpisów.</td></tr></table>
        <h2>6. Bezpieczeństwo i ograniczenia</h2>
        <ul><li>Hasła nie są przechowywane jawnie. JWT zawiera sub, email, jti oraz czas ważności.</li>
        <li>Gateway i mikroserwisy niezależnie sprawdzają podpis, wystawcę, odbiorcę i ważność JWT. Właściciel zgłoszeń pochodzi z sub.</li>
        <li>SQL używa parametrów. Razor koduje wyświetlany tekst. Formularze Razor Pages mają ochronę anty-CSRF.</li>
        <li>Klucze w appsettings.json są wyłącznie demonstracyjne. HTTP i nasłuch na loopback służą testom lokalnym.</li>
        <li>Zapisy logów są best-effort; awaria LogService nie cofa CRUD. Nie ma outbox, refresh tokenów, blacklisty JWT ani ról administratora.</li></ul>
        <h2>7. Weryfikacja</h2>
        <p>scripts/Smoke-Test.ps1 sprawdza API, pełny CRUD, błędne hasło, duplikat e-mail, brak lub uszkodzenie JWT, walidację, izolację danych i komplet logów. Wynik tego skryptu należy oceniać niezależnie od automatycznej demonstracji UI.</p>
        <p class="note">Poniższe obrazy zostały wykonane automatycznie z rzeczywiście uruchomionej aplikacji. Ten dokument powstaje dopiero po przejściu scenariusza demonstracyjnego UI.</p>
        """);
    html.Append("<p>Raport wygenerowano: ").Append(WebUtility.HtmlEncode(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))).Append("</p>");
    html.Append("<p>Konto demonstracyjne: ").Append(WebUtility.HtmlEncode(email)).Append("</p>");
    for (var i = 0; i < captured.Count; i++)
    {
        var item = captured[i];
        html.Append("<section class='page'><h2>8.").Append(i + 1).Append(". ")
            .Append(WebUtility.HtmlEncode(item.Title)).Append("</h2><p>")
            .Append(WebUtility.HtmlEncode(item.Description)).Append("</p><img src='data:image/png;base64,")
            .Append(item.Image).Append("'></section>");
    }
    html.Append("</body></html>");
    var htmlPath = Path.Combine(docsDir, "Raport_z_dzialania.html");
    await File.WriteAllTextAsync(htmlPath, html.ToString(), Encoding.UTF8);
    var reportPage = await context.NewPageAsync();
    await reportPage.SetContentAsync(html.ToString(), new PageSetContentOptions { WaitUntil = WaitUntilState.Load });
    var pdfPath = Path.Combine(docsDir, "Raport_z_dzialania.pdf");
    await reportPage.PdfAsync(new PagePdfOptions { Path = pdfPath, Format = "A4", PrintBackground = true,
        PreferCSSPageSize = true });
    if (!File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
        throw new InvalidOperationException("Nie zapisano raportu PDF.");
    Console.WriteLine($"Gotowy raport: {pdfPath}");
    Console.WriteLine($"Rzeczywiste zrzuty: {screenshotsDir}");
    Console.WriteLine($"Konto demonstracyjne pozostaje w bazie: {email}");
}
catch (Exception ex)
{
    Console.Error.WriteLine("Nie udało się zakończyć demonstracji. Sprawdź, czy wszystkie 5 aplikacji działa, oraz konsolę serwisów.");
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

async Task Capture(string file, string title, string description)
{
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    var bytes = await page.ScreenshotAsync(new PageScreenshotOptions
    { Path = Path.Combine(screenshotsDir, file + ".png"), FullPage = true });
    captured.Add((title, description, Convert.ToBase64String(bytes)));
}
