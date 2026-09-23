param(
    [ValidateSet('open-picker', 'picker', 'last', 'list', 'sync-history', 'track-focus', 'remove-closed')]
    [string] $Command = 'picker'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$script:PluginId = 'treramey.sesh-windows'
$script:HerdrBinary = if ($env:HERDR_BIN_PATH) { $env:HERDR_BIN_PATH } else { 'herdr' }
$script:HistoryStateDirectory = if ($env:HERDR_PLUGIN_STATE_DIR) {
    $env:HERDR_PLUGIN_STATE_DIR
} else {
    Join-Path $env:LOCALAPPDATA 'herdr\plugin-state\treramey.sesh-windows'
}
$script:HistoryStatePath = Join-Path $script:HistoryStateDirectory 'workspace-history.json'
$script:HistoryMutexName = 'Local\treramey.sesh-windows.workspace-history'

function Invoke-NativeProcess {
    param(
        [Parameter(Mandatory)] [string] $FilePath,
        [string[]] $ArgumentList = @(),
        [switch] $AllowFailure
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    foreach ($argument in $ArgumentList) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Sesh for Windows process failed to start: $FilePath"
    }

    $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
    $standardErrorTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
    $standardError = $standardErrorTask.GetAwaiter().GetResult()

    if ($process.ExitCode -ne 0 -and -not $AllowFailure) {
        $detail = $standardError.Trim()
        if (-not $detail) { $detail = $standardOutput.Trim() }
        throw "Sesh for Windows command failed ($($process.ExitCode)): $FilePath $($ArgumentList -join ' ')`n$detail"
    }

    [pscustomobject]@{
        ExitCode = $process.ExitCode
        StandardOutput = $standardOutput
        StandardError = $standardError
    }
}

function Invoke-HerdrJsonCommand {
    param([Parameter(Mandatory)] [string[]] $ArgumentList)

    $result = Invoke-NativeProcess -FilePath $script:HerdrBinary -ArgumentList $ArgumentList
    try {
        $result.StandardOutput | ConvertFrom-Json -Depth 100
    } catch {
        throw "Sesh for Windows could not parse Herdr JSON for '$($ArgumentList -join ' ')': $($_.Exception.Message)"
    }
}

function Get-HerdrSessionSnapshot {
    $response = Invoke-HerdrJsonCommand -ArgumentList @('api', 'snapshot')
    $response.result.snapshot
}

function Expand-SeshWindowsPath {
    param([Parameter(Mandatory)] [string] $Path)

    $expandedPath = [Environment]::ExpandEnvironmentVariables($Path)
    if ($expandedPath -eq '~') {
        return $HOME
    }
    if ($expandedPath.StartsWith('~/') -or $expandedPath.StartsWith('~\')) {
        return Join-Path $HOME $expandedPath.Substring(2)
    }
    $expandedPath
}

function ConvertFrom-TomlStringValue {
    param([Parameter(Mandatory)] [string] $Value)

    $trimmedValue = $Value.Trim()
    if ($trimmedValue.StartsWith("'") -and $trimmedValue.EndsWith("'")) {
        return $trimmedValue.Substring(1, $trimmedValue.Length - 2).Replace("''", "'")
    }
    try {
        return $trimmedValue | ConvertFrom-Json
    } catch {
        throw "Sesh for Windows found an unsupported TOML string: $trimmedValue"
    }
}

function Add-SeshConfiguredSession {
    param(
        [Parameter(Mandatory)] [AllowEmptyCollection()] [System.Collections.Generic.List[object]] $Sessions,
        [hashtable] $SessionValues
    )

    if (-not $SessionValues -or -not $SessionValues.ContainsKey('name') -or -not $SessionValues.ContainsKey('path')) {
        return
    }

    $sessionPath = Expand-SeshWindowsPath -Path $SessionValues.path
    if (-not (Test-Path -LiteralPath $sessionPath -PathType Container)) {
        return
    }

    $Sessions.Add([pscustomobject]@{
        Kind = 'directory'
        Label = [string] $SessionValues.name
        Path = [System.IO.Path]::GetFullPath($sessionPath)
        StartupCommand = if ($SessionValues.ContainsKey('startup_command')) { [string] $SessionValues.startup_command } else { '' }
        WorkspaceId = ''
        Source = 'config'
    })
}

function Get-SeshConfiguredSessions {
    $configurationPath = if ($env:HERDR_SESH_CONFIG) {
        Expand-SeshWindowsPath -Path $env:HERDR_SESH_CONFIG
    } else {
        Join-Path $HOME '.config\sesh\sesh.toml'
    }
    if (-not (Test-Path -LiteralPath $configurationPath -PathType Leaf)) {
        return @()
    }

    $sessions = [System.Collections.Generic.List[object]]::new()
    $currentSession = $null
    foreach ($line in Get-Content -LiteralPath $configurationPath) {
        if ($line -match '^\s*\[\[(?<section>[^]]+)\]\]\s*(?:#.*)?$') {
            Add-SeshConfiguredSession -Sessions $sessions -SessionValues $currentSession
            $currentSession = if ($Matches.section -eq 'session') { @{} } else { $null }
            continue
        }
        if ($null -eq $currentSession) {
            continue
        }
        if ($line -match '^\s*(?<key>name|path|startup_command)\s*=\s*(?<value>"(?:\\.|[^"])*"|''[^'']*'')\s*(?:#.*)?$') {
            $currentSession[$Matches.key] = ConvertFrom-TomlStringValue -Value $Matches.value
        }
    }
    Add-SeshConfiguredSession -Sessions $sessions -SessionValues $currentSession
    $sessions.ToArray()
}

function Get-ZoxideDirectoryEntries {
    $zoxideCommand = Get-Command 'zoxide' -ErrorAction SilentlyContinue
    if (-not $zoxideCommand) {
        return @()
    }

    $result = Invoke-NativeProcess -FilePath $zoxideCommand.Source -ArgumentList @('query', '-l', '-s') -AllowFailure
    if ($result.ExitCode -ne 0) {
        return @()
    }

    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($line in $result.StandardOutput -split "`r?`n") {
        if (-not $line.Trim()) { continue }
        $path = if ($line -match '^\s*[0-9]+(?:\.[0-9]+)?\s+(?<path>.+)$') { $Matches.path } else { $line.Trim() }
        if (-not (Test-Path -LiteralPath $path -PathType Container)) { continue }
        $fullPath = [System.IO.Path]::GetFullPath($path)
        $label = Split-Path -Leaf $fullPath
        if (-not $label) { $label = $fullPath }
        $entries.Add([pscustomobject]@{
            Kind = 'directory'
            Label = $label
            Path = $fullPath
            StartupCommand = ''
            WorkspaceId = ''
            Source = 'zoxide'
        })
    }
    $entries.ToArray()
}

function Get-HerdrWorkspaceEntries {
    param([Parameter(Mandatory)] $Snapshot)

    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($workspace in @($Snapshot.workspaces)) {
        # An overlay is represented as a normal pane and becomes focused while
        # this command runs. Exclude the picker itself so its plugin cwd does
        # not replace the workspace's real working directory.
        $workspacePanes = @($Snapshot.panes | Where-Object {
            $_.workspace_id -eq $workspace.workspace_id -and $_.pane_id -ne $env:HERDR_PANE_ID
        })
        $focusedPane = $workspacePanes | Where-Object focused | Select-Object -First 1
        $workspacePath = if ($focusedPane) {
            $focusedPane.cwd
        } elseif ($workspacePanes.Count -gt 0) {
            $workspacePanes[0].cwd
        } else {
            ''
        }
        $entries.Add([pscustomobject]@{
            Kind = 'workspace'
            Label = [string] $workspace.label
            Path = [string] $workspacePath
            StartupCommand = ''
            WorkspaceId = [string] $workspace.workspace_id
            Source = 'herdr'
            Focused = [bool] $workspace.focused
        })
    }
    $entries.ToArray()
}

function Get-SeshWindowsEntries {
    $snapshot = Get-HerdrSessionSnapshot
    $workspaceEntries = @(Get-HerdrWorkspaceEntries -Snapshot $snapshot)
    $configuredEntries = @(Get-SeshConfiguredSessions)
    $zoxideEntries = @(Get-ZoxideDirectoryEntries)
    $knownPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $mergedEntries = [System.Collections.Generic.List[object]]::new()

    foreach ($entry in $workspaceEntries) {
        $mergedEntries.Add($entry)
        if ($entry.Path) { [void] $knownPaths.Add([System.IO.Path]::GetFullPath($entry.Path)) }
    }
    foreach ($entry in @($configuredEntries) + @($zoxideEntries)) {
        $fullPath = [System.IO.Path]::GetFullPath($entry.Path)
        if ($knownPaths.Add($fullPath)) {
            $mergedEntries.Add($entry)
        }
    }
    $mergedEntries.ToArray()
}

function Format-SeshDisplayPath {
    param([Parameter(Mandatory)] [string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $homePath = [System.IO.Path]::GetFullPath($HOME).TrimEnd('\', '/')
    if ($fullPath.Equals($homePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return '~'
    }
    if ($fullPath.StartsWith("$homePath\", [System.StringComparison]::OrdinalIgnoreCase)) {
        return '~/' + $fullPath.Substring($homePath.Length + 1).Replace('\', '/')
    }
    $fullPath.Replace('\', '/')
}

function Format-SeshPickerLine {
    param([Parameter(Mandatory)] $Entry)

    $escape = [char] 27
    $icon = if ($Entry.Kind -eq 'workspace') {
        "${escape}[36m${escape}[39m"
    } elseif ($Entry.Source -eq 'config') {
        "${escape}[90m${escape}[39m"
    } else {
        "${escape}[36m${escape}[39m"
    }
    $displayPath = if ($Entry.Path) { Format-SeshDisplayPath -Path $Entry.Path } else { [string] $Entry.Label }
    "$icon $displayPath"
}

function Remove-AnsiEscapeSequences {
    param([Parameter(Mandatory)] [string] $Text)

    [regex]::Replace($Text, "`e\[[0-?]*[ -/]*[@-~]", '').Trim()
}

function Connect-SeshWindowsEntry {
    param([Parameter(Mandatory)] $Entry)

    if ($Entry.Kind -eq 'workspace') {
        [void] (Invoke-HerdrJsonCommand -ArgumentList @('workspace', 'focus', [string] $Entry.WorkspaceId))
        return
    }

    $createResponse = Invoke-HerdrJsonCommand -ArgumentList @(
        'workspace', 'create', '--cwd', [string] $Entry.Path, '--label', [string] $Entry.Label, '--focus'
    )
    if ($Entry.StartupCommand) {
        $rootPaneId = [string] $createResponse.result.root_pane.pane_id
        if ($rootPaneId) {
            [void] (Invoke-HerdrJsonCommand -ArgumentList @('pane', 'run', $rootPaneId, [string] $Entry.StartupCommand))
        }
    }

    $zoxideCommand = Get-Command 'zoxide' -ErrorAction SilentlyContinue
    if ($zoxideCommand) {
        [void] (Invoke-NativeProcess -FilePath $zoxideCommand.Source -ArgumentList @('add', [string] $Entry.Path) -AllowFailure)
    }
}

function Show-SeshWindowsPicker {
    $entries = @(Get-SeshWindowsEntries)
    if ($entries.Count -eq 0) {
        return
    }

    $entryByDisplayLine = @{}
    $pickerLines = @(
        foreach ($entry in $entries) {
            $displayLine = Format-SeshPickerLine -Entry $entry
            $plainDisplayLine = Remove-AnsiEscapeSequences -Text $displayLine
            if (-not $entryByDisplayLine.ContainsKey($plainDisplayLine)) {
                $entryByDisplayLine[$plainDisplayLine] = $entry
                $displayLine
            }
        }
    )

    $gumCommand = Get-Command 'gum' -ErrorAction SilentlyContinue
    if ($gumCommand) {
        # Match adriankarlen/herdr-sesh-minimal's inherited Rose Pine Gum theme.
        $env:GUM_FILTER_INDICATOR_FOREGROUND = '#c4a7e7'
        $env:GUM_FILTER_SELECTED_PREFIX_FOREGROUND = '#c4a7e7'
        $env:GUM_FILTER_UNSELECTED_PREFIX_FOREGROUND = '#6e6a86'
        $env:GUM_FILTER_HEADER_FOREGROUND = '#c4a7e7'
        $env:GUM_FILTER_MATCH_FOREGROUND = '#c4a7e7'
        $env:GUM_FILTER_PROMPT_FOREGROUND = '#6e6a86'
        $env:GUM_FILTER_PLACEHOLDER_FOREGROUND = '#6e6a86'
        $selection = $pickerLines | & $gumCommand.Source @(
            'filter',
            '--limit', '1',
            '--no-sort',
            '--fuzzy',
            '--no-strip-ansi',
            # Gum 2 shows key help by default; the version in the reference
            # screenshot does not, so disable it to preserve that UI.
            '--no-show-help',
            '--placeholder', 'Pick a sesh',
            '--prompt', ' '
        )
    } else {
        $fzfCommand = Get-Command 'fzf' -ErrorAction SilentlyContinue
        if (-not $fzfCommand) {
            throw 'Sesh for Windows requires gum or fzf on PATH.'
        }
        $selection = $pickerLines | & $fzfCommand.Source @(
            '--ansi',
            '--no-sort',
            '--layout', 'reverse-list',
            '--border', 'none',
            '--info', 'hidden',
            '--prompt', ' ',
            '--pointer', '•'
        )
    }
    if ($LASTEXITCODE -ne 0 -or -not $selection) {
        return
    }

    $selectedDisplayLine = Remove-AnsiEscapeSequences -Text ([string] $selection)
    $selectedEntry = $entryByDisplayLine[$selectedDisplayLine]
    if (-not $selectedEntry) {
        throw "Sesh for Windows could not resolve picker selection: $selectedDisplayLine"
    }
    Connect-SeshWindowsEntry -Entry $selectedEntry
}

function Invoke-WithWorkspaceHistoryLock {
    param([Parameter(Mandatory)] [scriptblock] $Operation)

    $mutex = [System.Threading.Mutex]::new($false, $script:HistoryMutexName)
    $lockAcquired = $false
    try {
        $lockAcquired = $mutex.WaitOne([TimeSpan]::FromSeconds(10))
        if (-not $lockAcquired) {
            throw 'Sesh for Windows timed out waiting for its workspace history lock.'
        }
        & $Operation
    } finally {
        if ($lockAcquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

function Get-WorkspaceHistoryIds {
    if (-not (Test-Path -LiteralPath $script:HistoryStatePath -PathType Leaf)) {
        return @()
    }
    try {
        $state = Get-Content -LiteralPath $script:HistoryStatePath -Raw | ConvertFrom-Json
        if ($state.PSObject.Properties['workspace_ids']) {
            return @($state.workspace_ids | ForEach-Object { [string] $_ })
        }
    } catch {
        return @()
    }
    @()
}

function Save-WorkspaceHistoryIds {
    param([Parameter(Mandatory)] [string[]] $WorkspaceIds)

    [System.IO.Directory]::CreateDirectory($script:HistoryStateDirectory) | Out-Null
    $temporaryPath = "$($script:HistoryStatePath).$PID.tmp"
    $json = @{ workspace_ids = @($WorkspaceIds); updated_at = [DateTimeOffset]::UtcNow.ToString('O') } |
        ConvertTo-Json -Compress
    [System.IO.File]::WriteAllText($temporaryPath, $json, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::Move($temporaryPath, $script:HistoryStatePath, $true)
}

function Update-WorkspaceHistory {
    param(
        [string] $FocusedWorkspaceId,
        [string] $ClosedWorkspaceId
    )

    Invoke-WithWorkspaceHistoryLock {
        $snapshot = Get-HerdrSessionSnapshot
        $liveWorkspaceIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($workspace in @($snapshot.workspaces)) { [void] $liveWorkspaceIds.Add([string] $workspace.workspace_id) }

        $orderedWorkspaceIds = [System.Collections.Generic.List[string]]::new()
        if ($FocusedWorkspaceId -and $liveWorkspaceIds.Contains($FocusedWorkspaceId)) {
            $orderedWorkspaceIds.Add($FocusedWorkspaceId)
        }
        foreach ($workspaceId in @(Get-WorkspaceHistoryIds)) {
            if ($workspaceId -eq $ClosedWorkspaceId -or -not $liveWorkspaceIds.Contains($workspaceId)) { continue }
            if (-not $orderedWorkspaceIds.Contains($workspaceId)) { $orderedWorkspaceIds.Add($workspaceId) }
        }
        Save-WorkspaceHistoryIds -WorkspaceIds $orderedWorkspaceIds.ToArray()
        $orderedWorkspaceIds.ToArray()
    }
}

function Get-PluginEventWorkspaceId {
    if (-not $env:HERDR_PLUGIN_EVENT_JSON) {
        return ''
    }
    try {
        $eventPayload = $env:HERDR_PLUGIN_EVENT_JSON | ConvertFrom-Json
        if ($eventPayload.PSObject.Properties['data'] -and $eventPayload.data.PSObject.Properties['workspace_id']) {
            return [string] $eventPayload.data.workspace_id
        }
        if ($eventPayload.PSObject.Properties['workspace_id']) {
            return [string] $eventPayload.workspace_id
        }
    } catch {
        throw "Sesh for Windows could not parse HERDR_PLUGIN_EVENT_JSON: $($_.Exception.Message)"
    }
    ''
}

function Sync-WorkspaceHistory {
    $snapshot = Get-HerdrSessionSnapshot
    [void] (Update-WorkspaceHistory -FocusedWorkspaceId ([string] $snapshot.focused_workspace_id))
}

function Switch-ToLastHerdrWorkspace {
    $snapshot = Get-HerdrSessionSnapshot
    $currentWorkspaceId = [string] $snapshot.focused_workspace_id
    $workspaceHistory = @(Update-WorkspaceHistory -FocusedWorkspaceId $currentWorkspaceId)
    $previousWorkspaceId = $workspaceHistory | Where-Object { $_ -ne $currentWorkspaceId } | Select-Object -First 1
    if (-not $previousWorkspaceId) {
        return
    }

    [void] (Invoke-HerdrJsonCommand -ArgumentList @('workspace', 'focus', [string] $previousWorkspaceId))
    [void] (Update-WorkspaceHistory -FocusedWorkspaceId ([string] $previousWorkspaceId))
}

switch ($Command) {
    'open-picker' {
        [void] (Invoke-HerdrJsonCommand -ArgumentList @(
            'plugin', 'pane', 'open', '--plugin', $script:PluginId, '--entrypoint', 'picker', '--placement', 'overlay'
        ))
    }
    'picker' { Show-SeshWindowsPicker }
    'last' { Switch-ToLastHerdrWorkspace }
    'list' {
        $entries = @(Get-SeshWindowsEntries)
        ConvertTo-Json -InputObject $entries -Depth 10
    }
    'sync-history' { Sync-WorkspaceHistory }
    'track-focus' {
        $workspaceId = Get-PluginEventWorkspaceId
        if (-not $workspaceId) { $workspaceId = [string] (Get-HerdrSessionSnapshot).focused_workspace_id }
        [void] (Update-WorkspaceHistory -FocusedWorkspaceId $workspaceId)
    }
    'remove-closed' {
        $workspaceId = Get-PluginEventWorkspaceId
        [void] (Update-WorkspaceHistory -ClosedWorkspaceId $workspaceId)
    }
}
