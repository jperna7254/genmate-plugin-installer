#!/usr/bin/env bash
set -euo pipefail

configuration="Debug"
launch=false
restore=false
verbosity="minimal"

while [[ $# -gt 0 ]]; do
    case "$1" in
        -c|--configuration) configuration="$2"; shift 2 ;;
        -l|--launch)        launch=true; shift ;;
        -r|--restore)       restore=true; shift ;;
        -v|--verbosity)     verbosity="$2"; shift 2 ;;
        *)
            echo "Unknown option: $1" >&2
            echo "Usage: build.sh [-c|--configuration <config>] [-l|--launch] [-r|--restore] [-v|--verbosity <level>]" >&2
            exit 1
            ;;
    esac
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if $restore; then
    echo "Restoring packages..."
    dotnet restore "$script_dir/GenMate.PluginInstaller.csproj"
fi

echo "Building ($configuration)..."
dotnet build "$script_dir/GenMate.PluginInstaller.csproj" \
    --configuration "$configuration" \
    --verbosity "$verbosity"

if $launch; then
    win_dotnet="/mnt/c/Program Files/dotnet/dotnet.exe"
    if [[ ! -f "$win_dotnet" ]]; then
        echo "Error: Windows dotnet not found at $win_dotnet" >&2
        echo "Install the .NET Desktop Runtime for Windows: https://dotnet.microsoft.com/download" >&2
        exit 1
    fi

    dll="$script_dir/bin/$configuration/net10.0-windows/GenMate.PluginInstaller.dll"
    echo "Launching $dll..."
    "$win_dotnet" "$dll"
fi
