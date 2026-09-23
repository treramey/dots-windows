# Let the Mise-installed .NET SDK start on minimal Linux/WSL images that do not
# have libicu installed yet. Without this, `dotnet` aborts before it can run
# commands such as `dotnet tool install -g EasyDotnet`.
#
# Preferred full globalization support: install the system ICU package
# (`sudo apt install libicu78` on this image, `sudo pacman -S icu` on Arch) and
# remove this override.
set -gx DOTNET_SYSTEM_GLOBALIZATION_INVARIANT 1
