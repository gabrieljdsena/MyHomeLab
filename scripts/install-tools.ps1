# Ensures the machine-wide toolchain MyHomeLab depends on at runtime.
# The API service runs as SYSTEM, so user-level installs (scoop shims,
# WindowsApps aliases, user PATH) are invisible to it. Everything here is
# installed machine-wide (winget --scope machine or portable dirs) and wired
# into the MACHINE PATH, which services read at startup.
#
# Run from an elevated PowerShell. Safe to re-run (idempotent).
#   .\scripts\install-tools.ps1           # install missing tools + wire PATH
#   .\scripts\install-tools.ps1 -Check    # report only, exit 1 if anything missing
param(
    [switch]$Check,
    [string[]]$Tools = @('node', 'python', 'git', '7zip', 'ffmpeg', 'opencode', 'curl'),
    [string]$ToolsRoot = 'C:\Tools\MyHomeLab'
)

$ErrorActionPreference = 'Stop'

# Pinned versions - bump deliberately, then re-run this script + restart the service.
$Versions = @{
    Node     = '22.23.3'
    Python   = '3.13.15'
    Git      = '2.55.0.5'
    SevenZip = '26.04'
    FFmpeg   = '9.0.2'
    Opencode = '1.18.35'
}

$WingetIds = @{
    Node     = 'OpenJS.NodeJS.LTS'
    Python   = 'Python.Python.3.13'
    Git      = 'Git.Git'
    SevenZip = '7zip.7zip'
    FFmpeg   = 'Gyan.FFmpeg'
}

function Test-IsAdmin {
    $p = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-MachinePathEntries {
    return @([Environment]::GetEnvironmentVariable('Path', 'Machine') -split ';' |
        ForEach-Object { $_.Trim().TrimEnd('\') } | Where-Object { $_ })
}

function Add-MachinePathEntry([string]$dir) {
    $entries = Get-MachinePathEntries
    $norm = $dir.TrimEnd('\')
    if ($entries -icontains $norm) {
        return $false
    }

    $newPath = (($entries + @($norm)) -join ';')
    [Environment]::SetEnvironmentVariable('Path', $newPath, 'Machine')
    if (($env:Path -split ';' | ForEach-Object { $_.Trim().TrimEnd('\') }) -notcontains $norm) {
        $env:Path = "$env:Path;$norm"
    }

    return $true
}

function Send-PathBroadcast {
    $sig = '[DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r);'
    try {
        $t = Add-Type -MemberDefinition $sig -Name EnvBroadcast -Namespace MyHomeLabTools -PassThru
        [UIntPtr]$r = [UIntPtr]::Zero
        $t::SendMessageTimeout([IntPtr]0xffff, 0x1a, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$r) | Out-Null
    }
    catch {
        # Best effort; services read machine env at startup regardless.
    }
}

function Find-ToolCommand([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue |
        Where-Object { $_.Source -notmatch 'WindowsApps' } |
        Select-Object -First 1
    return $cmd
}

function Install-WingetTool([string]$label, [string]$wingetId, [string]$version) {
    Write-Host "  [install] $label via winget ($wingetId $version)..."
    $args = @('install', '--id', $wingetId, '--version', $version, '--scope', 'machine',
        '--silent', '--accept-package-agreements', '--accept-source-agreements')
    $proc = Start-Process -FilePath 'winget.exe' -ArgumentList $args -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne -1978335035) {
        throw "$label winget install failed (exit $($proc.ExitCode))"
    }
}

function Install-Opencode([string]$version, [string]$toolsRoot) {
    $dir = Join-Path $toolsRoot 'opencode'
    $exe = Join-Path $dir 'opencode.exe'
    if ((Test-Path $exe) -and ((& $exe --version 2>$null) -match [regex]::Escape($version))) {
        Write-Host "  [ok] opencode $version already present"
        return $dir
    }

    Write-Host "  [install] opencode $version..."
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $url = "https://github.com/anomalyco/opencode/releases/download/v$version/opencode-windows-x64.zip"
    $zip = Join-Path ([IO.Path]::GetTempPath()) "opencode-$version.zip"
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $dir -Force
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    $found = Get-ChildItem -Path $dir -Recurse -Filter 'opencode.exe' | Select-Object -First 1
    if (-not $found) {
        throw "opencode.exe not found in downloaded archive"
    }

    if ($found.DirectoryName -ne $dir) {
        Move-Item (Join-Path $found.DirectoryName 'opencode.exe') $exe -Force
    }

    $reported = (& $exe --version 2>$null) -join ' '
    if ($reported -notmatch [regex]::Escape($version)) {
        throw "opencode version mismatch (expected $version, got: $reported)"
    }

    return $dir
}

if (-not $Check -and -not (Test-IsAdmin)) {
    throw "Run from an elevated PowerShell (machine installs + machine PATH require admin)."
}

$results = @()
$pathTouched = $false

foreach ($tool in $Tools) {
    switch ($tool.ToLowerInvariant()) {
        'node' {
            $cmd = Find-ToolCommand 'node.exe'
            if ($cmd) {
                $ver = (& $cmd.Source --version 2>$null) -join ' '
                $results += [pscustomobject]@{ Tool = 'node'; State = "present ($ver)"; Path = $cmd.Source }
            }
            elseif ($Check) {
                $results += [pscustomobject]@{ Tool = 'node'; State = 'MISSING'; Path = '' }
            }
            else {
                Install-WingetTool 'Node.js' $WingetIds.Node $Versions.Node
                $results += [pscustomobject]@{ Tool = 'node'; State = "installed ($($Versions.Node))"; Path = '' }
            }
        }
        'python' {
            $cmd = Find-ToolCommand 'python.exe'
            if (-not $cmd) { $cmd = Find-ToolCommand 'python3.exe' }
            if ($cmd) {
                $ver = (& $cmd.Source --version 2>&1) -join ' '
                $results += [pscustomobject]@{ Tool = 'python'; State = "present ($ver)"; Path = $cmd.Source }
            }
            elseif ($Check) {
                $results += [pscustomobject]@{ Tool = 'python'; State = 'MISSING'; Path = '' }
            }
            else {
                Install-WingetTool 'Python' $WingetIds.Python $Versions.Python
                $results += [pscustomobject]@{ Tool = 'python'; State = "installed ($($Versions.Python))"; Path = '' }
            }
        }
        'git' {
            $cmd = Find-ToolCommand 'git.exe'
            if ($cmd) {
                $ver = (& $cmd.Source --version 2>$null) -join ' '
                $results += [pscustomobject]@{ Tool = 'git'; State = "present ($ver)"; Path = $cmd.Source }
            }
            elseif ($Check) {
                $results += [pscustomobject]@{ Tool = 'git'; State = 'MISSING'; Path = '' }
            }
            else {
                Install-WingetTool 'Git' $WingetIds.Git $Versions.Git
                $results += [pscustomobject]@{ Tool = 'git'; State = "installed ($($Versions.Git))"; Path = '' }
            }
        }
        '7zip' {
            $cmd = Find-ToolCommand '7z.exe'
            if ($cmd -and $cmd.Source -notmatch 'scoop\\shims') {
                $results += [pscustomobject]@{ Tool = '7zip'; State = 'present'; Path = $cmd.Source }
            }
            elseif ($Check) {
                $results += [pscustomobject]@{ Tool = '7zip'; State = 'MISSING (scoop shims are user-level; need machine install)'; Path = '' }
            }
            else {
                Install-WingetTool '7-Zip' $WingetIds.SevenZip $Versions.SevenZip
                $results += [pscustomobject]@{ Tool = '7zip'; State = "installed ($($Versions.SevenZip))"; Path = '' }
            }
        }
        'ffmpeg' {
            $cmd = Find-ToolCommand 'ffmpeg.exe'
            if ($cmd -and $cmd.Source -notmatch 'scoop\\shims') {
                $ver = (& $cmd.Source -version 2>$null | Select-Object -First 1) -join ' '
                $results += [pscustomobject]@{ Tool = 'ffmpeg'; State = "present ($ver)"; Path = $cmd.Source }
            }
            elseif ($Check) {
                $results += [pscustomobject]@{ Tool = 'ffmpeg'; State = 'MISSING (scoop shims are user-level; need machine install)'; Path = '' }
            }
            else {
                Install-WingetTool 'FFmpeg' $WingetIds.FFmpeg $Versions.FFmpeg
                $results += [pscustomobject]@{ Tool = 'ffmpeg'; State = "installed ($($Versions.FFmpeg))"; Path = '' }
            }
        }
        'opencode' {
            $cmd = Find-ToolCommand 'opencode.exe'
            $exe = if ($cmd) { $cmd.Source } else { Join-Path $ToolsRoot 'opencode\opencode.exe' }
            if ((Test-Path $exe) -and ((& $exe --version 2>$null) -match [regex]::Escape($Versions.Opencode))) {
                $results += [pscustomobject]@{ Tool = 'opencode'; State = "present ($($Versions.Opencode))"; Path = $exe }
            }
            elseif ($Check) {
                $results += [pscustomobject]@{ Tool = 'opencode'; State = 'MISSING'; Path = '' }
            }
            else {
                $dir = Install-Opencode $Versions.Opencode $ToolsRoot
                if (Add-MachinePathEntry $dir) { $pathTouched = $true }
                $results += [pscustomobject]@{ Tool = 'opencode'; State = "installed ($($Versions.Opencode))"; Path = $exe }
            }
        }
        'curl' {
            $cmd = Find-ToolCommand 'curl.exe'
            $results += [pscustomobject]@{ Tool = 'curl'; State = if ($cmd) { "present ($($cmd.Source))" } else { 'MISSING (expected inbox)' }; Path = if ($cmd) { $cmd.Source } else { '' } }
        }
        default { throw "Unknown tool '$tool'. Valid: node, python, git, 7zip, ffmpeg, opencode, curl." }
    }
}

if ($pathTouched -and -not $Check) {
    Send-PathBroadcast
    Write-Host '[ok] machine PATH updated (services pick it up on restart)'
}

$results | Format-Table -AutoSize | Out-String | Write-Output
if ($Check -and ($results | Where-Object { $_.State -match '^MISSING' })) {
    Write-Host '[check] toolchain incomplete'
    exit 1
}

Write-Host '[ok] toolchain ready'
