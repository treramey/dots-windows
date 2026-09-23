# Windows 11 and WSL2 setup

Windows is intentionally a thin host. Windows-native applications stay on
Windows, while development tools, repositories, shells, and Neovim run inside
Ubuntu on WSL2.

```text
Windows 11
├── Outlook, Teams, browsers, Rider, and Visual Studio
├── Windows Terminal, PowerShell 7, Neovide, and Bitwarden
└── WSL2 / Ubuntu
    ├── ~/src
    ├── Neovim and its configuration
    ├── Git, Mise, language runtimes, and development CLIs
    ├── tmux or Zellij
    └── AI and other terminal tools
```

Keep repositories in the WSL Linux filesystem, such as `~/src`, rather than
under `/mnt/c`. This gives Git, filesystem watchers, package managers, and
Neovim normal Linux performance and semantics.

## Package ownership

Package declarations live in
[`home/.chezmoidata/packages.json`](home/.chezmoidata/packages.json).

| Manager | Responsibility |
| --- | --- |
| Scoop | Portable Windows host tools and applications, including PowerShell 7, Neovide, Windows Terminal, and `npiperelay` |
| Winget | Windows applications better maintained by their vendor installer, currently Bitwarden Desktop |
| APT | Ubuntu system libraries and tools that integrate with the operating system, including Fish, Git, `socat`, and tmux |
| Mise | Pinned development runtimes and versioned CLIs inside WSL, including Node, pnpm, .NET, Go, Python, Rust, Neovim, ripgrep, and fzf |
| Chezmoi | Configuration deployment and repeatable WSL package reconciliation |

Do not add WSL development tools to the Windows Scoop lists. Add versioned
developer tools to `packages.mise.common` or `packages.mise.wsl`; reserve APT
for operating-system packages and dependencies.

## First-time installation

### 1. Install WSL2

From an elevated Windows PowerShell terminal:

```powershell
wsl --install --distribution Ubuntu
```

Restart Windows when requested, start Ubuntu, and finish creating the Linux
user account. WSL should use systemd by default on current Ubuntu releases.

### 2. Provision the Windows host

From a Windows copy of this repository, inspect the plan and run the host
bootstrap:

```powershell
Set-Location $HOME\.dotfiles
.\install-windows.ps1 -Plan
.\install-windows.ps1
```

The script:

1. Reads the Windows package lists from `home/.chezmoidata/packages.json`.
2. Installs Scoop and the configured Scoop buckets and packages.
3. Installs Bitwarden Desktop through Winget.
4. Initializes and applies the Windows Chezmoi configuration.
5. Checks the Bitwarden SSH-agent pipe and the Ubuntu WSL environment.

Useful optional switches are:

```powershell
.\install-windows.ps1 -SkipContainerInstall
.\install-windows.ps1 -SkipFontInstall
.\install-windows.ps1 -SkipWslCheck
```

The Windows Chezmoi profile intentionally ignores the Linux `.config` and
`.local` trees, except for the native WezTerm configuration deployed to
`~/.config/wezterm`. PowerShell configuration is deployed to
`~/Documents/PowerShell/Microsoft.PowerShell_profile.ps1`.

Windows Terminal receives the managed Rose Pine scheme from
`~/AppData/Local/Microsoft/Windows Terminal/settings.json`. PowerShell 7 uses
Oh My Posh with the shared theme at `~/.config/ohmyposh/theme.toml`. WSL applications
also use the static Rose Pine palette; only an Omarchy installation follows
Omarchy's generated active theme.

### 3. Provision Ubuntu

Run the Ubuntu bootstrap inside WSL. It clones a separate Linux checkout to
`~/.dotfiles`, because Windows and WSL have different Chezmoi source and target
paths.

```bash
sudo apt-get update
sudo apt-get install --yes curl
curl -fsSL https://raw.githubusercontent.com/treramey/dots-windows/main/install-ubuntu.sh | bash
```

`install-ubuntu.sh` is intentionally only a bootstrap. It installs the minimum
packages needed for Git and Chezmoi, clones or reuses the Linux repository, and runs
`chezmoi apply`. The Chezmoi `run_onchange` script then:

- reconciles the Ubuntu APT package list;
- installs Mise and every pinned Mise tool;
- creates `~/src`;
- links the Neovim, Pi, and shared agent configuration directly to the
  repository checkout;
- enables the Bitwarden SSH bridge systemd user service.

The live workspace links are:

```text
~/.config/nvim -> ~/.dotfiles/home/dot_config/nvim
~/.pi          -> ~/.dotfiles/home/dot_pi
~/.agents      -> ~/.dotfiles/home/dot_agents
```

These paths are ignored by normal Chezmoi file copying so edits happen directly
in the repository.

## Neovim through Neovide

Neovim itself and all Neovim CLI dependencies run inside WSL. Neovide is the
Windows-native GUI frontend and connects to WSL with its `--wsl` option.

The managed PowerShell profile provides `v`, `vim`, and `nvim` aliases. With no
arguments, they open Neovide in the WSL `~/src` directory:

```powershell
v
```

The direct equivalent is:

```powershell
neovide --wsl
```

To create Windows shortcuts or file associations, set their command to the
installed `neovide.exe` with `--wsl`. The Neovim executable, plugins, LSP
servers, and configuration remain entirely inside Ubuntu.

Check that WSL provides Neovim with:

```powershell
wsl --distribution Ubuntu --exec sh -lc "mise which nvim"
```

## Windows browser bridge

WSL login shells set `BROWSER=wslview`. The managed `wslview` helper sends URLs
to the default Windows browser and converts absolute WSL paths to Windows paths
before opening them.

Examples inside WSL:

```bash
wslview https://github.com
gh browse
```

## Bitwarden SSH agent

Private SSH keys remain in Bitwarden Desktop on Windows. The bridge consists of:

```text
WSL application
  -> ~/.bitwarden-ssh-agent.sock
  -> socat
  -> npiperelay.exe
  -> \\.\pipe\openssh-ssh-agent
  -> Bitwarden Desktop
```

Scoop installs `npiperelay`, APT installs `socat`, and Chezmoi enables
`bitwarden-ssh-agent-bridge.service` in WSL. Fish and POSIX login shells set:

```bash
SSH_AUTH_SOCK="$HOME/.bitwarden-ssh-agent.sock"
```

On Windows:

1. Open and unlock Bitwarden Desktop.
2. Enable **Settings > SSH agent**.
3. Disable the Windows **OpenSSH Authentication Agent** service so it does not
   compete for the same named pipe.

The Windows bootstrap warns when either condition is not satisfied. To disable
the conflicting service from an elevated PowerShell terminal:

```powershell
Stop-Service ssh-agent -ErrorAction SilentlyContinue
Set-Service ssh-agent -StartupType Disabled
```

Verify the bridge inside WSL:

```bash
systemctl --user status bitwarden-ssh-agent-bridge.service
ssh-add -L
```

If the service was started before Bitwarden or `npiperelay` was ready, restart
it after unlocking Bitwarden:

```bash
systemctl --user restart bitwarden-ssh-agent-bridge.service
```

## Updating packages and configuration

Edit `home/.chezmoidata/packages.json` to add packages or change pinned Mise
versions. Then update the target environment:

```bash
cd ~/.dotfiles
git pull
chezmoi apply
```

Changes to the APT list, Mise declarations, or Bitwarden bridge automatically
change the `run_onchange` hash, causing WSL provisioning to run again. Normal
configuration-only changes are applied without reinstalling packages.

On Windows, pull the Windows checkout and rerun:

```powershell
Set-Location $HOME\.dotfiles
git pull
.\install-windows.ps1
```

Both bootstrap scripts are designed to be safely rerun.

## Troubleshooting

### Neovide cannot find Neovim

```bash
mise install
mise which nvim
```

Make sure `~/.local/share/mise/shims` is in `PATH`; the managed WSL `.profile`
and Fish environment add it automatically.

### Bitwarden socket is missing

```bash
systemctl --user status bitwarden-ssh-agent-bridge.service
journalctl --user --unit bitwarden-ssh-agent-bridge.service --since today
```

Confirm that Bitwarden is unlocked, its SSH agent is enabled, and Windows has
`npiperelay.exe` installed:

```powershell
scoop which npiperelay
```

### Re-run WSL reconciliation

Chezmoi normally runs provisioning only when its inputs change. To inspect or
reapply the current configuration:

```bash
chezmoi diff
chezmoi apply
```
