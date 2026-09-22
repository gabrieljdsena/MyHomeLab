# Installs the PawnIO kernel driver, required by LibreHardwareMonitor (0.9.5+) to read
# CPU/motherboard temperatures. Without it the hub's System panel shows no CPU temp.
# Run from an elevated PowerShell. Idempotent: skips if PawnIO is already installed.
param(
    [string]$ServiceName = 'MyHomeLab'
)

$ErrorActionPreference = 'Stop'

$version = '2.2.0'
$sha256 = '1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032'
$url = "https://github.com/namazso/PawnIO.Setup/releases/download/$version/PawnIO_setup.exe"

if (sc.exe query PawnIO 2>$null | Select-String -Pattern 'SERVICE_NAME' -Quiet) {
    Write-Host "[skip] PawnIO already installed"
}
else {
    $installer = Join-Path $env:TEMP "PawnIO_setup-$version.exe"
    Write-Host "[..] downloading PawnIO $version"
    Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installer).Hash.ToLower()
    if ($hash -ne $sha256) {
        Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
        throw "PawnIO installer hash mismatch (got $hash, expected $sha256)"
    }

    $proc = Start-Process -FilePath $installer -ArgumentList '-install', '-silent' -Wait -PassThru
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue

    if ($proc.ExitCode -notin 0, 3010) {
        throw "PawnIO installer failed with exit code $($proc.ExitCode)"
    }
    if ($proc.ExitCode -eq 3010) {
        Write-Host "[warn] PawnIO installed - a reboot is required before temperatures appear"
    }
    else {
        Write-Host "[ok] PawnIO $version installed"
    }
}

# The hub's sensor service opens LHM once at startup, so it must restart to pick up the driver.
if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Restart-Service $ServiceName -Force
    Write-Host "[ok] service '$ServiceName' restarted"
}
else {
    Write-Host "[skip] service '$ServiceName' not installed - start the hub to pick up PawnIO"
}
