# Fish shell completions for dot command

# Main command completions
complete -c dot -f

# Commands
complete -c dot -n "__fish_use_subcommand" -a "init" -d "Initialize and install dotfiles"
complete -c dot -n "__fish_use_subcommand" -a "apply" -d "Apply dotfiles with chezmoi"
complete -c dot -n "__fish_use_subcommand" -a "update" -d "Update dotfiles and packages"
complete -c dot -n "__fish_use_subcommand" -a "diff" -d "Preview pending changes"
complete -c dot -n "__fish_use_subcommand" -a "status" -d "Show what's out of sync"
complete -c dot -n "__fish_use_subcommand" -a "add" -d "Add a file to the source state"
complete -c dot -n "__fish_use_subcommand" -a "edit" -d "Edit a target file or open dotfiles dir"
complete -c dot -n "__fish_use_subcommand" -a "managed" -d "List all managed files"
complete -c dot -n "__fish_use_subcommand" -a "cd" -d "Open shell in source directory"
complete -c dot -n "__fish_use_subcommand" -a "watch" -d "Auto-apply on changes (start|stop|status)"
complete -c dot -n "__fish_use_subcommand" -a "doctor" -d "Run diagnostics"
complete -c dot -n "__fish_use_subcommand" -a "link" -d "Install dot command globally"
complete -c dot -n "__fish_use_subcommand" -a "unlink" -d "Remove global dot command"
complete -c dot -n "__fish_use_subcommand" -a "help" -d "Show help message"

# Global options
complete -c dot -n "__fish_use_subcommand" -l "version" -d "Show version information"
complete -c dot -n "__fish_use_subcommand" -s "h" -l "help" -d "Show help message"

# watch subcommands
complete -c dot -n "__fish_seen_subcommand_from watch" -xa "start stop status"

# add: complete with file paths
complete -c dot -n "__fish_seen_subcommand_from add" -F

# edit: complete with chezmoi managed files
function __dot_managed_files
    chezmoi managed 2>/dev/null
end
complete -c dot -n "__fish_seen_subcommand_from edit" -xa "(__dot_managed_files)"

# apply: complete with chezmoi managed files
complete -c dot -n "__fish_seen_subcommand_from apply" -xa "(__dot_managed_files)"
