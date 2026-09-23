# Disable greeting
set fish_greeting 

# Set Editor to neovim
set -gx EDITOR 'nvim'

# Set neovim as the program to open manpages
set -gx MANPAGER 'nvim +Man!'

# Add dotfiles directory to PATH for 'dot' command
fish_add_path ~/.dotfiles

mise activate fish | source

# User launchers wrap selected Mise-managed tools with platform integration.
# Keep them ahead of the tool install directories that Mise just prepended.
fish_add_path --move --path "$HOME/.local/bin"

# Omarchy's vendor envs.fish sets a basic FZF_DEFAULT_OPTS after user conf.d.
# Re-apply the dotfiles fzf theme here so tab completion/fzf stay monochrome.
set -l fzf_theme ~/.config/fish/conf.d/fzf-theme.fish
if test -r $fzf_theme
    source $fzf_theme
end
