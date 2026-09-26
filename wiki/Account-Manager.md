# Account Manager

Onyxstrap's account manager lets you save your Roblox accounts and launch straight into the one you pick — with each account keeping its **own FastFlag set**.

## Adding an account

**The easy way — one-time login (recommended):**

1. Open **Onyxstrap → Settings → Accounts → Add account**.
2. Click **Log in with Roblox**. An embedded browser opens on the official roblox.com login page.
3. Log in once — password, 2FA and CAPTCHA are all handled by Roblox's own page.
4. Onyxstrap detects the finished login automatically, verifies the session and saves the account. Done.

The embedded browser uses a throwaway profile that is wiped after each login: your password is never seen or stored by Onyxstrap, and no plaintext session data is left behind.

**The manual way — paste a token:**

If the embedded browser isn't available (WebView2 runtime missing) or you prefer not to log in inside Onyxstrap:

1. Log into [roblox.com](https://www.roblox.com) in your own browser (with 2FA if you use it).
2. Open your browser's developer tools (**F12**) → **Application** (Edge/Chrome) or **Storage** (Firefox) → **Cookies** → `https://www.roblox.com`.
3. Copy the value of the **`.ROBLOSECURITY`** cookie — it's a long `|`-separated string.
4. Paste it into Onyxstrap and click **Add**.

Onyxstrap verifies the token with roblox.com, auto-fills the account's display name, user ID and avatar, and stores it. The current FastFlag set is copied as the new account's own starting set.

> **Why tokens instead of passwords?** Roblox logins use CAPTCHA and 2FA challenges that a launcher can't (and shouldn't) automate. A session token is what your browser holds after logging in — saving it gives the same "one click into the account" experience reliably. Treat the token exactly like a password: anyone who has it can access the account.

## Switching accounts

- Open the **Accounts** page and use **Launch** next to an account, or
- Set an account **Active** — every subsequent launch (including website launches) boots into it.

Switching takes effect on the next Roblox launch: Onyxstrap mints a one-time **authentication ticket** from that account's session token and hands it to the Roblox client — the same mechanism the Roblox website uses when you press Play. Nothing is injected into the client.

> **Why not switch while the game is running?** That would require injecting into the Roblox process, which its anti-cheat (Byfron/Hyperion) actively detects and punishes. Onyxstrap switches between sessions instead — it's one click and a few seconds.

## Per-account FastFlags and presets

- Every account has its **own** FastFlag set. The engine settings editor edits whichever account is *active*, and your edits are saved to that account automatically.
- **Presets** are named snapshots of a flag set ("Max FPS", "Vanilla", ...) saved from the Accounts page and applicable to **any** account. Applying a preset overwrites that account's current flags, so save a preset of your current setup first if you want to keep it.

## Managing accounts

- **Rename** — change the display name (this is local only; it doesn't affect your Roblox username).
- **Remove** — forgets the account locally. It does *not* sign that account out anywhere.
- **Update token** (circular-arrow button) — if a session token expired, paste a fresh one and the account is re-authenticated in place; no need to remove and re-add it.
- If a token stops working (you logged out everywhere, Roblox invalidated the session, or you changed IP in some cases), remove the account and add it again with a fresh token.

## How your tokens are stored

- Tokens are encrypted with **Windows DPAPI** bound to your Windows user account, and saved to `Accounts.json` in Onyxstrap's install folder. There is no plaintext anywhere on disk.
- Tokens are only ever sent to **roblox.com** endpoints (token verification, thumbnail lookup, and ticket minting). See [Security & Privacy](Security-and-Privacy).
