# editor
alias v 'nvim'
alias code 'nvim'
alias lkjh 'nvim'
alias vimdiff 'nvim -d'
alias scratch 'nvim -c "setlocal buftype=nofile"'

# navigation
alias c 'clear'
alias x 'exit'
alias ... 'cd ../..'
alias .3 'cd ../../..'
alias .4 'cd ../../../..'
alias .5 'cd ../../../../..'

# eza
alias l 'eza -lh --icons=auto --color=always'
alias ls 'eza --icons=auto --color=always'
alias ll 'eza -lha --icons=auto --sort=name --group-directories-first --color=always'
alias ld 'eza -lhD --icons=auto --color=always'
alias lt 'eza --icons=auto --tree --color=always'

# git
alias g 'git'
alias lg 'lazygit'

# tmux
alias ks 'tmux kill-server'

# tools
alias grep 'grep --color=auto --exclude-dir={.bzr,CVS,.git,.hg,.svn,.idea,.tox}'
alias pbc 'pbcopy'
alias pbp 'pbpaste'
alias pn 'pnpm'
alias oc 'opencode'
complete -c oc -e
alias wr 'wrangler'
alias lc 'localcode'
alias rider 'open -a Rider'
alias howdy 'sh $HOME/.config/fetch.sh'
alias pray 'bun install'
