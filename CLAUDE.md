# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GenMate.PluginInstaller is a WPF (.NET 10) desktop app that installs the GenMate AutoCAD plugin. It is part of the larger GenMate ecosystem (see parent `GenMate/CLAUDE.md` for full architecture).

## Build & Run

Development happens in WSL, but the app must launch via the **Windows** .NET runtime (WPF requires the Windows desktop runtime).

```bash
./build.sh              # build only (Debug)
./build.sh -l           # build and launch via Windows dotnet
./build.sh -c Release -l  # release build and launch
./build.sh -r           # restore packages before building
```

The build script handles both steps: `dotnet build` in WSL, then launches via `/mnt/c/Program Files/dotnet/dotnet.exe` when `-l` is passed.

Manual equivalent:
```bash
dotnet build GenMate.PluginInstaller.csproj
"/mnt/c/Program Files/dotnet/dotnet.exe" bin/Debug/net10.0-windows/GenMate.PluginInstaller.dll
```

## UI Framework & Theming

- **MaterialDesignThemes** (Material Design In XAML Toolkit) for WPF controls and theming
- Theme: Light base, DeepPurple primary, Amber secondary
- **Montserrat** font family bundled in `Resources/Fonts/`, used globally
- Color palette mirrors the GenMate WebApp's MudBlazor colors (e.g. `PrimaryBrush` = `#594AE2`)
- Global styles defined in `Resources/MaterialDesignResources.xaml` — all buttons default to outlined primary style

### Available Custom Styles

Defined in `MaterialDesignResources.xaml` and used via `Style="{StaticResource ...}"`:
- `SuccessButton`, `WarningButton`, `ErrorButton` — colored outlined button variants
- `IconButton` — circular icon button (32x32)
- `ClickableCardButton` — card that acts as a button with hover effects
- `DenseCard` — flat card with black outline, no elevation
