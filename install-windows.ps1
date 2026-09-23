#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$RepositoryUrl = "https://github.com/treramey/dots-windows.git",
    [string]$RepositoryPath,
    [string]$PackageManifestPath,
    [string]$WslDistribution = "Ubuntu",
    [switch]$SkipRepositoryUpdate,
    [switch]$SkipWslCheck,
    [switch]$SkipContainerInstall,
    [switch]$SkipFontInstall,
    [switch]$Plan
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not $RepositoryPath) {
    $RepositoryPath = Join-Path $HOME ".dotfiles"
}
if (-not $PackageManifestPath) {
    $PackageManifestPath = Join-Path $PSScriptRoot "home\.chezmoidata\packages.json"
}

function Write-WindowsBootstrapStep {
    param([Parameter(Mandatory)][string]$Message)

    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-WindowsBootstrapCommand {
    param(
        [Parameter(Mandatory)][string]$Command,
        [Parameter()][string[]]$Arguments = @()
    )

    Write-Host "    $Command $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Windows bootstrap command failed: $Command exited with code $LASTEXITCODE"
    }
}

function Get-WindowsBootstrapPackageManifest {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Windows bootstrap package manifest missing: $Path"
    }

    $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if (-not $manifest.packages -or -not $manifest.packages.windows) {
        throw "Windows bootstrap package manifest invalid: packages.windows is missing from $Path"
    }
    return $manifest.packages.windows
}

function Add-WindowsBootstrapScoopPath {
    $scoopRoot = if ($env:SCOOP) { $env:SCOOP } else { Join-Path $HOME "scoop" }
    $scoopShims = Join-Path $scoopRoot "shims"
    if ((Test-Path $scoopShims) -and (($env:Path -split ";") -notcontains $scoopShims)) {
        $env:Path = "$scoopShims;$env:Path"
    }
}

function Install-WindowsBootstrapScoop {
    if (Get-Command scoop -ErrorAction SilentlyContinue) {
        Write-Host "    Scoop is already installed."
        return
    }

    Write-WindowsBootstrapStep "Installing Scoop"
    try {
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
    }
    catch {
        $effectiveExecutionPolicy = Get-ExecutionPolicy
        if ($effectiveExecutionPolicy -notin @("Bypass", "RemoteSigned", "Unrestricted")) {
            throw
        }
        Write-Host "    Keeping effective execution policy '$effectiveExecutionPolicy'."
    }

    $scoopInstaller = Invoke-RestMethod -Uri "https://get.scoop.sh"
    $scoopInstallerBlock = [scriptblock]::Create($scoopInstaller)
    $windowsIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $windowsPrincipal = [Security.Principal.WindowsPrincipal]::new($windowsIdentity)
    $isAdministrator = $windowsPrincipal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )
    if ($isAdministrator) {
        & $scoopInstallerBlock -RunAsAdmin
    }
    else {
        & $scoopInstallerBlock
    }
    Add-WindowsBootstrapScoopPath

    if (-not (Get-Command scoop -ErrorAction SilentlyContinue)) {
        throw "Windows bootstrap Scoop installation failed: scoop is not available in PATH"
    }
}

function Enable-WindowsBootstrapScoopBucket {
    param(
        [Parameter(Mandatory)][string]$Bucket,
        [Parameter()][string]$Source
    )

    $scoopBuckets = (& scoop bucket list | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "Windows bootstrap Scoop bucket check failed: scoop bucket list exited with code $LASTEXITCODE"
    }
    if ($scoopBuckets -match "(?m)^\s*$([regex]::Escape($Bucket))\s") {
        return
    }

    Write-WindowsBootstrapStep "Enabling Scoop $Bucket bucket"
    $arguments = @("bucket", "add", $Bucket)
    if ($Source) {
        $arguments += $Source
    }
    Invoke-WindowsBootstrapCommand -Command "scoop" -Arguments $arguments
}

function Install-WindowsBootstrapScoopPackages {
    param(
        [Parameter(Mandatory)][string]$Group,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Packages
    )

    if ($Packages.Count -eq 0) {
        return
    }

    Write-WindowsBootstrapStep "Installing $Group"
    foreach ($package in $Packages) {
        Invoke-WindowsBootstrapCommand -Command "scoop" -Arguments @("install", $package)
    }
    Add-WindowsBootstrapScoopPath
}

function Install-WindowsBootstrapWingetPackages {
    param([Parameter(Mandatory)][AllowNull()][AllowEmptyCollection()][string[]]$Packages)

    if (@($Packages).Count -eq 0) {
        return
    }
    if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) {
        throw "Windows bootstrap winget unavailable: install or update App Installer from Microsoft Store"
    }

    Write-WindowsBootstrapStep "Installing Windows host applications with winget"
    foreach ($package in $Packages) {
        & winget.exe list `
            --id $package `
            --exact `
            --accept-source-agreements `
            --disable-interactivity *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    $package is already installed."
            continue
        }

        Invoke-WindowsBootstrapCommand -Command "winget.exe" -Arguments @(
            "install",
            "--id", $package,
            "--exact",
            "--accept-package-agreements",
            "--accept-source-agreements",
            "--disable-interactivity"
        )
    }
}

function Initialize-WindowsBootstrapChezmoi {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][bool]$SkipUpdate
    )

    $sourceAlreadyInitialized = Test-Path -LiteralPath (Join-Path $Path ".git")

    Write-WindowsBootstrapStep "Initializing Windows dotfiles with chezmoi"
    Invoke-WindowsBootstrapCommand -Command "chezmoi" -Arguments @(
        "--source", $Path,
        "init",
        "--no-tty",
        $Url
    )

    $chezmoiSourcePath = (& chezmoi --source $Path source-path).Trim()
    if (-not (Test-Path -LiteralPath (Join-Path $chezmoiSourcePath ".chezmoi.toml.tmpl"))) {
        throw "Windows bootstrap chezmoi initialization failed: source missing at $chezmoiSourcePath"
    }

    $chezmoiOperation = if ($sourceAlreadyInitialized -and -not $SkipUpdate) { "update" } else { "apply" }
    Write-WindowsBootstrapStep "$($chezmoiOperation.Substring(0, 1).ToUpper())$($chezmoiOperation.Substring(1)) Windows dotfiles"
    Invoke-WindowsBootstrapCommand -Command "chezmoi" -Arguments @(
        "--source", $chezmoiSourcePath,
        $chezmoiOperation,
        "--no-tty"
    )
}

function Test-WindowsBootstrapBitwardenSshAgent {
    Write-WindowsBootstrapStep "Checking Bitwarden SSH agent"

    $windowsSshAgent = Get-Service -Name "ssh-agent" -ErrorAction SilentlyContinue
    if ($windowsSshAgent -and ($windowsSshAgent.Status -ne "Stopped" -or $windowsSshAgent.StartType -ne "Disabled")) {
        Write-Warning "Bitwarden SSH agent conflict: disable the Windows 'OpenSSH Authentication Agent' service."
    }

    if (-not (Test-Path -LiteralPath "\\.\pipe\openssh-ssh-agent")) {
        Write-Warning "Bitwarden SSH agent is not active: open Bitwarden Desktop, unlock the vault, and enable Settings > Enable SSH agent."
        return
    }

    Write-Host "    Bitwarden exposes the Windows OpenSSH agent pipe."
}

function Test-WindowsBootstrapWslEnvironment {
    param([Parameter(Mandatory)][string]$Distribution)

    Write-WindowsBootstrapStep "Checking WSL development environment"
    if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
        Write-Warning "Windows bootstrap WSL check failed: run an elevated 'wsl --install --distribution $Distribution', restart Windows, and initialize the distribution."
        return
    }

    & wsl.exe --status *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Windows bootstrap WSL check failed: WSL 2 is not ready. Run an elevated 'wsl --install --distribution $Distribution' and restart Windows."
        return
    }

    $installedDistributions = @(& wsl.exe --list --quiet) | ForEach-Object {
        ($_ -replace "`0", "").Trim()
    } | Where-Object { $_ }
    if ($installedDistributions -notcontains $Distribution) {
        Write-Warning "Windows bootstrap WSL distribution missing: install '$Distribution' with an elevated 'wsl --install --distribution $Distribution'."
        return
    }

    Invoke-WindowsBootstrapCommand -Command "wsl.exe" -Arguments @("--set-default", $Distribution)

    & wsl.exe --distribution $Distribution --exec sh -lc "command -v nvim >/dev/null 2>&1"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Windows bootstrap WSL Neovim check failed: run the Ubuntu bootstrap command documented in WINDOWS.md inside $Distribution before launching 'neovide --wsl'."
        return
    }

    Write-Host "    $Distribution is ready and provides Neovim."
}

function Get-WindowsBootstrapPlan {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][string[]]$ScoopMainPackages,
        [Parameter(Mandatory)][AllowNull()][AllowEmptyCollection()][string[]]$ScoopExtraPackages,
        [Parameter(Mandatory)][AllowNull()][AllowEmptyCollection()][string[]]$WingetPackages,
        [Parameter(Mandatory)][AllowNull()][AllowEmptyCollection()][string[]]$FontPackages,
        [Parameter(Mandatory)][string]$Distribution,
        [Parameter(Mandatory)][bool]$ShouldValidateWsl,
        [Parameter(Mandatory)][bool]$ShouldUpdateRepository
    )

    [ordered]@{
        platform = "windows"
        architecture = "thin-host-with-wsl-development"
        packageManifestPath = $ManifestPath
        scoopMainPackages = $ScoopMainPackages
        scoopExtraPackages = $ScoopExtraPackages
        wingetPackages = $WingetPackages
        scoopFontPackages = $FontPackages
        repositoryUrl = $Url
        repositoryPath = $Path
        repositoryManager = "chezmoi"
        updateRepository = $ShouldUpdateRepository
        wslDistribution = $Distribution
        validateWsl = $ShouldValidateWsl
        wslBootstrap = "~/.dotfiles/install-ubuntu.sh"
        neovideCommand = "neovide --wsl"
        sshAgent = "Bitwarden Desktop via npiperelay and socat"
    } | ConvertTo-Json -Depth 3
}

$packageManifest = Get-WindowsBootstrapPackageManifest -Path $PackageManifestPath
$scoopMainPackages = @($packageManifest.scoopMain)
$scoopExtraPackages = @($packageManifest.scoopExtras | Where-Object {
    -not $SkipContainerInstall -or $_ -ne "rancher-desktop"
})
$wingetPackages = @($packageManifest.winget)
# An `if` expression enumerates its output during assignment. Wrap the whole
# expression so a single font remains an array under Windows PowerShell 5.1.
$fontPackages = @(if ($SkipFontInstall) { @() } else { $packageManifest.scoopFonts })

if ($Plan) {
    Get-WindowsBootstrapPlan `
        -Url $RepositoryUrl `
        -Path $RepositoryPath `
        -ManifestPath $PackageManifestPath `
        -ScoopMainPackages $scoopMainPackages `
        -ScoopExtraPackages $scoopExtraPackages `
        -WingetPackages $wingetPackages `
        -FontPackages $fontPackages `
        -Distribution $WslDistribution `
        -ShouldValidateWsl (-not $SkipWslCheck) `
        -ShouldUpdateRepository (-not $SkipRepositoryUpdate)
    return
}

$windowsBootstrapIsWindows = if (Get-Variable IsWindows -ErrorAction SilentlyContinue) {
    $IsWindows
}
else {
    $env:OS -eq "Windows_NT"
}
if (-not $windowsBootstrapIsWindows) {
    throw "Windows bootstrap platform mismatch: install-windows.ps1 must run on Windows"
}

Write-WindowsBootstrapStep "Preparing thin Windows host"
Install-WindowsBootstrapScoop

$remainingScoopMainPackages = @($scoopMainPackages)
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Install-WindowsBootstrapScoopPackages -Group "Scoop prerequisite" -Packages @("git")
    $remainingScoopMainPackages = @($remainingScoopMainPackages | Where-Object { $_ -ne "git" })
}
Install-WindowsBootstrapScoopPackages -Group "Windows host tools" -Packages $remainingScoopMainPackages

if ($scoopExtraPackages.Count -gt 0) {
    Enable-WindowsBootstrapScoopBucket -Bucket "extras"
    Install-WindowsBootstrapScoopPackages -Group "Windows host applications" -Packages $scoopExtraPackages
}

Install-WindowsBootstrapWingetPackages -Packages $wingetPackages

if ($fontPackages.Count -gt 0) {
    Enable-WindowsBootstrapScoopBucket `
        -Bucket "nerd-fonts" `
        -Source "https://github.com/matthewjberger/scoop-nerd-fonts"
    Install-WindowsBootstrapScoopPackages -Group "terminal fonts" -Packages $fontPackages
}

Initialize-WindowsBootstrapChezmoi `
    -Url $RepositoryUrl `
    -Path $RepositoryPath `
    -SkipUpdate $SkipRepositoryUpdate

Test-WindowsBootstrapBitwardenSshAgent
if (-not $SkipWslCheck) {
    Test-WindowsBootstrapWslEnvironment -Distribution $WslDistribution
}

Write-Host ""
Write-Host "Windows host bootstrap complete. Run the Ubuntu bootstrap command from WINDOWS.md inside $WslDistribution to provision development tools." -ForegroundColor Green
