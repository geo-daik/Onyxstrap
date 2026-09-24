# Installing Onyxstrap

## Requirements

- Windows 10 or newer
- The [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0/runtime) (the released `Onyxstrap.exe` is framework-dependent)

## Installing

1. Download `Onyxstrap.exe` from the releases page.
2. Run it. The first launch is the installer: it copies itself into `%LOCALAPPDATA%\Onyxstrap` and registers itself as the handler for Roblox's launch protocols (`roblox-player://` and friends).
3. That's it. From now on, launching any game from the Roblox website goes through Onyxstrap.

> **Heads-up:** registering the protocols means Onyxstrap replaces whatever bootstrapper handled them before (the stock launcher, Bloxstrap, Voidstrap, ...). Your other bootstrapper isn't removed — run its installer again if you want it back.

## The launch menu

Launching `Onyxstrap.exe` directly (for example from the Start Menu shortcut) opens the launch menu, where you can start Roblox or Studio without going through the website, and open settings.

## Updating

Onyxstrap keeps Roblox itself up to date automatically every launch. To update Onyxstrap itself, download the newer `Onyxstrap.exe` and run it — it will offer to upgrade the installed copy.

## Uninstalling

- Run `Onyxstrap.exe -uninstall`, or use *Add or Remove Programs* in Windows Settings, or the uninstall button in Onyxstrap's own settings page.
- Uninstalling removes the `%LOCALAPPDATA%\Onyxstrap` folder (including your saved Roblox copy) and the protocol registration.

> Saved accounts live in `Accounts.json` inside the install folder and are removed with it. That's safe — removing an account from Onyxstrap never signs you out anywhere, and deleting the file doesn't log the accounts out of roblox.com either.
