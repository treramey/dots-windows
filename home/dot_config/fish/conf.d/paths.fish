# User paths
# conf.d files load before config.fish activates Mise. Expose the shims here so
# tool initializers such as Starship and Zoxide can run during conf.d startup.
fish_add_path "$HOME/.local/share/mise/shims"
fish_add_path "$HOME/.dotfiles"
fish_add_path "$HOME/.local/bin"
fish_add_path "$HOME/.local/scripts"
fish_add_path "$HOME/.opencode/bin"
fish_add_path "$HOME/.config/opencode/scripts"
fish_add_path "$HOME/.bun/bin"
fish_add_path "$HOME/.spicetify"
fish_add_path "$HOME/.dotnet/tools"
fish_add_path "$HOME/.aspire/bin"
fish_add_path "/Applications/Ghostty.app/Contents/MacOS"
