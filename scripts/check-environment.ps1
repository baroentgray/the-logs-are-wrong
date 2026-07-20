#requires -Version 7.0
$ErrorActionPreference = "Stop"

function Test-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Write-Check {
    param(
        [string]$Name,
        [bool]$Ok,
        [string]$Details,
        [bool]$RequiredForGate1 = $false
    )

    $status = if ($Ok) { "OK" } else { if ($RequiredForGate1) { "MISSING" } else { "NOT INSTALLED" } }
    $required = if ($RequiredForGate1) { "Gate 1 required" } else { "Later/optional" }
    Write-Host ("[{0}] {1} — {2} — {3}" -f $status, $Name, $required, $Details)
}

$gate1Ok = $true

# Git
$gitOk = Test-Command "git"
$gitDetails = if ($gitOk) { (git --version) -join " " } else { "Install Git for Windows" }
Write-Check "Git" $gitOk $gitDetails $true
$gate1Ok = $gate1Ok -and $gitOk

# Git LFS
$lfsOk = $false
$lfsDetails = "Run: git lfs install"
if ($gitOk) {
    try {
        $lfsDetails = (git lfs version 2>$null) -join " "
        $lfsOk = $LASTEXITCODE -eq 0
    } catch {
        $lfsOk = $false
    }
}
Write-Check "Git LFS" $lfsOk $lfsDetails $true
$gate1Ok = $gate1Ok -and $lfsOk

# .NET 10 SDK
$dotnetOk = Test-Command "dotnet"
$dotnet10Ok = $false
$dotnetDetails = "Install .NET 10 SDK x64"
if ($dotnetOk) {
    $sdks = @(dotnet --list-sdks)
    $dotnet10Ok = [bool]($sdks | Where-Object { $_ -match '^10\.' })
    $dotnetDetails = if ($dotnet10Ok) {
        "Installed SDKs: " + (($sdks | Where-Object { $_ -match '^10\.' }) -join ", ")
    } else {
        "dotnet found, but no .NET 10 SDK. Installed: " + ($sdks -join ", ")
    }
}
Write-Check ".NET 10 SDK" $dotnet10Ok $dotnetDetails $true
$gate1Ok = $gate1Ok -and $dotnet10Ok

# PowerShell
$pwshOk = $PSVersionTable.PSVersion.Major -ge 7
$pwshDetails = "PowerShell $($PSVersionTable.PSVersion)"
Write-Check "PowerShell 7" $pwshOk $pwshDetails $true
$gate1Ok = $gate1Ok -and $pwshOk

# Optional/later tools
$unityHubPaths = @(
    "$env:ProgramFiles\Unity Hub\Unity Hub.exe",
    "$env:LOCALAPPDATA\Programs\Unity Hub\Unity Hub.exe"
)
$unityHubOk = [bool]($unityHubPaths | Where-Object { Test-Path $_ })
Write-Check "Unity Hub" $unityHubOk "Required at Gate 2" $false

$blenderOk = Test-Command "blender"
$blenderDetails = if ($blenderOk) { (blender --version | Select-Object -First 1) } else { "Required before original 3D asset work" }
Write-Check "Blender" $blenderOk $blenderDetails $false

$steamPaths = @(
    "${env:ProgramFiles(x86)}\Steam\steam.exe",
    "$env:ProgramFiles\Steam\steam.exe"
)
$steamOk = [bool]($steamPaths | Where-Object { Test-Path $_ })
Write-Check "Steam Client" $steamOk "Required at Gate 3" $false

Write-Host ""
if ($gate1Ok) {
    Write-Host "Gate 1 environment: READY" -ForegroundColor Green
    exit 0
}

Write-Host "Gate 1 environment: NOT READY" -ForegroundColor Red
exit 1
