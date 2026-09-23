# Monochrome previews for fifc (fuzzy tab completion).
# FZF can use the terminal fg/bg, but preview tools must also avoid hard-coded colors.
set -g fifc_bat_opts --style=plain --color=never
set -g fifc_exa_opts --color=never
set -g fifc_ls_opts --color=never
set -g fifc_procs_opts --color=never

function __dotfiles_fifc_preview_opt_mono -d "Preview fifc option docs without ANSI colors"
    set -l regex "(?s)^(\-+[^\n]+)*$fifc_candidate([^\-\w\.]([^\.\n]|\.{2,}|\w+\.)*|)\n{1,2}.*?(^(\-+[^\n]+|\w+))"
    set -l regex_replace '^\h+(\-+[^\n]+.*)'
    set -l cmd (string match --regex --groups-only -- '(^|\h+)(\w+) ?-*$' $fifc_commandline)

    set -l out (man $cmd 2>/dev/null | string replace -r $regex_replace '$1' \
        | begin
            if type -q rg
                rg --multiline $regex
            else if type -q pcre2grep
                pcre2grep --multiline $regex
            else
                pcregrep --multiline $regex
            end
        end \
        # Remove last line as it should describe the next option.
        | awk 'n>=1 { print a[n%1] } { a[n%1]=$0; n=n+1 }' \
        | string trim \
    )

    # Fallback to fish description if there is no man page.
    if test -z "$out"
        echo "$fifc_desc"
        return
    end

    printf '%s\n' $out[1]
    if test (count $out) -gt 1
        printf '\n'
        printf '%s\n' $out[2..-1]
    end
end

# fifc's built-in option preview uses explicit green/white set_color calls.
# Register this ordered rule ahead of the plugin's unordered built-in rule.
if set -q _fifc_launched_by_fzf
    fifc \
        -O 1 \
        -n 'test "$fifc_group" = "options"' \
        -p __dotfiles_fifc_preview_opt_mono \
        -o _fifc_open_opt
end
