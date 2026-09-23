#Requires -Version 7.0

<#
.SYNOPSIS
Manages git subtrees vendored for coding-agent reference.

.DESCRIPTION
PowerShell port of dmmulroy's agent-repos CLI. It intentionally accepts the
same commands, aliases, options, manifest format, and instruction markers as
the upstream Bash script.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $CliArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ManifestName = '.agent-repos'
$script:DefaultBranch = 'main'
$script:DefaultReposDirectory = 'repos'
$script:StartMarker = '<!-- agent-repos:start -->'
$script:EndMarker = '<!-- agent-repos:end -->'
$script:AgentReposExitCode = 0

function Show-AgentReposUsage {
    <# Prints the command-line contract shared with the upstream Bash CLI. #>
    @'
Usage: agent-repos <command> [options]

Commands:
  init [options]
      Initialize the current git project for agent-readable vendored repos.

  add <github-url> [options]
      Add a repository as a squashed git subtree under repos/<name>.
      Tracked local changes are temporarily stashed while git subtree runs.
      Alias: clone

  update <name|prefix> [options]
      Pull the latest changes for one vendored subtree.
      Use --all to update every repo tracked in .agent-repos.
      Tracked local changes are temporarily stashed while git subtree runs.
      Alias: pull

  list
      List vendored repositories in the current git project.
      Alias: ls

  instructions [options]
      Add or refresh the root agent instructions block for vendored repos.
      Aliases: agent-md, agents

  completions [fish] [options]
      Print shell completions, or install a self-updating fish completion shim.
      Alias: completion

Options for init:
  -f, --file FILE       Agent file to update, relative to project root
                         (default: existing AGENTS.md/AGENT.md, else AGENT.md)
      --no-instructions Do not update the root agent instructions file
      --no-detect       Do not detect existing directories under repos/
      --no-gitignore    Do not add .agent-repos and repos/ to .gitignore
      --no-gitkeep      Do not create repos/.gitkeep
      --install-fish-completions
                         Install the self-updating fish completion shim

Options for add:
  -n, --name NAME       Logical name (default: repo name from URL)
  -p, --prefix PATH     Subtree path (default: repos/<name>)
  -b, --branch BRANCH   Branch/ref to add (default: remote HEAD, fallback main)
      --no-squash       Do not pass --squash to git subtree

Options for update:
  -a, --all             Update all repos tracked in .agent-repos
  -u, --url URL         Override stored URL
  -p, --prefix PATH     Override stored prefix
  -b, --branch BRANCH   Override stored branch/ref
      --no-squash       Do not pass --squash to git subtree

Options for instructions:
  -f, --file FILE       Agent file to update, relative to project root
                         (default: existing AGENTS.md/AGENT.md, else AGENT.md)

Options for completions:
  -i, --install         Install fish completions instead of printing them
      --path FILE       Install path (default: ~/.config/fish/completions/agent-repos.fish)

Examples:
  agent-repos init
  agent-repos add https://github.com/OWNER/REPO.git
  agent-repos add https://github.com/OWNER/REPO.git --branch main --prefix repos/repo
  agent-repos list
  agent-repos update effect
  agent-repos update --all
  agent-repos instructions
  agent-repos instructions --file AGENTS.md
  agent-repos completions fish
  agent-repos completions fish --install

This creates/updates a .agent-repos manifest so update/list can remember each
subtree's URL, prefix, branch, and squash setting.
'@
}

function Stop-AgentRepos {
    <# Raises a CLI error that the top-level handler prints with a stable prefix. #>
    param([Parameter(Mandatory)][string] $Message)

    throw [System.InvalidOperationException]::new($Message)
}

function Invoke-GitCapture {
    <# Runs git without displaying output and returns both output and exit code. #>
    param([Parameter(Mandatory)][string[]] $GitArguments)

    $outputLines = @(& git @GitArguments 2>$null)
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = [string]::Join("`n", $outputLines)
    }
}

function Invoke-GitChecked {
    <# Runs a visible git command and fails the agent-repos operation on nonzero exit. #>
    param([Parameter(Mandatory)][string[]] $GitArguments)

    & git @GitArguments
    if ($LASTEXITCODE -ne 0) {
        Stop-AgentRepos "git command failed with exit code $LASTEXITCODE"
    }
}

function Get-AgentProjectRoot {
    <# Finds the current git project root. #>
    $result = Invoke-GitCapture -GitArguments @('rev-parse', '--show-toplevel')
    if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.Output)) {
        Stop-AgentRepos 'not inside a git repository'
    }

    $result.Output.Trim()
}

function Get-AgentReposManifestPath {
    <# Returns the manifest path for one project root. #>
    param([Parameter(Mandatory)][string] $Root)

    Join-Path $Root $script:ManifestName
}

function Initialize-AgentReposManifest {
    <# Creates the tab-delimited manifest when it does not exist. #>
    param([Parameter(Mandatory)][string] $Root)

    $path = Get-AgentReposManifestPath -Root $Root
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        [System.IO.File]::WriteAllText(
            $path,
            "# agent-repos manifest`n# name`tprefix`turl`tbranch`tsquash`n",
            [System.Text.UTF8Encoding]::new($false)
        )
    }
}

function Get-AgentReposManifestRows {
    <# Reads valid manifest records while ignoring comments and malformed lines. #>
    param([Parameter(Mandatory)][string] $Root)

    $path = Get-AgentReposManifestPath -Root $Root
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return
    }

    foreach ($line in [System.IO.File]::ReadAllLines($path)) {
        $fields = @($line -split "`t", 6)
        if ($fields.Count -ge 4 -and $fields[0] -ne '' -and -not $fields[0].StartsWith('#')) {
            [pscustomobject]@{
                Name = $fields[0]
                Prefix = $fields[1]
                Url = $fields[2]
                Branch = $fields[3]
                Squash = if ($fields.Count -ge 5) { $fields[4] } else { '' }
            }
        }
    }
}

function Find-AgentReposManifestRow {
    <# Finds the first manifest record matching a logical name or subtree prefix. #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Key
    )

    Get-AgentReposManifestRows -Root $Root |
        Where-Object { $_.Name -eq $Key -or $_.Prefix -eq $Key } |
        Select-Object -First 1
}

function Set-AgentReposManifestRow {
    <# Inserts or replaces a manifest record by logical repository name. #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][string] $Prefix,
        [Parameter(Mandatory)][string] $Url,
        [Parameter(Mandatory)][string] $Branch,
        [Parameter(Mandatory)][string] $Squash
    )

    Initialize-AgentReposManifest -Root $Root
    $path = Get-AgentReposManifestPath -Root $Root
    $replacement = @($Name, $Prefix, $Url, $Branch, $Squash) -join "`t"
    $updatedLines = [System.Collections.Generic.List[string]]::new()
    $found = $false

    foreach ($line in [System.IO.File]::ReadAllLines($path)) {
        $fields = @($line -split "`t", 2)
        if (-not $line.StartsWith('#') -and $fields.Count -gt 0 -and $fields[0] -eq $Name) {
            $updatedLines.Add($replacement)
            $found = $true
        } else {
            $updatedLines.Add($line)
        }
    }

    if (-not $found) {
        $updatedLines.Add($replacement)
    }

    [System.IO.File]::WriteAllText(
        $path,
        ([string]::Join("`n", $updatedLines) + "`n"),
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Get-AgentRepoDefaultBranch {
    <# Resolves the remote HEAD branch and falls back to main when unavailable. #>
    param([Parameter(Mandatory)][string] $Url)

    $result = Invoke-GitCapture -GitArguments @('ls-remote', '--symref', $Url, 'HEAD')
    if ($result.ExitCode -eq 0 -and $result.Output -match '(?m)^ref:\s+refs/heads/([^\s]+)\s+HEAD$') {
        return $Matches[1]
    }

    $script:DefaultBranch
}

function Get-AgentRepoNameFromUrl {
    <# Infers the logical repository name from the last URL path segment. #>
    param([Parameter(Mandatory)][string] $Url)

    $trimmedUrl = $Url.TrimEnd('/')
    $name = ($trimmedUrl -split '/')[-1] -replace '\.git$', ''
    if ([string]::IsNullOrWhiteSpace($name)) {
        Stop-AgentRepos "could not infer repo name from URL: $Url"
    }

    $name
}

function Format-AgentReposCommandArgument {
    <# Quotes command arguments for readable diagnostic output. #>
    param([Parameter(Mandatory)][string] $Argument)

    if ($Argument -match '[\s"'']') {
        return '"' + $Argument.Replace('"', '\"') + '"'
    }
    $Argument
}

function Invoke-AgentReposSubtreeAdd {
    <# Adds one repository through git subtree using upstream argument ordering. #>
    param(
        [Parameter(Mandatory)][string] $Prefix,
        [Parameter(Mandatory)][string] $Url,
        [Parameter(Mandatory)][string] $Branch,
        [Parameter(Mandatory)][string] $Squash
    )

    $arguments = @('subtree', 'add', "--prefix=$Prefix", $Url, $Branch)
    if ($Squash -eq 'true') {
        $arguments += '--squash'
    }
    $displayArguments = @('git') + $arguments | ForEach-Object { Format-AgentReposCommandArgument $_ }
    Write-Host ('+ ' + ($displayArguments -join ' '))
    Invoke-GitChecked -GitArguments $arguments
}

function Invoke-AgentReposSubtreePull {
    <# Updates one repository through git subtree using upstream argument ordering. #>
    param(
        [Parameter(Mandatory)][string] $Prefix,
        [Parameter(Mandatory)][string] $Url,
        [Parameter(Mandatory)][string] $Branch,
        [Parameter(Mandatory)][string] $Squash
    )

    $arguments = @('subtree', 'pull', "--prefix=$Prefix", $Url, $Branch)
    if ($Squash -eq 'true') {
        $arguments += '--squash'
    }
    $displayArguments = @('git') + $arguments | ForEach-Object { Format-AgentReposCommandArgument $_ }
    Write-Host ('+ ' + ($displayArguments -join ' '))
    Invoke-GitChecked -GitArguments $arguments
}

function Test-AgentReposTrackedChanges {
    <# Reports whether tracked worktree or index changes need temporary stashing. #>
    $result = Invoke-GitCapture -GitArguments @('status', '--porcelain', '--untracked-files=no')
    $result.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($result.Output)
}

function Initialize-AgentReposGitCommit {
    <# Creates the initial commit required by git subtree for an empty repository. #>
    $result = Invoke-GitCapture -GitArguments @('rev-parse', '--verify', 'HEAD')
    if ($result.ExitCode -eq 0) {
        return
    }

    Write-Host 'Creating an initial commit because git subtree requires HEAD.'
    & git commit --allow-empty -m 'Initialize repository'
    if ($LASTEXITCODE -ne 0) {
        Stop-AgentRepos 'failed to create the initial commit required by git subtree'
    }
}

function Invoke-AgentReposWithCleanWorktree {
    <# Runs subtree work with tracked changes stashed and always attempts restoration. #>
    param(
        [Parameter(Mandatory)][string] $Reason,
        [Parameter(Mandatory)][scriptblock] $Operation
    )

    $stashed = $false
    $operationError = $null
    if (Test-AgentReposTrackedChanges) {
        Write-Host "Temporarily stashing tracked working tree changes so $Reason can run."
        $null = & git stash push -q -m "agent-repos: temporary autostash before $Reason" 2>&1
        if ($LASTEXITCODE -ne 0) {
            Stop-AgentRepos 'failed to stash local changes'
        }
        $stashed = $true
    }

    try {
        & $Operation
    } catch {
        $operationError = $_
    }

    if ($stashed) {
        Write-Host 'Restoring stashed working tree changes.'
        $null = & git stash apply --index -q 'stash@{0}' 2>&1
        if ($LASTEXITCODE -eq 0) {
            $null = & git stash drop -q 'stash@{0}' 2>&1
        } else {
            [Console]::Error.WriteLine('agent-repos: failed to restore stashed changes; they remain in stash@{0}')
            [Console]::Error.WriteLine('agent-repos: resolve the issue, then run: git stash pop --index stash@{0}')
            Stop-AgentRepos 'failed to restore stashed changes'
        }
    }

    if ($null -ne $operationError) {
        throw $operationError
    }
}

function Test-AgentReposPrefixTracked {
    <# Reports whether any git-tracked path already occupies a subtree prefix. #>
    param([Parameter(Mandatory)][string] $Prefix)

    $exactResult = Invoke-GitCapture -GitArguments @('ls-files', '--error-unmatch', '--', $Prefix)
    if ($exactResult.ExitCode -eq 0) {
        return $true
    }

    $nestedResult = Invoke-GitCapture -GitArguments @('ls-files', '--', $Prefix)
    $nestedResult.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($nestedResult.Output)
}

function Restore-AgentReposTrackedPrefix {
    <# Restores an adopted tracked prefix when it is absent from the worktree. #>
    param([Parameter(Mandatory)][string] $Prefix)

    if (Test-Path -LiteralPath $Prefix) {
        return
    }

    Write-Host "Restoring tracked prefix $Prefix from HEAD."
    $null = & git restore --staged --worktree -- $Prefix 2>&1
    if ($LASTEXITCODE -ne 0) {
        Invoke-GitChecked -GitArguments @('restore', '--', $Prefix)
    }
}

function Add-AgentReposGitignoreEntry {
    <# Adds one exact gitignore entry without duplicating existing lines. #>
    param(
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Entry
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        [System.IO.File]::WriteAllText($Path, '', [System.Text.UTF8Encoding]::new($false))
    }

    $lines = @([System.IO.File]::ReadAllLines($Path))
    if ($lines -ccontains $Entry) {
        return
    }

    $contents = [System.IO.File]::ReadAllText($Path)
    if ($contents.Length -gt 0 -and -not $contents.EndsWith("`n")) {
        $contents += "`n"
    }
    $contents += "$Entry`n"
    [System.IO.File]::WriteAllText($Path, $contents, [System.Text.UTF8Encoding]::new($false))
}

function Update-AgentReposGitignore {
    <# Adds the manifest and vendored repository directory to .gitignore. #>
    param([Parameter(Mandatory)][string] $Root)

    $gitignorePath = Join-Path $Root '.gitignore'
    Add-AgentReposGitignoreEntry -Path $gitignorePath -Entry '.agent-repos'
    Add-AgentReposGitignoreEntry -Path $gitignorePath -Entry 'repos/'
}

function ConvertTo-NormalizedAgentRepoUrl {
    <# Normalizes npm and SSH GitHub repository URLs for the manifest. #>
    param([Parameter(Mandatory)][string] $Url)

    $normalized = $Url -replace '^git\+', '' -replace '\.git$', ''
    if ($normalized -match '^git@github\.com:(.+)$') {
        $normalized = "https://github.com/$($Matches[1])"
    }
    $normalized
}

function ConvertTo-AgentRepoUrlCandidate {
    <# Extracts a canonical public GitHub repository URL from metadata text. #>
    param([AllowNull()][object] $Value)

    if ($Value -isnot [string]) {
        return
    }

    $pattern = '(?:git\+)?(?:(?:https://github\.com/)|git@github\.com:)([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?(?:[#/].*)?$'
    $candidate = $Value.Trim()
    if ($candidate -match $pattern -and $Matches[1] -ne 'user-attachments') {
        "https://github.com/$($Matches[1])/$($Matches[2])"
    }
}

function Get-AgentRepoUrlFromDirectory {
    <# Infers a GitHub URL from git origin, package.json, or agent-facing docs. #>
    param([Parameter(Mandatory)][string] $Directory)

    $gitMarker = Join-Path $Directory '.git'
    if (Test-Path -LiteralPath $gitMarker) {
        $originResult = Invoke-GitCapture -GitArguments @('-C', $Directory, 'config', '--get', 'remote.origin.url')
        if ($originResult.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($originResult.Output)) {
            return ConvertTo-NormalizedAgentRepoUrl -Url $originResult.Output.Trim()
        }
    }

    $packageJsonPath = Join-Path $Directory 'package.json'
    if (Test-Path -LiteralPath $packageJsonPath -PathType Leaf) {
        try {
            $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
            $candidates = [System.Collections.Generic.List[object]]::new()
            $repositoryProperty = $packageJson.PSObject.Properties['repository']
            if ($null -ne $repositoryProperty) {
                if ($repositoryProperty.Value -is [string]) {
                    $candidates.Add($repositoryProperty.Value)
                } elseif ($null -ne $repositoryProperty.Value) {
                    $repositoryUrl = $repositoryProperty.Value.PSObject.Properties['url']
                    if ($null -ne $repositoryUrl) { $candidates.Add($repositoryUrl.Value) }
                }
            }
            $bugsProperty = $packageJson.PSObject.Properties['bugs']
            if ($null -ne $bugsProperty) {
                if ($bugsProperty.Value -is [string]) {
                    $candidates.Add($bugsProperty.Value)
                } elseif ($null -ne $bugsProperty.Value) {
                    $bugsUrl = $bugsProperty.Value.PSObject.Properties['url']
                    if ($null -ne $bugsUrl) { $candidates.Add($bugsUrl.Value) }
                }
            }
            $homepageProperty = $packageJson.PSObject.Properties['homepage']
            if ($null -ne $homepageProperty) { $candidates.Add($homepageProperty.Value) }

            foreach ($candidate in $candidates) {
                $url = ConvertTo-AgentRepoUrlCandidate -Value $candidate
                if (-not [string]::IsNullOrWhiteSpace($url)) { return $url }
            }
        } catch {
            # Invalid package metadata is ignored just like the upstream script.
        }
    }

    foreach ($fileName in @('README.md', 'AGENTS.md', 'AGENT.md', 'LLMS.md')) {
        $path = Join-Path $Directory $fileName
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        try {
            $contents = Get-Content -LiteralPath $path -Raw
            $matches = [regex]::Matches($contents, '(?:git\+)?(?:https://github\.com/|git@github\.com:)[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:\.git)?')
            foreach ($match in $matches) {
                $url = ConvertTo-AgentRepoUrlCandidate -Value $match.Value
                if (-not [string]::IsNullOrWhiteSpace($url)) { return $url }
            }
        } catch {
            continue
        }
    }
}

function Find-ExistingAgentRepos {
    <# Adds immediate repos/ directories missing from the manifest. #>
    param([Parameter(Mandatory)][string] $Root)

    $reposDirectory = Join-Path $Root $script:DefaultReposDirectory
    if (-not (Test-Path -LiteralPath $reposDirectory -PathType Container)) {
        return
    }

    foreach ($directory in Get-ChildItem -LiteralPath $reposDirectory -Directory | Sort-Object Name) {
        $name = $directory.Name
        $prefix = "$($script:DefaultReposDirectory)/$name"
        if ($null -ne (Find-AgentReposManifestRow -Root $Root -Key $name) -or
            $null -ne (Find-AgentReposManifestRow -Root $Root -Key $prefix)) {
            continue
        }

        $url = Get-AgentRepoUrlFromDirectory -Directory $directory.FullName
        if ([string]::IsNullOrWhiteSpace($url)) { $url = 'UNKNOWN' }
        Set-AgentReposManifestRow -Root $Root -Name $name -Prefix $prefix -Url $url -Branch $script:DefaultBranch -Squash 'true'
        if ($url -eq 'UNKNOWN') {
            Write-Host "Detected $prefix (URL unknown; update once with: agent-repos update $name --url <github-url>)"
        } else {
            Write-Host "Detected $prefix -> $url"
        }
    }
}

function Get-AgentReposOptionValue {
    <# Validates that a command option has a following value. #>
    param(
        [Parameter(Mandatory)][string[]] $Arguments,
        [Parameter(Mandatory)][int] $Index
    )

    if ($Index + 1 -ge $Arguments.Count) {
        Stop-AgentRepos "$($Arguments[$Index]) requires a value"
    }
    $Arguments[$Index + 1]
}

function Invoke-AgentReposInitCommand {
    <# Implements agent-repos init with the upstream options and defaults. #>
    param([string[]] $Arguments = @())

    $Arguments = @($Arguments)
    $requestedFile = ''
    $updateInstructions = $true
    $detectRepos = $true
    $updateIgnore = $true
    $createGitkeep = $true
    $installCompletions = $false

    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        switch ($Arguments[$index]) {
            { $_ -in @('-f', '--file') } { $requestedFile = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            '--no-instructions' { $updateInstructions = $false; break }
            '--no-detect' { $detectRepos = $false; break }
            '--no-gitignore' { $updateIgnore = $false; break }
            '--no-gitkeep' { $createGitkeep = $false; break }
            '--install-fish-completions' { $installCompletions = $true; break }
            { $_ -in @('-h', '--help') } { Show-AgentReposUsage; return }
            default { Stop-AgentRepos "unknown init option: $($Arguments[$index])" }
        }
    }

    $root = Get-AgentProjectRoot
    Set-Location -LiteralPath $root
    Initialize-AgentReposManifest -Root $root
    if ($updateIgnore) { Update-AgentReposGitignore -Root $root }
    $reposDirectory = Join-Path $root $script:DefaultReposDirectory
    $null = New-Item -ItemType Directory -Path $reposDirectory -Force
    if ($detectRepos) { Find-ExistingAgentRepos -Root $root }
    if ($createGitkeep) {
        [System.IO.File]::WriteAllBytes((Join-Path $reposDirectory '.gitkeep'), [byte[]]::new(0))
    }
    if ($updateInstructions) {
        if ($requestedFile) {
            Invoke-AgentReposInstructionsCommand -Arguments @('--file', $requestedFile)
        } else {
            Invoke-AgentReposInstructionsCommand
        }
    }
    if ($installCompletions) {
        Install-AgentReposFishCompletion -Path (Get-DefaultAgentReposFishCompletionPath)
    }

    Write-Host "Initialized agent-repos in $root"
    Write-Host 'Next: agent-repos add https://github.com/OWNER/REPO.git'
}

function Invoke-AgentReposAddCommand {
    <# Implements agent-repos add and clone with positional URL handling. #>
    param([string[]] $Arguments = @())

    $Arguments = @($Arguments)
    if ($Arguments.Count -lt 1) {
        Stop-AgentRepos 'Usage: agent-repos add <github-url> [--name NAME] [--branch BRANCH] [--prefix PATH]'
    }

    $url = $Arguments[0]
    $name = ''
    $prefix = ''
    $branch = ''
    $squash = 'true'
    for ($index = 1; $index -lt $Arguments.Count; $index++) {
        switch ($Arguments[$index]) {
            { $_ -in @('-n', '--name') } { $name = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            { $_ -in @('-p', '--prefix') } { $prefix = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            { $_ -in @('-b', '--branch') } { $branch = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            '--no-squash' { $squash = 'false'; break }
            { $_ -in @('-h', '--help') } { Show-AgentReposUsage; return }
            default { Stop-AgentRepos "unknown add option: $($Arguments[$index])" }
        }
    }

    $root = Get-AgentProjectRoot
    Set-Location -LiteralPath $root
    if (-not $name) { $name = Get-AgentRepoNameFromUrl -Url $url }
    if (-not $prefix) { $prefix = "$($script:DefaultReposDirectory)/$name" }
    if (-not $branch) { $branch = Get-AgentRepoDefaultBranch -Url $url }

    if (Test-AgentReposPrefixTracked -Prefix $prefix) {
        Write-Host "Prefix $prefix is already tracked by git; adopting it in $($script:ManifestName) instead of running git subtree add."
        Restore-AgentReposTrackedPrefix -Prefix $prefix
        Set-AgentReposManifestRow -Root $root -Name $name -Prefix $prefix -Url $url -Branch $branch -Squash $squash
        Write-Host "Tracked $name at $prefix in $($script:ManifestName)"
        Write-Host 'Run: agent-repos instructions'
        return
    }

    if (Test-Path -LiteralPath $prefix) {
        Stop-AgentRepos "prefix already exists: $prefix"
    }

    Initialize-AgentReposGitCommit
    Invoke-AgentReposWithCleanWorktree -Reason 'git subtree add' -Operation {
        Invoke-AgentReposSubtreeAdd -Prefix $prefix -Url $url -Branch $branch -Squash $squash
    }
    Set-AgentReposManifestRow -Root $root -Name $name -Prefix $prefix -Url $url -Branch $branch -Squash $squash
    Write-Host "Tracked $name at $prefix in $($script:ManifestName)"
    Write-Host 'Run: agent-repos instructions'
}

function Update-OneAgentRepo {
    <# Pulls and persists one manifest repository with optional overrides. #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [Parameter(Mandatory)][string] $Key,
        [string] $OverrideUrl = '',
        [string] $OverridePrefix = '',
        [string] $OverrideBranch = '',
        [string] $OverrideSquash = ''
    )

    $row = Find-AgentReposManifestRow -Root $Root -Key $Key
    if ($null -eq $row) {
        $name = $Key
        $prefix = $OverridePrefix
        $url = $OverrideUrl
        $branch = $OverrideBranch
        $squash = $OverrideSquash
        if (-not $prefix -or -not $url) {
            Stop-AgentRepos "'$Key' is not in $($script:ManifestName); pass --prefix and --url or add it first"
        }
    } else {
        $name = $row.Name
        $prefix = $row.Prefix
        $url = $row.Url
        $branch = $row.Branch
        $squash = $row.Squash
    }

    if ($OverrideUrl) { $url = $OverrideUrl }
    if ($OverridePrefix) { $prefix = $OverridePrefix }
    if ($OverrideBranch) { $branch = $OverrideBranch }
    if ($url -eq 'UNKNOWN') {
        Stop-AgentRepos "URL for '$name' is unknown; rerun with: agent-repos update $name --url <github-url>"
    }
    if (-not $branch) { $branch = Get-AgentRepoDefaultBranch -Url $url }
    if ($OverrideSquash) { $squash = $OverrideSquash }
    if (-not $squash) { $squash = 'true' }

    Write-Host "Updating $name ($prefix) from $url @ $branch"
    Invoke-AgentReposWithCleanWorktree -Reason 'git subtree pull' -Operation {
        Invoke-AgentReposSubtreePull -Prefix $prefix -Url $url -Branch $branch -Squash $squash
    }
    Set-AgentReposManifestRow -Root $Root -Name $name -Prefix $prefix -Url $url -Branch $branch -Squash $squash
}

function Invoke-AgentReposUpdateCommand {
    <# Implements agent-repos update and pull for one or all manifest entries. #>
    param([string[]] $Arguments = @())

    $Arguments = @($Arguments)
    $key = ''
    $updateAll = $false
    $overrideUrl = ''
    $overridePrefix = ''
    $overrideBranch = ''
    $overrideSquash = ''
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        switch ($Arguments[$index]) {
            { $_ -in @('-a', '--all') } { $updateAll = $true; break }
            { $_ -in @('-u', '--url') } { $overrideUrl = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            { $_ -in @('-p', '--prefix') } { $overridePrefix = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            { $_ -in @('-b', '--branch') } { $overrideBranch = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            '--no-squash' { $overrideSquash = 'false'; break }
            { $_ -in @('-h', '--help') } { Show-AgentReposUsage; return }
            default {
                if ($key) { Stop-AgentRepos "too many update arguments: $($Arguments[$index..($Arguments.Count - 1)] -join ' ')" }
                $key = $Arguments[$index]
            }
        }
    }

    $root = Get-AgentProjectRoot
    Set-Location -LiteralPath $root
    if ($updateAll) {
        if ($key) { Stop-AgentRepos 'use either --all or a single repo name/prefix' }
        if (-not (Test-Path -LiteralPath (Get-AgentReposManifestPath $root) -PathType Leaf)) {
            Stop-AgentRepos "no $($script:ManifestName) manifest found"
        }
        foreach ($row in @(Get-AgentReposManifestRows -Root $root)) {
            Update-OneAgentRepo -Root $root -Key $row.Name -OverrideUrl $overrideUrl -OverridePrefix $overridePrefix -OverrideBranch $overrideBranch -OverrideSquash $overrideSquash
        }
        return
    }

    if (-not $key) {
        Stop-AgentRepos 'Usage: agent-repos update <name|prefix> [options] or agent-repos update --all'
    }
    Update-OneAgentRepo -Root $root -Key $key -OverrideUrl $overrideUrl -OverridePrefix $overridePrefix -OverrideBranch $overrideBranch -OverrideSquash $overrideSquash
}

function Invoke-AgentReposListCommand {
    <# Lists manifest records or falls back to discovered repos/ directories. #>
    param([string[]] $Arguments = @())

    $Arguments = @($Arguments)
    $root = Get-AgentProjectRoot
    Set-Location -LiteralPath $root
    $rows = @(Get-AgentReposManifestRows -Root $root)
    if (Test-Path -LiteralPath (Get-AgentReposManifestPath $root) -PathType Leaf) {
        '{0,-20} {1,-28} {2,-10} {3}' -f 'NAME', 'PREFIX', 'BRANCH', 'URL'
        foreach ($row in $rows) {
            '{0,-20} {1,-28} {2,-10} {3}' -f $row.Name, $row.Prefix, $row.Branch, $row.Url
        }
    }

    if ($rows.Count -eq 0) {
        $reposDirectory = Join-Path $root $script:DefaultReposDirectory
        if (Test-Path -LiteralPath $reposDirectory -PathType Container) {
            Write-Host "No $($script:ManifestName) manifest entries found. Discovered directories under $($script:DefaultReposDirectory)/:"
            Get-ChildItem -LiteralPath $reposDirectory -Directory | Sort-Object FullName | ForEach-Object {
                "$($script:DefaultReposDirectory)/$($_.Name)"
            }
        } else {
            Write-Host 'No vendored repos found.'
            Write-Host 'Add one with: agent-repos add https://github.com/OWNER/REPO.git'
        }
    }
}

function Get-AgentReposInstructionFilePath {
    <# Selects the requested or conventional root agent instruction file. #>
    param(
        [Parameter(Mandatory)][string] $Root,
        [string] $RequestedFile = ''
    )

    if ($RequestedFile) { return Join-Path $Root $RequestedFile }
    $agentsPath = Join-Path $Root 'AGENTS.md'
    if (Test-Path -LiteralPath $agentsPath -PathType Leaf) { return $agentsPath }
    $agentPath = Join-Path $Root 'AGENT.md'
    if (Test-Path -LiteralPath $agentPath -PathType Leaf) { return $agentPath }
    $agentPath
}

function New-AgentReposInstructionsBlock {
    <# Generates the managed vendored-repositories instruction block. #>
    param([Parameter(Mandatory)][string] $Root)

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in @(
        $script:StartMarker,
        '## Vendored Repositories',
        '',
        'This project vendors external repositories under @repos/ for coding-agent reference.',
        '',
        '- Use vendored repositories as read-only reference material when working with related libraries.',
        '- Prefer examples and patterns from vendored source code over generated guesses or web search results.',
        '- Do not edit files under @repos/ unless explicitly asked.',
        '- Do not import from @repos/; application code should continue importing from normal package dependencies.'
    )) { $lines.Add($line) }

    if ((Test-Path -LiteralPath (Get-AgentReposManifestPath $Root) -PathType Leaf) -or
        (Test-Path -LiteralPath (Join-Path $Root $script:DefaultReposDirectory) -PathType Container)) {
        $lines.Add('')
        $lines.Add('Vendored repositories currently available:')
        $rows = @(Get-AgentReposManifestRows -Root $Root)
        if ($rows.Count -gt 0) {
            foreach ($row in $rows) {
                $bullet = "- @$($row.Prefix.TrimEnd('/'))/ — $($row.Url)"
                if ($row.Branch) { $bullet += " ($($row.Branch))" }
                $lines.Add($bullet)
            }
        } else {
            $reposDirectory = Join-Path $Root $script:DefaultReposDirectory
            if (Test-Path -LiteralPath $reposDirectory -PathType Container) {
                foreach ($directory in Get-ChildItem -LiteralPath $reposDirectory -Directory | Sort-Object FullName) {
                    $lines.Add("- @$($script:DefaultReposDirectory)/$($directory.Name)/")
                }
            }
        }
    }

    $lines.Add('')
    $lines.Add('When working with a related library, inspect its vendored repository for idiomatic usage, tests, module structure, API design, examples, and docs. If the vendored repository contains agent-oriented guidance such as LLMS.md, AGENTS.md, or AGENT.md, read that guidance before making changes.')
    $lines.Add('')
    $lines.Add('When repeatedly working with a vendored library, consider creating a project-local pattern file under agent-patterns/ (for example, agent-patterns/<library>-<topic>.md) that summarizes the implementation, tests, docs, common constructors/combinators, examples, error-handling patterns, and what to avoid.')
    $lines.Add($script:EndMarker)
    [string]::Join("`n", $lines)
}

function Invoke-AgentReposInstructionsCommand {
    <# Creates or refreshes the managed block in the root agent instruction file. #>
    param([string[]] $Arguments = @())

    $Arguments = @($Arguments)
    $requestedFile = ''
    for ($index = 0; $index -lt $Arguments.Count; $index++) {
        switch ($Arguments[$index]) {
            { $_ -in @('-f', '--file') } { $requestedFile = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            { $_ -in @('-h', '--help') } { Show-AgentReposUsage; return }
            default { Stop-AgentRepos "unknown instructions option: $($Arguments[$index])" }
        }
    }

    $root = Get-AgentProjectRoot
    Set-Location -LiteralPath $root
    $filePath = Get-AgentReposInstructionFilePath -Root $root -RequestedFile $requestedFile
    $parentDirectory = Split-Path -Parent $filePath
    if ($parentDirectory) { $null = New-Item -ItemType Directory -Path $parentDirectory -Force }
    $block = New-AgentReposInstructionsBlock -Root $root

    if (Test-Path -LiteralPath $filePath -PathType Leaf) {
        $existingLines = @([System.IO.File]::ReadAllLines($filePath))
        $outputLines = [System.Collections.Generic.List[string]]::new()
        $insideBlock = $false
        $replaced = $false
        foreach ($line in $existingLines) {
            if ($line -eq $script:StartMarker) {
                foreach ($blockLine in ($block -split "`n")) { $outputLines.Add($blockLine) }
                $insideBlock = $true
                $replaced = $true
                continue
            }
            if ($line -eq $script:EndMarker) {
                $insideBlock = $false
                continue
            }
            if (-not $insideBlock) { $outputLines.Add($line) }
        }
        if (-not $replaced) {
            if ($outputLines.Count -gt 0) { $outputLines.Add('') }
            foreach ($blockLine in ($block -split "`n")) { $outputLines.Add($blockLine) }
        }
        $contents = [string]::Join("`n", $outputLines) + "`n"
    } else {
        $contents = "# Agent Instructions`n`n$block`n"
    }

    [System.IO.File]::WriteAllText($filePath, $contents, [System.Text.UTF8Encoding]::new($false))
    $relativePath = [System.IO.Path]::GetRelativePath($root, $filePath).Replace('\', '/')
    Write-Host "Updated $relativePath"
}

function New-AgentReposFishCompletions {
    <# Emits the fish completion definitions maintained by the upstream CLI. #>
    @'
# Fish completions for agent-repos.
# Generated by: agent-repos completions fish

function __agent_repos_names
    set -l root (git rev-parse --show-toplevel 2>/dev/null)
    if test -n "$root"; and test -f "$root/.agent-repos"
        awk -F '\t' 'NF >= 4 && $1 !~ /^#/ { print $1 "\t" $2 }' "$root/.agent-repos"
    end
end

complete -c agent-repos -f
complete -c agent-repos -n __fish_use_subcommand -a 'init' -d 'Initialize project for vendored repos'
complete -c agent-repos -n __fish_use_subcommand -a 'add' -d 'Add a repo as a git subtree'
complete -c agent-repos -n __fish_use_subcommand -a 'clone' -d 'Alias for add'
complete -c agent-repos -n __fish_use_subcommand -a 'update' -d 'Update a vendored subtree'
complete -c agent-repos -n __fish_use_subcommand -a 'pull' -d 'Alias for update'
complete -c agent-repos -n __fish_use_subcommand -a 'list' -d 'List vendored repos'
complete -c agent-repos -n __fish_use_subcommand -a 'ls' -d 'Alias for list'
complete -c agent-repos -n __fish_use_subcommand -a 'instructions' -d 'Update root agent instructions'
complete -c agent-repos -n __fish_use_subcommand -a 'agent-md' -d 'Alias for instructions'
complete -c agent-repos -n __fish_use_subcommand -a 'agents' -d 'Alias for instructions'
complete -c agent-repos -n __fish_use_subcommand -a 'completions' -d 'Print or install shell completions'
complete -c agent-repos -n __fish_use_subcommand -a 'completion' -d 'Alias for completions'
complete -c agent-repos -n __fish_use_subcommand -a 'help' -d 'Show help'

complete -c agent-repos -n '__fish_seen_subcommand_from init' -s f -l file -r -d 'Agent file to update'
complete -c agent-repos -n '__fish_seen_subcommand_from init' -l no-instructions -d 'Do not update agent instructions'
complete -c agent-repos -n '__fish_seen_subcommand_from init' -l no-detect -d 'Do not detect existing repos/'
complete -c agent-repos -n '__fish_seen_subcommand_from init' -l no-gitignore -d 'Do not update .gitignore'
complete -c agent-repos -n '__fish_seen_subcommand_from init' -l no-gitkeep -d 'Do not create repos/.gitkeep'
complete -c agent-repos -n '__fish_seen_subcommand_from init' -l install-fish-completions -d 'Install fish completion shim'

complete -c agent-repos -n '__fish_seen_subcommand_from add clone' -s n -l name -r -d 'Logical repo name'
complete -c agent-repos -n '__fish_seen_subcommand_from add clone' -s p -l prefix -r -d 'Subtree path'
complete -c agent-repos -n '__fish_seen_subcommand_from add clone' -s b -l branch -r -d 'Branch/ref'
complete -c agent-repos -n '__fish_seen_subcommand_from add clone' -l no-squash -d 'Do not squash subtree history'

complete -c agent-repos -n '__fish_seen_subcommand_from update pull' -a '(__agent_repos_names)' -d 'Vendored repo'
complete -c agent-repos -n '__fish_seen_subcommand_from update pull' -s a -l all -d 'Update all tracked repos'
complete -c agent-repos -n '__fish_seen_subcommand_from update pull' -s u -l url -r -d 'Override repo URL'
complete -c agent-repos -n '__fish_seen_subcommand_from update pull' -s p -l prefix -r -d 'Override subtree path'
complete -c agent-repos -n '__fish_seen_subcommand_from update pull' -s b -l branch -r -d 'Override branch/ref'
complete -c agent-repos -n '__fish_seen_subcommand_from update pull' -l no-squash -d 'Do not squash subtree history'

complete -c agent-repos -n '__fish_seen_subcommand_from instructions agent-md agents' -s f -l file -r -d 'Agent file to update'

complete -c agent-repos -n '__fish_seen_subcommand_from completions completion' -a 'fish' -d 'Fish shell'
complete -c agent-repos -n '__fish_seen_subcommand_from completions completion' -s i -l install -d 'Install self-updating fish completion shim'
complete -c agent-repos -n '__fish_seen_subcommand_from completions completion' -l path -r -d 'Completion install path'
'@
}

function Get-DefaultAgentReposFishCompletionPath {
    <# Returns the XDG-aware default fish completion shim path. #>
    $configHome = if ($env:XDG_CONFIG_HOME) { $env:XDG_CONFIG_HOME } else { Join-Path $HOME '.config' }
    Join-Path $configHome 'fish/completions/agent-repos.fish'
}

function Install-AgentReposFishCompletion {
    <# Installs a fish shim that always sources completions from the current CLI. #>
    param([Parameter(Mandatory)][string] $Path)

    $parentDirectory = Split-Path -Parent $Path
    if ($parentDirectory) { $null = New-Item -ItemType Directory -Path $parentDirectory -Force }
    $contents = @'
# Installed by `agent-repos completions fish --install`.
# This shim sources completions generated by the CLI so they stay up to date.
if type -q agent-repos
    agent-repos completions fish | source
end
'@
    [System.IO.File]::WriteAllText($Path, $contents + "`n", [System.Text.UTF8Encoding]::new($false))
    Write-Host "Installed fish completions to $Path"
}

function Invoke-AgentReposCompletionsCommand {
    <# Prints or installs completions with upstream shell validation. #>
    param([string[]] $Arguments = @())

    $Arguments = @($Arguments)
    $shell = 'fish'
    $install = $false
    $path = ''
    $index = 0
    if ($Arguments.Count -gt 0 -and -not $Arguments[0].StartsWith('-')) {
        $shell = $Arguments[0]
        $index = 1
    }
    while ($index -lt $Arguments.Count) {
        switch ($Arguments[$index]) {
            { $_ -in @('-i', '--install') } { $install = $true; break }
            '--path' { $path = Get-AgentReposOptionValue $Arguments $index; $index++; break }
            { $_ -in @('-h', '--help') } { Show-AgentReposUsage; return }
            default { Stop-AgentRepos "unknown completions option: $($Arguments[$index])" }
        }
        $index++
    }

    if ($shell -ne 'fish') { Stop-AgentRepos "unsupported shell for completions: $shell" }
    if ($install) {
        if (-not $path) { $path = Get-DefaultAgentReposFishCompletionPath }
        Install-AgentReposFishCompletion -Path $path
    } else {
        New-AgentReposFishCompletions
    }
}

function Invoke-AgentReposCli {
    <# Dispatches the same commands and aliases as the upstream agent-repos CLI. #>
    param([string[]] $Arguments = @())

    $Arguments = @($Arguments)
    if ($Arguments.Count -eq 0 -or [string]::IsNullOrEmpty($Arguments[0])) {
        Show-AgentReposUsage
        $script:AgentReposExitCode = 1
        return
    }

    $commandName = $Arguments[0]
    $commandParameters = if ($Arguments.Count -gt 1) {
        @{ Arguments = [string[]]@($Arguments[1..($Arguments.Count - 1)]) }
    } else {
        @{}
    }
    switch ($commandName) {
        { $_ -eq 'init' } { Invoke-AgentReposInitCommand @commandParameters; break }
        { $_ -in @('add', 'clone') } { Invoke-AgentReposAddCommand @commandParameters; break }
        { $_ -in @('update', 'pull') } { Invoke-AgentReposUpdateCommand @commandParameters; break }
        { $_ -in @('list', 'ls') } { Invoke-AgentReposListCommand @commandParameters; break }
        { $_ -in @('instructions', 'agent-md', 'agents') } { Invoke-AgentReposInstructionsCommand @commandParameters; break }
        { $_ -in @('completions', 'completion') } { Invoke-AgentReposCompletionsCommand @commandParameters; break }
        { $_ -in @('-h', '--help', 'help') } { Show-AgentReposUsage; break }
        default { Stop-AgentRepos "unknown command: $commandName" }
    }
}

try {
    Invoke-AgentReposCli -Arguments $CliArguments
} catch {
    [Console]::Error.WriteLine("agent-repos: $($_.Exception.Message)")
    if ($env:AGENT_REPOS_DEBUG) {
        [Console]::Error.WriteLine($_.ScriptStackTrace)
    }
    $script:AgentReposExitCode = 1
}

exit $script:AgentReposExitCode
