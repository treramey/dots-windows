# Open the wt switch picker; repaint so the prompt reflects the new worktree.
function __wt.switch -d "Open the wt switch worktree picker"
    wt switch
    commandline -f repaint
end
