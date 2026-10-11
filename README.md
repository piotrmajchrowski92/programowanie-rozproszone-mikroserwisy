# System zgłoszeń — C# / Visual Studio

Rozwiązanie przygotowano na podstawie zadanie1.pdf: trzy mikroserwisy, API Gateway, klient i dokumentacja.

## Ważny status dostarczonego rozwiązania

Kod został przygotowany i sprawdzony statycznie pod kątem struktury plików, konfiguracji JSON/XML i kompletności artefaktów.
W środowisku tworzenia paczki nie było SDK .NET ani możliwości uruchomienia usług. **Nie wykonano kompilacji, testów integracyjnych ani demonstracji UI.**
Nie dołączono wymyślonych zrzutów ekranu.

Documentation/Opis_projektu.pdf opisuje rozwiązanie i instrukcję uruchomienia; nie jest jeszcze finalnym PDF-em ze zrzutami.
Po uruchomieniu projektu skrypt scripts/Generate-Report.ps1 tworzy rzeczywiste zrzuty oraz kompletny Documentation/Raport_z_dzialania.pdf.
To ten ostatni dokument należy oddać razem z projektem. Generator i obsługa klienta są napisane w C#.

## 1. Wymagania

- Windows i Visual Studio 2022 z aktualizacjami (zalecana aktualna wspierana wersja).
- Komponent „ASP.NET i tworzenie aplikacji internetowych” / „ASP.NET and web development”.
- SDK .NET 8, nie tylko runtime. Sprawdzenie: `dotnet --list-sdks`.
- Internet przy pierwszym przywróceniu pakietów NuGet i pobraniu Chromium do raportu.
- Wolne porty 5100, 5101, 5102, 5103 i 5200.

Repozytorium używa global.json z rollForward=latestFeature dla SDK 8.0.
Pakiety Microsoft w Shared mają zakres 8.0.*, a Playwright w generatorze 1.*.
NuGet pobierze najnowszą dostępną wersję w tych liniach. Wersje faktycznie pobrane są widoczne w obj/project.assets.json.
Po udanym pierwszym przywróceniu można zastąpić zakresy konkretnymi wersjami, by zamrozić środowisko do oddania projektu.

## 2. Uruchomienie w Visual Studio — podstawowy sposób

1. Wypakuj całe archiwum do katalogu z uprawnieniami zapisu.
2. Otwórz `MicroservicesAssignment.sln`.
3. Przywróć pakiety NuGet, np. przez prawy przycisk na rozwiązaniu → Restore NuGet Packages.
4. Wybierz konfigurację Debug / Any CPU i wykonaj Build → Build Solution.
5. Prawy przycisk na rozwiązaniu → Properties → Startup Project / Configure Startup Projects.
6. Wybierz Multiple startup projects. Ustaw Action = Start dla:
   - LogService
   - AuthService
   - TicketService
   - ApiGateway
   - WebClient
7. Shared pozostaw jako None. To biblioteka, nie proces.
8. Dla projektów używaj profili o nazwach takich jak projekty, a nie IIS Express.
9. Uruchom F5 lub Ctrl+F5.
10. Otwórz `http://127.0.0.1:5200`.

Wszystkie adresy testowe to HTTP. Nie przełączaj pojedynczego serwisu na HTTPS bez zmiany pozostałej konfiguracji.
Bazy tworzą się przy starcie, automatycznie, bez migracji i bez SQL Server.

Alternatywny sposób uruchomienia z PowerShell, w katalogu rozwiązania:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-All.ps1
```

Skrypt otwiera pięć konsol. Zatrzymanie: Ctrl+C w każdej konsoli i zamknięcie okien.
Nie uruchamiaj skryptu równocześnie z działającymi już projektami Visual Studio — porty będą zajęte.

## 3. Projekty i porty

| Projekt | Typ | Port | Baza | Zadanie |
|---|---|---:|---|---|
| AuthService | ASP.NET Core Minimal API | 5101 | AuthService/Data/auth.db | Rejestracja, logowanie, JWT |
| TicketService | ASP.NET Core Minimal API | 5102 | TicketService/Data/tickets.db | CRUD zgłoszeń |
| LogService | ASP.NET Core Minimal API | 5103 | LogService/Data/logs.db | Zapis i odczyt logów |
| ApiGateway | ASP.NET Core Minimal API | 5100 | — | Centralny routing i JWT |
| WebClient | ASP.NET Core Razor Pages | 5200 | — | Interfejs z obsługą w C# |
| Shared | Biblioteka .NET | — | — | Kontrakty, SQLite, JWT, AuditClient |
| tools/ReportGenerator | Dodatkowy program konsolowy C# | — | — | Rzeczywiste zrzuty i PDF |

Generator nie jest mikroserwisem ani projektem startowym; uruchamia się osobno skryptem.
Frontend nie wymaga Node.js, npm ani JavaScript. HTML i CSS definiują widoki, a obsługę formularzy i wywołania Gateway realizuje C#.

## 4. Architektura i przepływ

```text
Przeglądarka
    → WebClient :5200 (Razor Pages; C#)
        → ApiGateway :5100
            → AuthService :5101   → auth.db
            → TicketService :5102 → tickets.db
            → LogService :5103    → logs.db

AuthService / TicketService
    → ApiGateway /internal/logs (X-Internal-Key)
        → LogService /api/logs
```

Klient nie zna adresów usług, zna tylko GatewayUrl.
JWT sprawdza Gateway oraz niezależnie serwis docelowy; publiczny klient nie może zapisywać logów.
Gateway przekazuje wybrane nagłówki, kod statusu i treść odpowiedzi. Dla tego niewielkiego projektu proxy napisano w C# z HttpClient, bez dodatkowego frameworka Gateway.
Nie jest to zamiennik kompletnego reverse proxy do produkcji: brak np. load balancing, circuit breaker, pełnej obsługi streamingu/WebSocket.

## 5. Bazy danych

### AuthService — Users

Id (GUID jako TEXT), Email (unikalny), PasswordHash, CreatedAtUtc.
E-mail jest normalizowany do małych liter i trimowany. Hasło 8–128 znaków.
PasswordHasher przechowuje sformatowany skrót z parametrami i solą, nie jawne hasło.

### TicketService — Tickets

Id (INTEGER AUTOINCREMENT), OwnerId, Title, Description, Status, CreatedAtUtc, UpdatedAtUtc.
Tytuł 3–100 znaków, opis do 2000. Status: Nowe, W toku lub Zamknięte.
OwnerId pochodzi z claim sub w tokenie. Każdy SELECT, UPDATE i DELETE ogranicza dane do właściciela.
Cudzy i nieistniejący rekord daje 404, aby nie ujawniać jego istnienia.
Nie ma klucza obcego do Users: to odrębne bazy mikroserwisów.

### LogService — Logs

Id, TimestampUtc, UserId, Service, Action, Details.
System zapisuje REGISTER, LOGIN, LOGIN_FAILED, CREATE, READ, READ_LIST, UPDATE, DELETE.
Logi są trwałe i zawierają czas UTC generowany w LogService.
Użytkownik odczytuje tylko własne wpisy. Logi nieudanego logowania na nieistniejące konto mają pusty UserId i nie pojawiają się w panelu zwykłego użytkownika.

## 6. API publiczne — wszystkie adresy przez Gateway :5100

| Metoda | Ścieżka | JWT | Prawidłowy wynik |
|---|---|---|---|
| POST | /api/auth/register | Nie | 201 |
| POST | /api/auth/login | Nie | 200 + token |
| GET | /api/auth/validate | Tak | 200 + valid=true |
| GET | /api/tickets | Tak | 200 + lista |
| GET | /api/tickets/{id} | Tak | 200 + rekord |
| POST | /api/tickets | Tak | 201 + rekord |
| PUT | /api/tickets/{id} | Tak | 200 + rekord |
| DELETE | /api/tickets/{id} | Tak | 204 |
| GET | /api/logs?take=100 | Tak | 200 + lista |

Chronione API wymaga `Authorization: Bearer <token>`.
Błędne dane: 400; brak/poprawność/wygasanie tokenu: 401; duplikat e-mail: 409; cudzy/brakujący rekord: 404; niedostępny serwis: 502.
Limit `take` jest ograniczany do 1–200; UI wyświetla do 100 logów.
Każda z pięciu aplikacji udostępnia `/health`.

API wewnętrzne:
- Gateway: POST /internal/logs, wymagany X-Internal-Key.
- LogService: POST /api/logs, wymagany ten sam X-Internal-Key.
- Gateway nie wystawia publicznego POST /api/logs i nie przenosi klucza wewnętrznego z publicznych żądań.

Przykładowe JSON-y:

```json
{"email":"student@example.com","password":"DemoPass123!"}
```

```json
{"title":"Brak dostępu do raportu","description":"Proszę sprawdzić uprawnienia.","status":"Nowe"}
```

## 7. Ręczna demonstracja

1. Zarejestruj `student@example.com`, hasło `DemoPass123!` (przykładowe lokalne konto).
2. Zaloguj się. Sprawdź e-mail oraz czas ważności tokenu w panelu.
3. Kliknij „Sprawdź JWT”.
4. Dodaj zgłoszenie „Brak dostępu do raportu Power BI”, status Nowe.
5. Sprawdź listę i log CREATE. Odświeżenie listy generuje READ_LIST.
6. Kliknij Edytuj, zmień tytuł i status na W toku, zapisz.
7. Sprawdź zmiany i logi READ oraz UPDATE.
8. Usuń zgłoszenie. Lista będzie pusta, log DELETE pozostanie.
9. Wyloguj się. Panel CRUD nie będzie widoczny.
10. Dla izolacji danych utwórz drugie konto. Nie zobaczy danych pierwszego.

GET listy jest audytowany, więc odświeżanie panelu dodaje READ_LIST. To zamierzone zachowanie.
Wylogowanie usuwa token z sesji klienta, ale nie unieważnia już wydanego JWT globalnie.

## 8. Testy API

Po uruchomieniu pięciu aplikacji, w osobnej konsoli:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Smoke-Test.ps1
```

Testy sprawdzają health, brak JWT, logi bez JWT, zabezpieczenie zapisu logów,
rejestrację, duplikat, złe hasło, logowanie, poprawny i uszkodzony token,
walidację zgłoszenia, pełny CRUD, izolację dwóch kont i komplet działań w logach.
Skrypt kończy się błędem przy niezgodności; komunikat „Wszystkie testy...” pojawia się dopiero po przejściu testów.
Konta testowe pozostają w bazie, zgłoszenie testowe jest usuwane.

## 9. Finalny PDF z rzeczywistymi zrzutami — automatycznie w C#

Uruchomione musi być całe rozwiązanie. Następnie:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Generate-Report.ps1
```

Skrypt:
1. Kompiluje tools/ReportGenerator w C#.
2. Instaluje Chromium dla Playwright (pierwszy raz wymaga internetu).
3. Otwiera rzeczywiście działający WebClient w przeglądarce headless.
4. Przechodzi i sprawdza: rejestrację, logowanie, CREATE/READ/UPDATE/DELETE, weryfikację JWT i wylogowanie.
5. Robi osiem rzeczywistych plików PNG w Screenshots.
6. Tworzy Documentation/Raport_z_dzialania.html oraz Documentation/Raport_z_dzialania.pdf, z opisem i zrzutami.

Zrzuty nie są makietami. Raport nie jest generowany przy nieudanym scenariuszu UI.
Nie usuwa starego raportu po nieudanej kolejnej próbie, więc sprawdź datę i wynik konsoli, zanim go oddasz.
Generator tworzy osobne losowe konto demonstracyjne; po wykonaniu rekord zgłoszenia jest usunięty, a konto i logi pozostają.
Nie trzeba instalować Pythona ani narzędzia do PDF. Kod generatora można otworzyć w Visual Studio osobno jako projekt .csproj.

Jeśli nie chcesz używać generatora, wykonaj scenariusz ręcznie, zrób zrzuty (Win+Shift+S), wklej je z opisem do Worda i wyeksportuj PDF.
Nie oddawaj samego Opis_projektu.pdf jako dokumentu spełniającego wymaganie zrzutów.

## 10. Bezpieczeństwo i świadome uproszczenia

Projekt służy lokalnej demonstracji, a nie wdrożeniu produkcyjnemu:
- Sekrety w appsettings.json są demonstracyjne. Zmień Jwt:Key i InternalKey przed innym użyciem.
- Jwt:Issuer, Jwt:Audience i Jwt:Key muszą być identyczne w AuthService, TicketService, LogService i ApiGateway.
- InternalKey musi być identyczny w tych czterech projektach.
- Można użyć User Secrets (każdy projekt ma UserSecretsId) lub zmiennych środowiskowych Jwt__Key i InternalKey.
- Klucze muszą mieć co najmniej 32 bajty/znaki; domyślne są dłuższe.
- Wszystkie usługi nasłuchują na 127.0.0.1. Na produkcji ogranicz dostęp sieciowy tak, aby publiczny był tylko Gateway/klient; samo JWT nie wymusza ścieżki sieciowej przez Gateway.
- HTTP jest używane tylko lokalnie. W produkcji należy skonfigurować TLS i bezpieczne ciasteczka.
- JWT sprawdza algorytm HS256, podpis, issuer, audience i exp, z tolerancją 10 sekund.
- Sesja klienta jest w pamięci serwera; restart WebClient wylogowuje użytkownika. Nie ma tokenu w localStorage.
- Razor Pages domyślnie używa anty-CSRF dla formularzy POST; Razor koduje wyświetlany tekst.
- Brak MFA, refresh tokenów, rate limiting, ról administratora i blacklisty JWT.
- Logi są best-effort: AuditClient czeka maksymalnie 3 s; awaria zapisu nie cofa operacji biznesowej. Ostrzeżenie trafia do konsoli źródłowego serwisu. Do gwarantowanego audytu potrzebne byłyby outbox i kolejka.
- Odczyt logów nie generuje kolejnego audytu, aby uniknąć rekurencji.
- SQLite jest obsługiwany poleceniami parametryzowanymi. Repozytoria są synchroniczne i przeznaczone dla małego projektu demonstracyjnego.

## 11. Typowe problemy

- Brak SDK: zainstaluj SDK .NET 8, sprawdź `dotnet --list-sdks`; sam runtime nie wystarczy.
- Port zajęty: zatrzymaj wcześniejsze procesy; nie uruchamiaj aplikacji jednocześnie skryptem i z Visual Studio.
- Błąd 502: sprawdź, czy serwis docelowy działa i czy adres w ApiGateway/appsettings.json jest poprawny.
- Błąd 401 po zmianie sekretów: identyczne ustawienia JWT w czterech projektach, restart i ponowne logowanie.
- Zajęta/zablokowana baza: zatrzymaj usługi i zamknij przeglądarkę SQLite, sprawdź prawo zapisu do katalogu.
- Przywracanie pakietów: sprawdź źródło nuget.org, internet i ewentualny firmowy proxy.
- Brak Chromium: ponownie uruchom Generate-Report.ps1 i sprawdź komunikat instalacji Playwright.
- PDF nie został utworzony: sprawdź, czy UI działa ręcznie i czy testy API przechodzą; generator zgłasza błąd zamiast tworzyć fikcyjny raport.

Reset danych: zatrzymaj procesy i usuń katalogi Data w trzech serwisach. Restart odtworzy puste bazy.

## 12. Lista przed oddaniem

- [ ] Projekt kompiluje się w Twoim Visual Studio.
- [ ] Wszystkie pięć aplikacji działa.
- [ ] Smoke-Test.ps1 przechodzi.
- [ ] Sprawdzono ręcznie UI lub poprawnie wykonano generator.
- [ ] Dołączono Documentation/Raport_z_dzialania.pdf z rzeczywistymi zrzutami.
- [ ] Nie dołączono bin, obj, .vs, prawdziwych danych ani rzeczywistych sekretów.

## Dokumentacja technologii

ASP.NET Core JWT: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication
Microsoft.Data.Sqlite: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/
Playwright .NET: https://playwright.dev/dotnet/docs/intro
