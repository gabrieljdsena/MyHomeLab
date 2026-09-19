#Requires -RunAsAdministrator
<#
  enable-wol.ps1 — MyHomeLab Wake-on-LAN enable without BIOS

  Enables OS-side WoL so a magic packet can power on the host after
  Turn off (shutdown /s /t 0). BIOS must still have WoL enabled — if not,
  reboot to firmware without pressing DEL: shutdown /r /fw /t 0

  What this does:
  1. Disables Fast Startup (powercfg /h off + HiberbootEnabled=0) — required, blocks WoL
  2. For each physical NIC: Set-NetAdapterPowerManagement -WakeOnMagicPacket Enabled
  3. Prints MACs + WoL status for use in MyHomeLab Manage -> MAC address

  Run from an elevated PowerShell or via MyHomeLab Terminal (as admin) or via
  POST /api/wol/host/enable (service runs as LocalSystem).
#>

$ErrorActionPreference = "Continue"

function Assert-Admin {
  $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  if (-not $isAdmin) { Write-Warning "Not running as Administrator — powercfg/NetAdapter changes will fail. Right-click -> Run as Administrator." }
}

Assert-Admin

Write-Host "== MyHomeLab enable-wol ==" -ForegroundColor Cyan

Write-Host "`n[1] Disabling Fast Startup (powercfg /h off)..." -ForegroundColor Yellow
try {
  powercfg /h off
  Write-Host "powercfg exit: $LASTEXITCODE"
} catch { Write-Warning $_ }

Write-Host "[1b] Setting HiberbootEnabled=0..." -ForegroundColor Yellow
try {
  reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power" /v HiberbootEnabled /t REG_DWORD /d 0 /f | Out-Null
  Write-Host "registry set"
} catch { Write-Warning $_ }

Write-Host "`n[2] Enabling Wake on Magic Packet per adapter..." -ForegroundColor Yellow
try {
  $adapters = Get-NetAdapter -Physical | Where-Object { $_.Virtual -eq $false }
  foreach ($a in $adapters) {
    Write-Host " -> $($a.Name) ($($a.MacAddress)) status $($a.Status)"
    try {
      $pm = Get-NetAdapterPowerManagement -Name $a.Name -ErrorAction SilentlyContinue
      if ($pm) {
        Set-NetAdapterPowerManagement -Name $a.Name -WakeOnMagicPacket Enabled -WakeOnPattern Enabled -ErrorAction Stop | Out-Null
        Write-Host "    enabled" -ForegroundColor Green
      } else {
        Write-Warning "    no PowerManagement data"
      }
    } catch {
      Write-Warning "    failed: $($_.Exception.Message)"
      Write-Host "    Trying Advanced registry fallback: WakeOnMagicPacket..."
      try {
        Set-NetAdapterAdvancedProperty -Name $a.Name -RegistryKeyword "WakeOnMagicPacket" -RegistryValue 1 -ErrorAction SilentlyContinue | Out-Null
        Set-NetAdapterAdvancedProperty -Name $a.Name -RegistryKeyword "*WakeOnMagicPacket" -RegistryValue 1 -ErrorAction SilentlyContinue | Out-Null
      } catch {}
    }
  }
} catch { Write-Warning $_ }

Write-Host "`n[3] Current WoL status:" -ForegroundColor Yellow
try {
  Get-NetAdapterPowerManagement | Select-Object Name, WakeOnMagicPacket, WakeOnPattern, DeviceSleepOnDisconnect | Format-Table -AutoSize | Out-String | Write-Host
} catch { Write-Warning $_ }

Write-Host "`n[4] MAC addresses (add to MyHomeLab Manage -> MAC):" -ForegroundColor Yellow
Get-NetAdapter -Physical | Where-Object { $_.MacAddress } | ForEach-Object {
  $mac = $_.MacAddress
  $ip = (Get-NetIPAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1).IPAddress
  Write-Host "  $($_.Name): $mac $(if ($ip) { "IP $ip" })"
}

Write-Host "`n[5] Broadcast for this host (for another device to wake it): 255.255.255.255:9 (or 192.168.15.255 if router blocks)" -ForegroundColor Yellow
Write-Host "Test from phone/PC on same LAN: wol $((Get-NetAdapter -Physical | Where MacAddress | Select -First 1).MacAddress)  or via MyHomeLab Dashboard SystemPanel -> Send magic packet"
Write-Host "`nIf it still doesn't wake, BIOS WoL is disabled — reboot to firmware:`n  shutdown /r /fw /t 0`nthen enable: Advanced -> APM -> Power On By PCI-E / WoL -> Enabled, ErP -> Disabled" -ForegroundColor Cyan
Write-Host "`nDone. Now Turn off (POST /api/system/power) then test WoL while host is off." -ForegroundColor Green
