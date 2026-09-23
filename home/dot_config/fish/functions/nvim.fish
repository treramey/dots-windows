function nvim --wraps nvim --description 'nvim wrapper that cd-on-exit when a worktree hook writes a path'
    set -l cd_file (mktemp)
    NVIM_CD_ON_EXIT=$cd_file command nvim $argv
    set -l exit_code $status
    if test -s "$cd_file"
        set -l target (cat "$cd_file")
        if test -d "$target"
            cd "$target"
        end
    end
    rm -f "$cd_file"
    return $exit_code
end
