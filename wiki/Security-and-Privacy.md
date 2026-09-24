# Security & Privacy

## What Onyxstrap stores

| Data | Where | Notes |
|---|---|---|
| App settings | `Settings.json` | plain JSON, no secrets |
| Saved accounts | `Accounts.json` | session tokens **DPAPI-encrypted**, bound to your Windows user |
| App state | `State.json` | version info, window state |
| FastFlags | `Modifications\ClientSettings\ClientAppSettings.json` | plain JSON |

There is no plaintext copy of any token on disk. DPAPI means only **your Windows user account** on **your machine** can decrypt them — copying `Accounts.json` to another PC or another user gets nothing.

## What leaves your PC

- **roblox.com only, for accounts:** verifying a token when you add an account, fetching the avatar thumbnail, and minting one-time launch tickets when you launch into an account. Your tokens are never sent anywhere else.
- **Roblox's own CDN:** downloading/updating the Roblox client, exactly like the official launcher.
- **Telemetry: off by default.** Upstream Bloxstrap ships with analytics on; this fork defaults it to *disabled*, and its analytics endpoint doesn't exist anyway.

## Threat model, honestly

- A `.ROBLOSECURITY` token is **as powerful as your password** (it bypasses 2FA). That's true when it sits in your browser, and true when Onyxstrap holds it encrypted. Anything running as *your Windows user* could in principle ask DPAPI to decrypt it.
- For that reason: don't share tokens or your `Accounts.json`, don't paste tokens into random "account switcher" tools, and if a token ever leaks, log out of the session on the Roblox website (Settings → Security → sign out of all sessions) to kill it.

## What Onyxstrap deliberately does NOT do

- No injection into the Roblox process (also why there's no in-game account switcher — see [Account Manager](Account-Manager)).
- No modification of `RobloxPlayerBeta.exe` or its signature. The game-window icon rebrand is cosmetic window messaging only.
- No outbound connections other than the ones listed above.
