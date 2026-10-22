# Development runner: boots the API (watch) behind the scenes and the Vite
# dev server in the foreground. API binds http://192.168.15.22:8080 + https://192.168.15.22:443.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\create-db.ps1"

dotnet dev-certs https --trust 2>$null | Out-Null

$env:ASPNETCORE_ENVIRONMENT = 'Development'
Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue

# Stable JWT signing key so dev sessions survive API restarts.
$secretDir = Join-Path $env:LOCALAPPDATA 'MyHomeLab'
$secretFile = Join-Path $secretDir 'auth.jwt-secret'
if (-not (Test-Path $secretFile)) {
    New-Item -ItemType Directory -Path $secretDir -Force | Out-Null
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    Set-Content -Path $secretFile -Value ([Convert]::ToBase64String($bytes)) -NoNewline
    Write-Host "[dev] generated JWT signing secret at $secretFile"
}
$env:Auth__JwtSecret = Get-Content $secretFile -Raw

Write-Host "[dev] starting API on :8080 and :443 ..."
$api = Start-Process dotnet -ArgumentList @('run', '--project', "$root\src\MyHomeLab.Api") `
    -WorkingDirectory $root -WindowStyle Hidden -PassThru

try {
    Write-Host "[dev] starting Vite dev server on http://localhost:5173 (proxy /api -> 192.168.15.22:8080)"
    Push-Location "$root\frontend"
    npm run dev
}
finally {
    Pop-Location
    if ($api -and -not $api.HasExited) {
        Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
    }
}