# Onyxstrap

> An alternative bootstrapper for Roblox with a built-in account manager, forked from [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap).

Onyxstrap is a free, open-source launcher for Roblox on Windows 10+. It does everything Bloxstrap does, and adds its own account management features on top.

## Features

Everything from Bloxstrap:

- **FastFlag editing** - a friendly GUI for Roblox engine configuration flags
- **Mod persistence** - custom files and tweaks survive Roblox updates
- **Multi-instance launching**, Discord Rich Presence, custom bootstrapper styles and icons, and more

Plus Onyxstrap's own:

- **Account Manager** - save your Roblox accounts inside Onyxstrap and launch straight into the one you pick. Each account gets its **own FastFlag set** (your flag edits follow the active account), and named **presets** (e.g. "Max FPS", "Vanilla") can be saved once and applied to any account.
  - Switching accounts happens at launch: Onyxstrap mints a one-time authentication ticket from the account's session token and hands it to the Roblox client - the same mechanism the Roblox website uses. Nothing is injected into the client process.
- **Privacy-first defaults** - no analytics.

## How accounts are stored

Accounts are saved by their `.ROBLOSECURITY` session token rather than your password (Roblox logins use CAPTCHA/2FA, which a launcher can't and shouldn't automate). Tokens are:

- encrypted at rest with **Windows DPAPI**, bound to your Windows user account - no plaintext ever touches disk
- only ever sent to **roblox.com** itself, to verify the token and mint launch tickets
- treated as password-grade secrets: anyone with a token can access the account, so never share them

> Switching inside a *running* Roblox session would require injecting into the client, which Roblox's anti-cheat detects. Onyxstrap switches between sessions instead - pick an account, and the next launch boots into it.

## Documentation

Help lives in the [wiki](wiki/Home):

- [Installing Onyxstrap](wiki/Installing-Onyxstrap.md)
- [Account Manager](wiki/Account-Manager.md) — saving accounts, switching, token security
- [Engine Settings (FastFlags)](wiki/Engine-Settings-FastFlags.md) — per-account flag sets and presets
- [Mods & Customization](wiki/Mods-and-Customization.md) — bootstrapper styles, the game-window rebrand
- [Troubleshooting](wiki/Troubleshooting.md)
- [Security & Privacy](wiki/Security-and-Privacy.md)

## Building from source

Requires the .NET SDK (8.0+ works; the project targets `net6.0-windows`):

```
git clone --recursive <this repo>
dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false .\Onyxstrap\Onyxstrap.csproj
```

The output is a single `Onyxstrap.exe` that acts as both the installer and the launcher. Building with the .NET 8 SDK uses `global.json`'s `rollForward: latestSdk`.

## Credits & license

Onyxstrap is based on **Bloxstrap** by [pizzaboxer](https://github.com/pizzaboxer) and its contributors - huge thanks to them. Bloxstrap's upstream repo lives at [bloxstraplabs/bloxstrap](https://github.com/bloxstraplabs/bloxstrap).

Both Bloxstrap and Onyxstrap are released under the [MIT license](LICENSE).

Onyxstrap is a community project and is not affiliated with Roblox Corporation. As with any launcher that touches engine flags, customizing the client beyond supported settings is technically against the Roblox Terms of Service - use your judgement.
