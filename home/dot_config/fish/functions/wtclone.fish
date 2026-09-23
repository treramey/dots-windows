# Clone a repository into a bare-root worktree layout.
function wtclone -d "Clone a repo with a .bare directory and initial worktree" -a repo directory branch
    if test -z "$repo"
        echo "Usage: "(status -u)" repo [directory] [branch]"
        return 1
    end

    if test -z "$directory"
        set directory (path basename (string trim --right --chars=/ "$repo"))
        set directory (string replace -r '\.git$' '' -- "$directory")
    end

    if test -e "$directory"
        echo "Directory already exists: $directory"
        return 1
    end

    mkdir -p "$directory"
    or return 1

    git clone --bare "$repo" "$directory/.bare"
    or return 1

    # Make normal remote branch refs available in the bare repo.
    git --git-dir="$directory/.bare" config remote.origin.fetch '+refs/heads/*:refs/remotes/origin/*'
    and git --git-dir="$directory/.bare" fetch origin
    or return 1

    if test -z "$branch"
        set branch (git --git-dir="$directory/.bare" symbolic-ref --quiet --short HEAD 2>/dev/null)
        set branch (string replace -r '^refs/heads/' '' -- "$branch")
    end

    if test -z "$branch"
        set branch main
    end

    git --git-dir="$directory/.bare" worktree add "$directory/$branch" "$branch"
    or return 1

    echo "Created bare-root worktree repo: $directory"
    echo "Initial worktree: $directory/$branch"
end
