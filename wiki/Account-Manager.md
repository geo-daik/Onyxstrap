# Onyxstrap 1.3.1 — Account switcher repairs

> **Experimental — known issues:** The account switcher still has problems and may not sign in or switch accounts correctly. Check the account shown in Roblox after launching. If switching fails, open Roblox and sign in normally.

## What changed
- Restored Accounts with Add account, Sign in again, Launch selected, and Remove.
- Saved sessions are encrypted with Windows DPAPI for the current Windows user. Vault writes use atomic replacement and a cross-process lock; corrupt vaults are preserved.
- Reauthentication and launch verify the immutable Roblox user ID. Saved labels are never written into Roblox identity data.
- Authentication failures cancel launch. Ticket replacement preserves the protocol version and all original join parameters.
- Cookie updates replace the existing session and remove duplicates. Verification decrypts the prepared data instead of searching ciphertext for plaintext.
- Session updates support only the known version-1 encrypted cookie format. Missing or unsupported formats stop the launch. No appStorage identity fields are edited.
- A failed process launch restores the prior encrypted session. Concurrent switches are blocked; external changes preserve the recovery backup instead of being overwritten.
- Switching requires Roblox to be closed and disallows administrator-mode launches.
- Login navigation checks HTTPS hostnames, blocks popups/downloads, disables password/autofill saving, and uses a separate temporary profile for each login. Closing cancels requests, stops polling, clears cookies, disposes the browser, and retries profile deletion. Windows locks can still prevent profile deletion; this is logged.
- Legacy per-account FastFlags are retained as data but are not automatically applied. Normal launch uses the current Roblox session.


## Compatibility
Saved-account launch is experimental. Real-client authentication has not yet been verified. Only synthetic session tests have passed.
