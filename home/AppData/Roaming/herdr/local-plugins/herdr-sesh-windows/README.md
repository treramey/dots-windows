# Sesh for Windows

A native Windows Herdr plugin inspired by
[`adriankarlen/herdr-sesh-minimal`](https://github.com/adriankarlen/herdr-sesh-minimal).

The picker combines:

- live Herdr workspaces;
- optional `[[session]]` entries from `~/.config/sesh/sesh.toml`; and
- recent directories from zoxide.

It uses `gum filter` in a full Herdr overlay and tracks workspace focus events so the
`last` action toggles between the two most recently used workspaces.

Dependencies: `gum`, `zoxide`, and PowerShell 7. `fzf` is used as a fallback
when Gum is unavailable.

## Link locally

```powershell
herdr plugin link "$env:APPDATA\herdr\local-plugins\herdr-sesh-windows"
```

## Actions

- `treramey.sesh-windows.open-picker`
- `treramey.sesh-windows.last`

The managed Herdr config binds these to `prefix+f` and `prefix+l`.
