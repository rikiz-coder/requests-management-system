#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installs the prerequisites for running and checking the Requests Management project on Windows.

.DESCRIPTION
  Required (to run the system with Docker Compose):
    - WSL 2 (needed by Docker Desktop)
    - Git
    - Docker Desktop
  Development tools (to build and run the tests outside Docker) - installed unless -RuntimeOnly:
    - .NET 10 SDK
    - Node.js LTS (Angular)
  Optional (-WithTools):
    - Visual Studio Code
    - SQL Server Management Studio (to view Execution Plans)

  Safe to run more than once: anything already installed is skipped.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\setup-windows.ps1
  powershell -ExecutionPolicy Bypass -File .\scripts\setup-windows.ps1 -WithTools
#>
param(
    [switch]$RuntimeOnly,
    [switch]$WithTools
)

# 'Continue' on purpose: in Windows PowerShell 5.1, 'Stop' turns stderr output of native tools (winget, wsl) into terminating errors.
$ErrorActionPreference = 'Continue'
$script:NeedsRestart = $false
$script:Failures = @()

function Write-Step($text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

function Test-WingetPackage([string]$Id) {
    $null = winget list --id $Id --exact --source winget --accept-source-agreements 2>$null
    return ($LASTEXITCODE -eq 0)
}

function Install-WingetPackage([string]$Id, [string]$Name) {
    if (Test-WingetPackage $Id) {
        Write-Host "[OK]   $Name is already installed" -ForegroundColor Green
        return
    }
    Write-Host "[...]  Installing $Name ($Id)"
    # --source winget: skip the Microsoft Store source, which fails on some networks (certificate errors)
    winget install --id $Id --exact --source winget --silent --accept-package-agreements --accept-source-agreements
    # -1978335189 = already installed / no applicable upgrade
    if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq -1978335189) {
        Write-Host "[OK]   $Name installed" -ForegroundColor Green
    } else {
        Write-Warning "$Name failed to install (exit code $LASTEXITCODE)"
        $script:Failures += $Name
    }
}

# --- 1. Pre-checks -----------------------------------------------------------
Write-Step "Checking system"

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Host "winget was not found. Install 'App Installer' from the Microsoft Store and run this script again." -ForegroundColor Red
    exit 1
}

$os = Get-CimInstance Win32_OperatingSystem
Write-Host "Windows: $($os.Caption) build $($os.BuildNumber)"
if ([int]$os.BuildNumber -lt 19045) {
    Write-Warning "Docker Desktop needs Windows 10 22H2 (build 19045) or Windows 11."
}

$ramGb = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
Write-Host "RAM: $ramGb GB"
if ($ramGb -lt 8) { Write-Warning "Less than 8 GB RAM - SQL Server in Docker may be slow." }

# When a hypervisor is already running, Win32_Processor reports virtualization as disabled even though it is on,
# so only warn when no hypervisor is present.
$hypervisor = (Get-CimInstance Win32_ComputerSystem).HypervisorPresent
$virt = (Get-CimInstance Win32_Processor | Select-Object -First 1).VirtualizationFirmwareEnabled
if (-not $hypervisor -and $virt -eq $false) {
    Write-Warning "Virtualization is disabled in BIOS/UEFI. Docker Desktop will not start until it is enabled (Intel VT-x / AMD-V). After a restart, check Task Manager > Performance > CPU > Virtualization."
}

# --- 2. WSL 2 ----------------------------------------------------------------
Write-Step "WSL 2"
$wslOk = $false
try {
    $null = wsl.exe --status 2>$null
    $wslOk = ($LASTEXITCODE -eq 0)
} catch { $wslOk = $false }

if ($wslOk) {
    Write-Host "[OK]   WSL is installed - updating"
    wsl.exe --update
} else {
    Write-Host "[...]  Installing WSL (no Linux distribution is needed)"
    wsl.exe --install --no-distribution
    $script:NeedsRestart = $true
}

# --- 3. Packages -------------------------------------------------------------
Write-Step "Required tools"
Install-WingetPackage 'Git.Git'              'Git'
Install-WingetPackage 'Docker.DockerDesktop' 'Docker Desktop'

if (-not $RuntimeOnly) {
    Write-Step "Development tools"
    Install-WingetPackage 'Microsoft.DotNet.SDK.10' '.NET 10 SDK'
    Install-WingetPackage 'OpenJS.NodeJS.LTS'       'Node.js LTS'
}

if ($WithTools) {
    Write-Step "Optional tools"
    Install-WingetPackage 'Microsoft.VisualStudioCode'          'Visual Studio Code'
    Install-WingetPackage 'Microsoft.SQLServerManagementStudio' 'SQL Server Management Studio'
}

# Let Docker Desktop's group membership take effect for the current user
try {
    $user = "$env:USERDOMAIN\$env:USERNAME"
    if (Get-LocalGroup -Name 'docker-users' -ErrorAction SilentlyContinue) {
        $isMember = Get-LocalGroupMember -Group 'docker-users' -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq $user }
        if (-not $isMember) {
            Add-LocalGroupMember -Group 'docker-users' -Member $user -ErrorAction SilentlyContinue
            $script:NeedsRestart = $true
        }
    }
} catch { }

# --- 4. Summary --------------------------------------------------------------
Write-Step "Summary"
if ($script:Failures.Count -gt 0) {
    Write-Warning ("Failed: " + ($script:Failures -join ', ') + ". See the troubleshooting section of docs/INSTALL.he.md.")
} else {
    Write-Host "All packages were installed." -ForegroundColor Green
}

if ($script:NeedsRestart) {
    Write-Host "`nRESTART the computer, then open Docker Desktop once and wait until it shows 'Engine running'." -ForegroundColor Yellow
} else {
    Write-Host "`nOpen Docker Desktop and wait until it shows 'Engine running'." -ForegroundColor Yellow
}
Write-Host "Then, in a NEW terminal, run:  powershell -ExecutionPolicy Bypass -File .\scripts\verify-windows.ps1"
