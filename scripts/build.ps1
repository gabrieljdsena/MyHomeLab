# Production build: frontend -> API wwwroot -> staged publish -> swap into the live folder.
# The running service locks files in artifacts\publish, so we publish to a staging folder
# first and only stop/swap/start around the final rename.
param([switch]$NoRestart)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'
$stagingDir = Join-Path $root 'artifacts\publish-staging'
$oldDir = Join-Path $root 'artifacts\publish-old'
$serviceName = 'MyHomeLab'

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
$wasRunning = $null -ne $service -and $service.Status -eq 'Running'
if ($wasRunning) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        throw "Service '$serviceName' is running and stopping it needs an elevated PowerShell - re-run this script as Administrator."
    }
}

Write-Host "[build] frontend (npm run build)"
Push-Location "$root\frontend"
& "$root\frontend\node_modules\.bin\oxlint.cmd" 2>$null
npm run build
if ($LASTEXITCODE -ne 0) { throw "frontend build failed (exit $LASTEXITCODE)" }
Pop-Location

Write-Host "[build] refreshing API wwwroot (drop stale hashed assets)"
Remove-Item "$root\src\MyHomeLab.Api\wwwroot\*" -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem "$root\src\MyHomeLab.Api\obj" -Recurse -Directory -Filter "compressed" -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force }

Write-Host "[build] backend publish -> $stagingDir"
Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$root\src\MyHomeLab.Api\MyHomeLab.Api.csproj" -c Release -o $stagingDir
if ($LASTEXITCODE -ne 0) { throw "backend publish failed (exit $LASTEXITCODE)" }

if ($wasRunning) {
    Write-Host "[build] stopping service $serviceName for the swap"
    Stop-Service -Name $serviceName -Force
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(90))
}

Write-Host "[build] swapping $stagingDir -> $publishDir"
try {
    Remove-Item $oldDir -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path $publishDir) { Move-Item $publishDir $oldDir }
    Move-Item $stagingDir $publishDir
}
catch {
    if ((Test-Path $oldDir) -and -not (Test-Path $publishDir)) { Move-Item $oldDir $publishDir }
    if ($wasRunning -and -not $NoRestart) { Start-Service -Name $serviceName -ErrorAction SilentlyContinue }
    throw
}
Remove-Item $oldDir -Recurse -Force -ErrorAction SilentlyContinue

if ($wasRunning -and -not $NoRestart) {
    Write-Host "[build] starting service $serviceName"
    Start-Service -Name $serviceName
}

Write-Host "[build] done."
Write-Host "  Run: dotnet $publishDir\MyHomeLab.Api.dll"
Write-Host "  (set ASPNETCORE_Kestrel__Certificates__Default__Path/Password when serving :443)"
