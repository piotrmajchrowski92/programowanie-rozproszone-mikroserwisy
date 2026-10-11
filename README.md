# System zgłoszeń

Aplikacja w C# i ASP.NET Core 8 składająca się z trzech mikroserwisów, API Gateway i klienta webowego. Użytkownik może utworzyć konto, zalogować się, zarządzać swoimi zgłoszeniami i przeglądać historię działań.

## Technologie

C# / .NET 8, ASP.NET Core Minimal API, Razor Pages, SQLite, JWT Bearer oraz Playwright .NET do automatyzacji demonstracji.

## Struktura

| Projekt | Port | Zakres |
|---|---:|---|
| AuthService | 5101 | Rejestracja, logowanie, wydawanie i weryfikacja JWT |
| TicketService | 5102 | Tworzenie, odczyt, edycja i usuwanie zgłoszeń |
| LogService | 5103 | Zapis zdarzeń i odczyt logów |
| ApiGateway | 5100 | Routing żądań i weryfikacja JWT |
| WebClient | 5200 | Interfejs Razor Pages |
| Shared | — | Kontrakty i wspólna infrastruktura |
| tools/ReportGenerator | — | Scenariusz demonstracyjny, zrzuty i raport PDF |

```text
Przeglądarka → WebClient → ApiGateway
                            ├── AuthService   → auth.db
                            ├── TicketService → tickets.db
                            └── LogService    → logs.db

AuthService / TicketService → Gateway /internal/logs → LogService
```

Każdy mikroserwis ma własną bazę SQLite. Shared jest biblioteką, a generator raportu osobnym programem konsolowym. Nie są uruchamiane jako mikroserwisy.

## Uruchomienie

Wymagania: Windows, Visual Studio 2022 z komponentem ASP.NET i tworzenie aplikacji internetowych oraz SDK .NET 8. Porty 5100–5103 i 5200 muszą być wolne.

1. Sklonuj repozytorium i otwórz `MicroservicesAssignment.sln`.
2. Przywróć pakiety NuGet i zbuduj rozwiązanie.
3. W Solution Explorer kliknij prawym przyciskiem rozwiązanie
   i wybierz Properties → Common Properties → Configure Startup Projects
   (w niektórych wersjach: Startup Project).
4. Zaznacz Multiple startup projects i ustaw:

   | Projekt | Action | Debug Target |
   |---|---|---|
   | LogService | Start | LogService |
   | AuthService | Start | AuthService |
   | TicketService | Start | TicketService |
   | ApiGateway | Start | ApiGateway |
   | WebClient | Start | WebClient |
   | Shared | None | — |

5. Zapisz ustawienia i wybierz utworzony profil na pasku narzędzi.
6. Naciśnij F5 i poczekaj na uruchomienie aplikacji.
7. Otwórz http://127.0.0.1:5200.

Build Solution kompiluje kod, ale nie uruchamia aplikacji.
5. Otwórz `http://127.0.0.1:5200`.

Bazy tworzą się automatycznie w katalogu `Data` każdego mikroserwisu. SQL Server nie jest wymagany.

Alternatywnie, z katalogu rozwiązania:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-All.ps1
```

Skrypt uruchamia pięć aplikacji w osobnych konsolach. Nie należy uruchamiać drugiej kopii aplikacji na tych samych portach.

Plik `global.json` wybiera stabilne SDK z linii 8.0. Pakiety Microsoft używają zakresu 8.0.*, a Playwright 1.*.

## Funkcjonalność i dane

Użytkownik rejestruje konto i loguje się, otrzymując JWT. Klient przechowuje token w sesji po stronie serwera i używa go przy wywołaniach API.

Zgłoszenie zawiera tytuł (3–100 znaków), opis (do 2000 znaków) oraz status: `Nowe`, `W toku` lub `Zamknięte`. Właściciel jest identyfikowany przez claim `sub` w JWT. Odczyt, aktualizacja i usunięcie dotyczą tylko jego rekordów.

| Baza | Tabela | Pola |
|---|---|---|
| auth.db | Users | Id, Email, PasswordHash, CreatedAtUtc |
| tickets.db | Tickets | Id, OwnerId, Title, Description, Status, CreatedAtUtc, UpdatedAtUtc |
| logs.db | Logs | Id, TimestampUtc, UserId, Service, Action, Details |

Historia obejmuje REGISTER, LOGIN, LOGIN_FAILED, CREATE, READ, READ_LIST, UPDATE i DELETE. Czas jest zapisywany w UTC. Panel pokazuje do 100 własnych logów, a API pozwala odczytać do 200.

## API Gateway

Adres: `http://127.0.0.1:5100`.

| Metoda | Ścieżka | JWT |
|---|---|---|
| POST | /api/auth/register | Nie |
| POST | /api/auth/login | Nie |
| GET | /api/auth/validate | Tak |
| GET | /api/tickets | Tak |
| GET | /api/tickets/{id} | Tak |
| POST | /api/tickets | Tak |
| PUT | /api/tickets/{id} | Tak |
| DELETE | /api/tickets/{id} | Tak |
| GET | /api/logs?take=100 | Tak |

Chronione żądania wymagają `Authorization: Bearer <token>`. Błędne dane zwracają 400, brak lub nieważny JWT 401, duplikat e-mail 409, cudzy lub nieistniejący rekord 404, a niedostępny serwis docelowy 502.

Zapis logów odbywa się przez wewnętrzny endpoint Gateway `/internal/logs`, chroniony nagłówkiem `X-Internal-Key`. Każda aplikacja udostępnia `/health`.

## Testy

Po uruchomieniu aplikacji:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Smoke-Test.ps1
```

Skrypt sprawdza dostępność usług, rejestrację i logowanie, CRUD, walidację, brak lub uszkodzenie JWT, izolację danych użytkowników i obecność logów. W razie niezgodności kończy się błędem. Konta testowe pozostają w bazie; testowe zgłoszenie jest usuwane.

## Dokumentacja

Opis architektury, API i scenariusza działania znajduje się w `Documentation/Raport_z_dzialania.pdf`.

Raport można ponownie wygenerować przy uruchomionych aplikacjach:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Generate-Report.ps1
```

Generator przechodzi rejestrację, logowanie, CRUD, weryfikację JWT i wylogowanie. Zapisuje zrzuty w `Screenshots` oraz raport HTML i PDF w `Documentation`. Pierwsze uruchomienie wymaga pobrania Chromium dla Playwright.

Demonstracja interfejsu i testy API są oddzielnymi mechanizmami weryfikacji. Jeśli generowanie zakończy się błędem, na dysku może pozostać raport z wcześniejszego uruchomienia.

## Konfiguracja i ograniczenia

- Ustawienia w `appsettings.json` służą lokalnemu uruchomieniu. Klucze JWT i komunikacji wewnętrznej są demonstracyjne.
- Parametry JWT i klucz wewnętrzny muszą być zgodne w AuthService, TicketService, LogService i ApiGateway.
- Hasła są przechowywane jako skróty PasswordHasher. Zapytania SQL są parametryzowane.
- Gateway i serwisy niezależnie sprawdzają podpis HS256, issuer, audience i ważność tokenu, z tolerancją 10 sekund.
- Restart WebClient usuwa sesje. Wylogowanie nie unieważnia globalnie wydanego wcześniej JWT.
- Audyt działa w trybie best-effort: awaria zapisu logu nie wycofuje operacji biznesowej. Nie zastosowano kolejki ani wzorca outbox.
- Brak MFA, refresh tokenów, ról administratora i listy unieważnionych JWT.
- Serwisy nasłuchują na lokalnym HTTP. Inne środowisko wymaga dostosowania TLS, dostępu sieciowego i zarządzania sekretami.
- Gateway wykorzystuje HttpClient; nie implementuje load balancing, circuit breaker ani WebSocket.

Reset danych: zatrzymaj usługi i usuń ich katalogi `Data`. Ponowne uruchomienie odtworzy puste bazy.
