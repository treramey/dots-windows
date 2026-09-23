function gum_filter
    set -l result (gum filter)
    if test -n "$result"
        commandline --insert "$result"
    end
    commandline -f repaint
end
