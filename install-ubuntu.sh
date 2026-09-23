#!/usr/bin/env bash

set -euo pipefail

repository_url="${DOTFILES_REPOSITORY_URL:-https://github.com/treramey/dots-windows.git}"
default_repository_path="$HOME/.dotfiles"
if [[ -n "${BASH_SOURCE[0]:-}" ]]; then
    script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
    if [[ -d "$script_directory/.git" && -d "$script_directory/home" ]]; then
        default_repository_path="$script_directory"
    fi
fi
repository_path="${DOTFILES_REPOSITORY_PATH:-$default_repository_path}"

write_ubuntu_bootstrap_step() {
    printf '\n==> %s\n' "$1"
}

require_ubuntu_wsl() {
    if [[ ! -r /etc/os-release ]]; then
        printf 'Ubuntu WSL bootstrap platform check failed: /etc/os-release is missing.\n' >&2
        exit 1
    fi

    # shellcheck disable=SC1091
    source /etc/os-release
    if [[ "${ID:-}" != "ubuntu" ]]; then
        printf "Ubuntu WSL bootstrap platform check failed: expected Ubuntu, found '%s'.\n" "${ID:-unknown}" >&2
        exit 1
    fi

    if ! grep -qi microsoft /proc/sys/kernel/osrelease; then
        printf 'Ubuntu WSL bootstrap platform check failed: this script must run inside WSL.\n' >&2
        exit 1
    fi
}

install_ubuntu_bootstrap_packages() {
    write_ubuntu_bootstrap_step "Installing Ubuntu bootstrap packages"
    sudo apt-get update
    sudo apt-get install --yes ca-certificates curl git
}

install_ubuntu_chezmoi() {
    if command -v chezmoi >/dev/null 2>&1; then
        printf '    Chezmoi is already installed.\n'
        return
    fi

    write_ubuntu_bootstrap_step "Installing chezmoi"
    mkdir -p "$HOME/.local/bin"
    sh -c "$(curl -fsLS https://get.chezmoi.io)" -- -b "$HOME/.local/bin"
    export PATH="$HOME/.local/bin:$PATH"
}

checkout_ubuntu_dotfiles() {
    if [[ -d "$repository_path/.git" && -d "$repository_path/home" ]]; then
        printf '    Dotfiles repository is already available at %s.\n' "$repository_path"
        return
    fi

    if [[ -e "$repository_path" ]]; then
        printf "Ubuntu WSL bootstrap source conflict: '%s' exists but is not a dotfiles checkout.\n" "$repository_path" >&2
        exit 1
    fi

    write_ubuntu_bootstrap_step "Cloning Ubuntu dotfiles"
    git clone "$repository_url" "$repository_path"
}

initialize_ubuntu_dotfiles() {
    write_ubuntu_bootstrap_step "Applying Ubuntu WSL dotfiles and provisioning"
    chezmoi --source "$repository_path" init --no-tty "$repository_url"
    local chezmoi_source_path
    chezmoi_source_path="$(chezmoi --source "$repository_path" source-path)"
    chezmoi --source "$chezmoi_source_path" apply --no-tty
}

verify_ubuntu_dotfiles() {
    write_ubuntu_bootstrap_step "Verifying Ubuntu dotfiles"

    local nvim_path
    nvim_path="$(mise which nvim)"
    printf '    Neovim: %s\n' "$nvim_path"

    if ! fish -c 'command -q starship; and command -q zoxide; and functions -q z'; then
        printf 'Ubuntu WSL bootstrap verification failed: Fish did not initialize Starship and Zoxide.\n' >&2
        exit 1
    fi
    printf '    Fish initialized Starship and Zoxide.\n'
}

main() {
    require_ubuntu_wsl
    install_ubuntu_bootstrap_packages
    install_ubuntu_chezmoi
    checkout_ubuntu_dotfiles
    initialize_ubuntu_dotfiles
    verify_ubuntu_dotfiles

    printf '\nUbuntu WSL bootstrap complete. Future package changes apply through chezmoi update.\n'
    printf "Optional: make Fish your login shell with 'chsh -s /usr/bin/fish'.\n"
}

main "$@"
