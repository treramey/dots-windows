# dots-windows

Windows 11 host and Ubuntu WSL dotfiles, managed with
[chezmoi](https://chezmoi.org).

- **Windows host** — PowerShell 7, Windows Terminal, WezTerm + Herdr,
  Neovide, Git, and the pi agent, deployed from `home/` by chezmoi.
- **Ubuntu WSL** — fish, tmux, mise runtimes, and development CLIs, deployed
  by a second checkout of this repo inside WSL.
- **Neovim** — shared with the Linux machines via
  [treramey/nvim](https://github.com/treramey/nvim).

Start with [WINDOWS.md](WINDOWS.md) for the platform design, first-time
install, and the update workflow.
