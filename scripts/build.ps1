# Production build: frontend -> API wwwroot -> single publish output folder.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'

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

Write-Host "[build] backend publish -> $publishDir"
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$root\src\MyHomeLab.Api\MyHomeLab.Api.csproj" -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "backend publish failed (exit $LASTEXITCODE)" }

Write-Host "[build] done."
Write-Host "  Run: dotnet $publishDir\MyHomeLab.Api.dll"
Write-Host "  (set ASPNETCORE_Kestrel__Certificates__Default__Path/Password when serving :443)"