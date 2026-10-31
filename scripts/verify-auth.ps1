# Auth enforcement smoke test against a running MyHomeLab instance.
# Fails (exit 1) if any protected /api route answers without a session, if the
# anonymous carve-outs break, or - with -Username/-Password - if the full
# login -> cookie -> use -> logout -> revoked flow misbehaves.
# Trusts self-signed certs for this process only. Windows PowerShell 5.1 safe.
param(
    [string]$BaseUrl = 'https://localhost',
    [string]$Username,
    [string]$Password,
    [string]$ApiToken,
    [string]$ActionToken
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

if (-not ("MyHomeLabTrustAllCerts" -as [type])) {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class MyHomeLabTrustAllCerts : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
}
"@
}
if (-not ('System.Net.Http.HttpResponseMessage' -as [type])) {
    Add-Type -AssemblyName System.Net.Http
}
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Net.ServicePointManager]::CertificatePolicy = New-Object MyHomeLabTrustAllCerts

function Invoke-Probe {
    param(
        [string]$Method,
        [string]$Path,
        [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [hashtable]$Headers
    )
    $arguments = @{
        Method          = $Method
        Uri             = "$BaseUrl$Path"
        UseBasicParsing = $true
        TimeoutSec      = 15
        ErrorAction     = 'Stop'
    }
    if ($Session) { $arguments.WebSession = $Session }
    if ($Headers) { $arguments.Headers = $Headers }

    try {
        $response = Invoke-WebRequest @arguments
        return [pscustomobject]@{
            Status      = [int]$response.StatusCode
            ContentType = [string]$response.Headers['Content-Type']
            Body        = [string]$response.Content
        }
    }
    catch {
        $response = $_.Exception.Response
        if (-not $response) {
            throw "Cannot reach $($arguments.Uri) - is the hub running? ($($_.Exception.Message))"
        }

        $contentType = ''
        $body = ''
        if ($response -is [System.Net.Http.HttpResponseMessage]) {
            $contentType = [string]$response.Content.Headers.ContentType
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
        else {
            $contentType = [string]$response.ContentType
            try {
                $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
                $body = $reader.ReadToEnd()
                $reader.Dispose()
            }
            catch { }
        }

        return [pscustomobject]@{
            Status      = [int]$response.StatusCode
            ContentType = $contentType
            Body        = $body
        }
    }
}

$failures = 0

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if ($Condition) {
        Write-Host "[ok] $Message"
    }
    else {
        Write-Host "[FAIL] $Message"
        $script:failures++
    }
}

Write-Host "[info] probing $BaseUrl ..."

try {
    $status = Invoke-Probe -Method Get -Path '/api/auth/status'
}
catch {
    Write-Host "[FAIL] $_"
    exit 1
}

# --- anonymous carve-outs -----------------------------------------------------

Assert-True ($status.Status -eq 200) "GET /api/auth/status is anonymous (got $($status.Status))"
$needsSetup = $null
try { $needsSetup = ($status.Body | ConvertFrom-Json).needsSetup } catch { }
Assert-True ($null -ne $needsSetup) 'GET /api/auth/status returns a needsSetup field'
if ($needsSetup -eq $true) {
    Write-Host "[info] hub is in first-run state: create the account at $BaseUrl/setup"
}

$spa = Invoke-Probe -Method Get -Path '/'
if ($spa.Status -eq 200) {
    Write-Host '[ok] GET / serves the SPA anonymously'
}
else {
    Write-Host "[warn] GET / returned $($spa.Status) (SPA not built into wwwroot - run scripts/build.ps1 if that matters)"
}

# --- protected routes: 401 problem+json without a session ---------------------

$protected = @(
    @{ Method = 'GET';  Path = '/api/apps' },
    @{ Method = 'GET';  Path = '/api/categories' },
    @{ Method = 'GET';  Path = '/api/machines' },
    @{ Method = 'GET';  Path = '/api/files' },
    @{ Method = 'GET';  Path = '/api/logs' },
    @{ Method = 'GET';  Path = '/api/system' },
    @{ Method = 'GET';  Path = '/api/system/network' },
    @{ Method = 'GET';  Path = '/api/system/postgres' },
    @{ Method = 'GET';  Path = '/api/docker/containers' },
    @{ Method = 'GET';  Path = '/api/terminal/config' },
    @{ Method = 'GET';  Path = '/api/terminal/ws' },
    @{ Method = 'GET';  Path = '/api/api-keys' },
    @{ Method = 'GET';  Path = '/api/auth/me' },
    @{ Method = 'POST'; Path = '/api/auth/logout' },
    @{ Method = 'POST'; Path = '/api/auth/logout-all' },
    @{ Method = 'GET';  Path = '/swagger/v1/swagger.json' }
)

foreach ($route in $protected) {
    $result = Invoke-Probe -Method $route.Method -Path $route.Path
    $label = "$($route.Method) $($route.Path)"
    Assert-True ($result.Status -eq 401) "$label -> 401 without session (got $($result.Status))"
    if ($result.Status -eq 401) {
        Assert-True ($result.ContentType -like 'application/problem+json*') "$label 401 body is problem+json"
    }
}

# Informational: healthz visibility is a policy choice, not a leak.
$health = Invoke-Probe -Method Get -Path '/api/healthz'
if ($health.Status -eq 401) {
    Write-Host '[info] GET /api/healthz is protected (401). Give MetaController [AllowAnonymous] if monitoring scripts need it.'
}
else {
    Write-Host "[info] GET /api/healthz is anonymous ($($health.Status))."
}

# --- optional read-only API token flow ----------------------------------------

if ($ApiToken) {
    $tokenHeaders = @{ 'X-Api-Key' = $ApiToken }
    foreach ($path in @('/api/apps', '/api/system', '/api/logs')) {
        $result = Invoke-Probe -Method Get -Path $path -Headers $tokenHeaders
        Assert-True ($result.Status -eq 200) "GET $path with API token -> 200 (got $($result.Status))"
    }

    $notAllowed = Invoke-Probe -Method Get -Path '/api/system/network' -Headers $tokenHeaders
    Assert-True ($notAllowed.Status -eq 401) "GET /api/system/network is outside the token scope (got $($notAllowed.Status))"

    $management = Invoke-Probe -Method Get -Path '/api/api-keys' -Headers $tokenHeaders
    Assert-True ($management.Status -eq 401) "GET /api/api-keys rejects API tokens (got $($management.Status))"

    $invalid = Invoke-Probe -Method Get -Path '/api/apps' -Headers @{ 'X-Api-Key' = 'mhl_invalid' }
    Assert-True ($invalid.Status -eq 401) "GET /api/apps rejects an invalid API token (got $($invalid.Status))"
}
else {
    Write-Host '[info] pass -ApiToken to verify the read-only API-token scope.'
}

# --- optional action-token authorization checks -------------------------------

if ($ActionToken) {
    # Random app ids keep these policy checks side-effect free: an authorized
    # request reaches the controller and gets 404; a read-only key gets 401/403.
    $actionHeaders = @{ 'X-Api-Key' = $ActionToken }
    $missingId = [guid]::NewGuid().ToString()
    $deploy = Invoke-Probe -Method Post -Path "/api/apps/$missingId/deploy" -Headers $actionHeaders
    Assert-True ($deploy.Status -eq 404) "POST deploy with action token is authorized, then app-not-found (got $($deploy.Status))"

    $restart = Invoke-Probe -Method Post -Path "/api/apps/$missingId/container/restart" -Headers $actionHeaders
    Assert-True ($restart.Status -eq 404) "POST container restart with action token is authorized, then app-not-found (got $($restart.Status))"
}
else {
    Write-Host '[info] pass -ActionToken to verify the redeploy/restart-key policy without triggering a real action.'
}

# --- optional full credential flow --------------------------------------------

if ($Username -and $Password) {
    if (-not $BaseUrl.StartsWith('https://')) {
        Write-Host '[warn] credential flow needs an https base URL (the Secure cookie is never replayed over plain http); skipping.'
    }
    else {
        $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $loginOk = $false
        try {
            $login = Invoke-WebRequest -Method Post -Uri "$BaseUrl/api/auth/login" `
                -ContentType 'application/json' `
                -Body (@{ username = $Username; password = $Password } | ConvertTo-Json) `
                -WebSession $session -UseBasicParsing -ErrorAction Stop
            $loginOk = $true
            Assert-True ([int]$login.StatusCode -eq 200) 'POST /api/auth/login -> 200'
        }
        catch {
            $statusCode = -1
            if ($_.Exception.Response) { $statusCode = [int]$_.Exception.Response.StatusCode }
            if ($statusCode -eq 429) {
                Write-Host '[FAIL] login -> 429: rate limited, wait up to 15 minutes and retry'
            }
            elseif ($statusCode -eq 401) {
                Write-Host '[FAIL] login -> 401: username or password is wrong'
            }
            else {
                Write-Host "[FAIL] login failed (status $statusCode)"
            }
            $failures++
        }

        if ($loginOk) {
            $cookie = $session.Cookies.GetCookies($BaseUrl)['__Host-mhl.session']
            Assert-True ($null -ne $cookie) 'session cookie __Host-mhl.session was issued'

            $me = Invoke-Probe -Method Get -Path '/api/auth/me' -Session $session
            Assert-True ($me.Status -eq 200) "GET /api/auth/me with cookie -> 200 (got $($me.Status))"

            $apps = Invoke-Probe -Method Get -Path '/api/apps' -Session $session
            Assert-True ($apps.Status -eq 200) "GET /api/apps with cookie -> 200 (got $($apps.Status))"

            $logout = Invoke-Probe -Method Post -Path '/api/auth/logout' -Session $session
            Assert-True ($logout.Status -eq 204) "POST /api/auth/logout -> 204 (got $($logout.Status))"

            $meAfter = Invoke-Probe -Method Get -Path '/api/auth/me' -Session $session
            Assert-True ($meAfter.Status -eq 401) "GET /api/auth/me after logout -> 401 again (got $($meAfter.Status))"
        }
    }
}
else {
    Write-Host '[info] pass -Username/-Password to also verify the login/cookie/logout flow (https base URL required).'
}

if ($failures -eq 0) {
    Write-Host '[ok] all auth enforcement checks passed'
    exit 0
}

Write-Host "[FAIL] $failures check(s) failed"
exit 1
