param([string]$Gateway = 'http://127.0.0.1:5100')
$ErrorActionPreference = 'Stop'
$Gateway = $Gateway.TrimEnd('/')
function Call-Api([string]$Method, [string]$Path, $Body = $null, [string]$Token = '') {
    $args = @{ Uri = "$Gateway$Path"; Method = $Method; UseBasicParsing = $true }
    if ($Token) { $args.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) {
        $args.ContentType = 'application/json; charset=utf-8'
        $args.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10))
    }
    try {
        $response = Invoke-WebRequest @args
        $data = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Data = $data }
    } catch {
        if ($null -eq $_.Exception.Response) { throw }
        return [pscustomobject]@{ Status = [int]$_.Exception.Response.StatusCode; Data = $null }
    }
}
function Expect($Response, [int]$Status, [string]$Name) {
    if ($Response.Status -ne $Status) { throw "$Name : oczekiwano HTTP $Status, otrzymano $($Response.Status)" }
    Write-Host "OK: $Name (HTTP $Status)" -ForegroundColor Green
}
foreach ($port in @(5100,5101,5102,5103,5200)) {
    $health = Invoke-WebRequest "http://127.0.0.1:$port/health" -UseBasicParsing
    if ($health.StatusCode -ne 200) { throw "Brak health na porcie $port" }
}
$credentials = @{ email = "test-$([Guid]::NewGuid().ToString('N'))@example.com"; password = 'DemoPass123!' }
Expect (Call-Api 'GET' '/api/tickets') 401 'CRUD bez JWT'
Expect (Call-Api 'GET' '/api/logs') 401 'Logi bez JWT'
Expect (Call-Api 'POST' '/internal/logs' @{userId='';service='Test';action='TEST';details='test'}) 401 'Zapis logu bez klucza'
Expect (Call-Api 'POST' '/api/auth/register' $credentials) 201 'Rejestracja'
Expect (Call-Api 'POST' '/api/auth/register' $credentials) 409 'Duplikat e-mail'
Expect (Call-Api 'POST' '/api/auth/login' @{email=$credentials.email;password='WrongPass123!'}) 401 'Bledne haslo'
$login = Call-Api 'POST' '/api/auth/login' $credentials
Expect $login 200 'Logowanie'
$token = $login.Data.token
Expect (Call-Api 'GET' '/api/auth/validate' $null $token) 200 'Weryfikacja JWT'
Expect (Call-Api 'GET' '/api/auth/validate' $null ($token + 'x')) 401 'Zmodyfikowany JWT'
$ticket = @{title='Test automatyczny';description='Test integracyjny CRUD';status='Nowe'}
Expect (Call-Api 'POST' '/api/tickets' @{title='x';description='';status='Zly'} $token) 400 'Walidacja danych'
$create = Call-Api 'POST' '/api/tickets' $ticket $token
Expect $create 201 'CREATE'
$id = $create.Data.id
$read = Call-Api 'GET' "/api/tickets/$id" $null $token
Expect $read 200 'READ'
if ($read.Data.title -ne $ticket.title) { throw 'Odczyt zwrocil inny tytul.' }
$list = Call-Api 'GET' '/api/tickets' $null $token
Expect $list 200 'READ lista'
if (@($list.Data | Where-Object id -eq $id).Count -ne 1) { throw 'Brak rekordu na liscie.' }
$other = @{email="other-$([Guid]::NewGuid().ToString('N'))@example.com";password='DemoPass123!'}
Expect (Call-Api 'POST' '/api/auth/register' $other) 201 'Drugie konto'
$otherLogin = Call-Api 'POST' '/api/auth/login' $other
Expect $otherLogin 200 'Logowanie drugiego konta'
Expect (Call-Api 'GET' "/api/tickets/$id" $null $otherLogin.Data.token) 404 'Izolacja danych: cudzy rekord'
$ticket.status = 'W toku'
$update = Call-Api 'PUT' "/api/tickets/$id" $ticket $token
Expect $update 200 'UPDATE'
if ($update.Data.status -ne 'W toku') { throw 'Status nie zostal zmieniony.' }
Expect (Call-Api 'DELETE' "/api/tickets/$id" $null $token) 204 'DELETE'
Expect (Call-Api 'GET' "/api/tickets/$id" $null $token) 404 'Odczyt po usunieciu'
$logs = Call-Api 'GET' '/api/logs?take=200' $null $token
Expect $logs 200 'Odczyt logow'
foreach ($action in @('REGISTER','LOGIN_FAILED','LOGIN','CREATE','READ','READ_LIST','UPDATE','DELETE')) {
    if (@($logs.Data | Where-Object action -eq $action).Count -eq 0) { throw "Brak logu: $action" }
}
if (@($logs.Data | Where-Object userId -ne $login.Data.userId).Count -gt 0) { throw 'Wyciekanie logow innego uzytkownika.' }
Write-Host 'Wszystkie testy zakonczone poprawnie.' -ForegroundColor Green
Write-Host "Utworzone konta testowe pozostaja w bazie: $($credentials.email), $($other.email)"
