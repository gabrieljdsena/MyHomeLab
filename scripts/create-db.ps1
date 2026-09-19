# Creates the MyHomeLab Postgres database if it does not exist.
# Uses the existing WB env vars (DB_HOST/DB_PORT/DB_USER/DB_PASS) when present.
param(
    [string]$DbUser = $(if ($env:DB_USER) { $env:DB_USER } else { 'postgres' }),
    [string]$DbPassword = $(if ($env:DB_PASS) { $env:DB_PASS } else { '' }),
    [string]$DbName = 'myhomelab'
)

$ErrorActionPreference = 'Stop'

$psql = Get-ChildItem "$env:ProgramFiles\PostgreSQL" -Recurse -Filter psql.exe -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1

if (-not $psql) {
    throw "psql.exe not found under $env:ProgramFiles\PostgreSQL"
}

$env:PGPASSWORD = $DbPassword
if ([string]::IsNullOrWhiteSpace($env:DB_HOST)) { $hostArg = 'localhost' } else { $hostArg = $env:DB_HOST }
if ([string]::IsNullOrWhiteSpace($env:DB_PORT)) { $portArg = '5432' } else { $portArg = $env:DB_PORT }

$exists = & $psql.FullName -h $hostArg -U $DbUser -p $portArg -tAc "SELECT 1 FROM pg_database WHERE datname='$DbName'"

if ($exists -eq '1') {
    Write-Host "[ok] database '$DbName' already exists"
} else {
    & $psql.FullName -h $hostArg -U $DbUser -p $portArg -c "CREATE DATABASE $DbName"
    Write-Host "[ok] database '$DbName' created"
}

Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue