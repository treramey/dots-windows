# AGENTS.md

Windows 11 host and Ubuntu WSL dotfiles managed with chezmoi. This repo is
`treramey/dots-windows`; the Arch/macOS machines use the separate
`treramey/.dotfiles` repo. See [WINDOWS.md](WINDOWS.md) for the full platform
design, first-time install, and update workflow.

## Layout

- `home/` — chezmoi source directory (`.chezmoiroot` points here)
- `home/dot_config/` → `~/.config`; on Windows only `git`, `wezterm`, and
  `ohmyposh` are applied, the POSIX trees are for WSL
- `home/AppData/` — Windows-native config: Windows Terminal, herdr, neovide,
  and the script that symlinks `~/AppData/Local/nvim`
- `home/Documents/PowerShell/` — PowerShell 7 profile
- `home/dot_pi/` — pi agent config materialized to `~/.pi` on Windows
- `home/dot_agents/` — tracked snapshot of the shared agent skills (`~/.agents`)
- `home/.chezmoidata/packages.json` — Scoop / Winget / APT / Mise package
  declarations (package ownership rules are in WINDOWS.md)
- `install-windows.ps1` / `install-ubuntu.sh` — host and WSL bootstraps, both
  safely rerunnable

## Applying changes

1. Edit files in this repo (source dir is `~/.dotfiles/home`).
2. `chezmoi apply` (or target one path: `chezmoi apply ~/.config/git/config`)
3. Reload the affected app (PowerShell: `. $PROFILE`; Windows Terminal and
   WezTerm hot-reload; herdr needs a restart)

The machine is the source of truth. After changing config directly on the
machine, sync it back into the repo:

- `chezmoi re-add <path>` for managed plain files
- `chezmoi add <path>` for files not yet managed
- `.tmpl` templates cannot be re-added; update them by hand and verify with
  `chezmoi diff`

## Platform gating

- `home/.chezmoiignore.tmpl` gates POSIX-only trees off Windows and
  Windows-native locations off Linux/macOS.
- `home/.chezmoitemplates/is-wsl` detects the WSL host for scripts.
- `home/dot_config/git/config.tmpl` branches per-OS: Windows uses the Windows
  OpenSSH sshCommand, `autocrlf`, the Bitwarden signing key, and wincred;
  Linux keeps the mise .NET paths and `~/.ssh/id_ed25519.pub` signing.

## Neovim

`home/dot_config/nvim` is a git submodule pinned to
[treramey/nvim](https://github.com/treramey/nvim) main; `~/AppData/Local/nvim`
symlinks to it (created by `home/AppData/Local/symlink_nvim.tmpl`), so Neovim
edits happen directly in the repo directory.

The Windows-only adaptations (MinGW CC fix for tree-sitter, Program Files
dotnet, pwsh terminals, Neovide guifont and transparency guards) live as
**uncommitted working-tree changes inside the submodule** until they are
upstreamed to treramey/nvim. Do not commit inside the submodule or reset it
without coordinating with that repo. `run_before_00-init-nvim-submodule`
scripts (`.ps1` on Windows, `.sh` on Linux) initialize the submodule on every
apply, so a fresh clone heals itself.

## Pi and agent skills

- `~/.pi` is materialized from `home/dot_pi` by chezmoi on Windows; runtime
  state (`node_modules`, `archive`, `chrome-cdp-profile`,
  `web-search-cache`, `todos`) is ignored by both chezmoi and git.
- `~/.agents` is not chezmoi-managed. `home/dot_agents/` is a tracked
  snapshot mirrored from the machine (`robocopy /MIR`).

## Herdr

Herdr is the Windows terminal multiplexer (WezTerm launches `herdr.exe`).
Its live config is templated at `home/AppData/Roaming/herdr/config.toml.tmpl`;
the custom sesh-windows plugin is tracked under
`home/AppData/Roaming/herdr/local-plugins/`. Runtime files (sessions, logs,
sockets, plugins.json) are never tracked.

## Theme

Rose Pine across Windows and WSL: Windows Terminal, WezTerm, ohmyposh, bat,
eza, delta, fzf. Only Omarchy machines follow Omarchy's generated theme.
