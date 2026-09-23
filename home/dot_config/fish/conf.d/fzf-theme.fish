# FZF theme that follows the terminal/system palette.
# Monochrome on purpose: use the active terminal fg/bg plus reverse/underline.
# This keeps fzf readable across Omarchy/Ghostty light and dark themes.
set -l fzf_system_colors \
    --color=bw,fg:-1,bg:-1,preview-fg:-1,preview-bg:-1 \
    --color=selected-fg:-1:reverse,selected-bg:-1,selected-hl:-1:reverse:underline \
    --color=current-fg:-1:reverse,current-bg:-1,current-hl:-1:reverse:underline \
    --color=hl:-1:underline,query:-1,prompt:-1,pointer:-1,marker:-1 \
    --color=info:-1:dim,spinner:-1,header:-1:dim,gutter:-1 \
    --color=border:-1,preview-border:-1,separator:-1,scrollbar:-1

set -gx FZF_DEFAULT_OPTS \
    --cycle \
    --layout=default \
    --height=90% \
    --preview-window=wrap \
    --marker='*' \
    --no-bold \
    $fzf_system_colors

# fzf.fish appends these per-widget opts after FZF_DEFAULT_OPTS, so this forces
# Ctrl-r history to match even if an older FZF_DEFAULT_OPTS is inherited.
set -g fzf_history_opts $fzf_system_colors

# fzf.fish: directory preview with eza
set fzf_preview_dir_cmd eza --all --icons --group-directories-first --git --color=never

# fzf.fish: show hidden files, limit depth
set fzf_fd_opts --hidden --max-depth 5

# fzf.fish: pipe git diffs through delta
set fzf_diff_highlighter delta --paging=never --width=20
