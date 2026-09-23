<#
.SYNOPSIS
Scaffold a new xUnit test project against the consumer solution's Central Package Management
(Directory.Packages.props) pins.

.DESCRIPTION
Given a production project, this script generates tests/<ProductionProject>.Tests/, wires the
project reference, and adds PackageReference entries for the standard test stack — without
specifying any versions (CPM owns those).

The script verifies that each required package is pinned in Directory.Packages.props before
adding the reference. Missing pins are listed in a single error so the user can add them all
at once and re-run.

.PARAMETER ProductionProject
Path to the production .csproj (the project under test). Required.

.PARAMETER OutputDir
Where to place the test project. Defaults to "tests/<ProductionProject>.Tests/" relative to
the solution root.

.PARAMETER Packages
Subset of the standard test stack to include. Default is the foundation set:
  xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet.collector,
  AwesomeAssertions, NSubstitute, AutoFixture, AutoFixture.AutoNSubstitute,
  AutoFixture.Xunit2, Bogus, FluentValidation.TestHelper.

.PARAMETER WhatIf
Show what would happen without making changes.

.EXAMPLE
./new-test-project.ps1 -ProductionProject src/MyService/MyService.csproj

.EXAMPLE
./new-test-project.ps1 -ProductionProject src/MyService/MyService.csproj `
    -Packages xunit,coverlet.collector,AwesomeAssertions,NSubstitute
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $ProductionProject,
    [string] $OutputDir,
    [string[]] $Packages = @(
        'xunit',
        'xunit.runner.visualstudio',
        'Microsoft.NET.Test.Sdk',
        'coverlet.collector',
        'AwesomeAssertions',
        'NSubstitute',
        'AutoFixture',
        'AutoFixture.AutoNSubstitute',
        'AutoFixture.Xunit2',
        'Bogus',
        'FluentValidation.TestHelper'
    )
)

$ErrorActionPreference = 'Stop'

function Find-SolutionRoot {
    param([string] $StartPath)
    $current = Resolve-Path $StartPath
    while ($current) {
        if (Test-Path (Join-Path $current 'Directory.Packages.props')) {
            return $current
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { return $null }
        $current = $parent
    }
    return $null
}

function Read-PinnedPackages {
    param([string] $PropsPath)
    [xml] $doc = Get-Content $PropsPath -Raw
    $pinned = @{}
    foreach ($pv in $doc.Project.ItemGroup.PackageVersion) {
        if ($pv.Include) { $pinned[$pv.Include] = $pv.Version }
    }
    return $pinned
}

$productionPath = (Resolve-Path $ProductionProject).Path
if (-not (Test-Path $productionPath)) {
    throw "Production project not found: $ProductionProject"
}

$productionDir = Split-Path -Parent $productionPath
$productionName = [System.IO.Path]::GetFileNameWithoutExtension($productionPath)
$testName = "$productionName.Tests"

$solutionRoot = Find-SolutionRoot -StartPath $productionDir
if (-not $solutionRoot) {
    throw "Could not find Directory.Packages.props walking up from $productionDir. This script requires Central Package Management."
}

$propsPath = Join-Path $solutionRoot 'Directory.Packages.props'
$pinned = Read-PinnedPackages -PropsPath $propsPath

$missing = $Packages | Where-Object { -not $pinned.ContainsKey($_) }
if ($missing) {
    Write-Error @"
The following packages are not pinned in $propsPath:
$($missing -join "`n  - ")

Add a <PackageVersion Include="<name>" Version="<x.y.z>" /> entry for each, then re-run.
This script will not write unversioned references that would float at restore time.
"@
    exit 1
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $solutionRoot "tests/$testName"
}

$testCsproj = Join-Path $OutputDir "$testName.csproj"
if (Test-Path $testCsproj) {
    throw "Test project already exists at $testCsproj. Refusing to overwrite. Move or delete it and re-run."
}

if ($PSCmdlet.ShouldProcess($OutputDir, "Create xUnit test project")) {
    & dotnet new xunit --name $testName --output $OutputDir | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet new xunit failed with exit code $LASTEXITCODE" }

    foreach ($pkg in $Packages) {
        Write-Host "Adding PackageReference: $pkg (pinned at $($pinned[$pkg]))"
        & dotnet add $testCsproj package $pkg --no-restore | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "dotnet add package $pkg failed with exit code $LASTEXITCODE" }
    }

    Write-Host "Adding ProjectReference to $productionName"
    & dotnet add $testCsproj reference $productionPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet add reference failed with exit code $LASTEXITCODE" }

    $sln = Get-ChildItem -Path $solutionRoot -Filter '*.sln' -File | Select-Object -First 1
    if ($sln) {
        Write-Host "Adding project to solution $($sln.Name)"
        & dotnet sln $sln.FullName add $testCsproj | Out-Host
    } else {
        Write-Warning "No .sln found at $solutionRoot — skipping sln add. Add the project manually."
    }

    Write-Host ""
    Write-Host "Done. Next steps:" -ForegroundColor Green
    Write-Host "  1. dotnet restore $testCsproj"
    Write-Host "  2. Mirror $productionDir's folder structure under $OutputDir"
    Write-Host "  3. See SKILL.md for the shared rules (FIRST, 3A, naming, bans)"
}
