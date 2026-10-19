<#
.SYNOPSIS
  Checks that every prerequisite is installed and that Docker is running.
  Run it in a NEW terminal after setup-windows.ps1 (and after a restart, if one was requested).
#>
$allOk = $true

# Reload PATH from the registry: a terminal opened before the installation still has the old PATH
# and would report freshly installed tools as missing.
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [Environment]::GetEnvironmentVariable('Path', 'User')

function Check([string]$Name, [string]$Exe, [string[]]$Arguments, [bool]$Required = $true) {
    $found = $false
    $out = ''
    if (Get-Command $Exe -ErrorAction SilentlyContinue) {
        $out = (& $Exe @Arguments 2>$null | Select-Object -First 1)
        $found = ($LASTEXITCODE -eq 0)
    }
    if ($found) {
        Write-Host ("[OK]      {0,-16} {1}" -f $Name, $out) -ForegroundColor Green
    } elseif ($Required) {
        Write-Host ("[MISSING] {0,-16} (required)" -f $Name) -ForegroundColor Red
        $script:allOk = $false
    } else {
        Write-Host ("[--]      {0,-16} (optional - not found)" -f $Name) -ForegroundColor DarkYellow
    }
}

Check 'Git'            'git'    @('--version')
Check 'Docker'         'docker' @('--version')
Check 'Docker Compose' 'docker' @('compose', 'version')
Check 'Docker engine'  'docker' @('info', '--format', 'Server {{.ServerVersion}} ({{.OperatingSystem}})')
Check '.NET SDK'       'dotnet' @('--version') $false
Check 'Node.js'        'node'   @('--version') $false
Check 'npm'            'npm'    @('--version') $false

Write-Host ""
if ($allOk) {
    Write-Host "Ready. You can now clone the project and run: docker compose up -d --build" -ForegroundColor Green
} else {
    Write-Host "Something is missing - see the troubleshooting section of docs/INSTALL.he.md" -ForegroundColor Red
    exit 1
}
