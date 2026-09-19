# Registers MyHomeLab as a Windows service (auto-start, survives reboot).
# Run from an elevated PowerShell. Generates a self-signed HTTPS cert if absent.
param(
    [string]$ServiceName = 'MyHomeLab',
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'
$certDir = Join-Path $root 'certs'
$certFile = Join-Path $certDir 'myhomelab.pfx'
$certPasswordFile = Join-Path $certDir 'myhomelab.pfx.password'
$dll = Join-Path $publishDir 'MyHomeLab.Api.dll'

if ($Uninstall) {
    if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
        Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Write-Host "[ok] service '$ServiceName' removed"
    }
    else {
        Write-Host "[skip] service '$ServiceName' not installed"
    }
    return
}

if (-not (Test-Path $dll)) {
    throw "Published output not found. Run .\scripts\build.ps1 first (missing: $dll)"
}

if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
    # HTTPS cert: reuse a generated pfx or create one for the LAN hostname.
    if (-not (Test-Path $certFile)) {
        New-Item -ItemType Directory -Path $certDir -Force | Out-Null
        $password = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 24 | ForEach-Object { [char]$_ })
        dotnet dev-certs https -ep $certFile -p $password
        Set-Content -Path $certPasswordFile -Value $password -NoNewline
        Write-Host "[ok] generated HTTPS cert at $certFile"
    }

    if (Test-Path $certPasswordFile) {
        [Environment]::SetEnvironmentVariable('ASPNETCORE_Kestrel__Certificates__Default__Path', $certFile, 'Machine')
        [Environment]::SetEnvironmentVariable('ASPNETCORE_Kestrel__Certificates__Default__Password', (Get-Content $certPasswordFile -Raw), 'Machine')
    }

    [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', 'http://0.0.0.0:8080;https://0.0.0.0:443', 'Machine')

    $dbPassword = [Environment]::GetEnvironmentVariable('DB_PASS', 'User')
    if (-not $dbPassword) { $dbPassword = [Environment]::GetEnvironmentVariable('DB_PASS', 'Process') }
    if ($dbPassword) {
        [Environment]::SetEnvironmentVariable('MYHOMELAB_DB_PASSWORD', $dbPassword, 'Machine')
        Write-Host "[ok] DB password pinned as machine env (MYHOMELAB_DB_PASSWORD)"
    }
    else {
        Write-Host "[warn] DB_PASS not found - service may fail to reach Postgres"
    }

    $binPath = "dotnet `"$dll`""
    New-Service -Name $ServiceName -BinaryPathName $binPath -StartupType Automatic -DisplayName 'MyHomeLab Hub' | Out-Null

    Write-Host "[ok] service '$ServiceName' registered"
}
else {
    Write-Host "[skip] service '$ServiceName' already registered"
}

Start-Service $ServiceName -ErrorAction SilentlyContinue
Write-Host "[ok] service '$ServiceName' started"