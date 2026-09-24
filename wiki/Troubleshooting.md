# Troubleshooting

## First stop: the logs

Everything Onyxstrap does is logged to `Logs\` inside the install folder (`%LOCALAPPDATA%\Onyxstrap\Logs`). One file per launch. If something fails, the reason is almost always in there.

## Launch problems

**"Roblox failed to launch" / stuck on the loading screen**

- Check the latest log for a failed join. Retrying usually fixes one-off hiccups.
- If you use a VPN/proxy, try without it — join requests are session-bound.

**The website launches the wrong bootstrapper**

The `roblox-player://` protocol is claimed by one program at a time. Whichever bootstrapper *installed itself last* owns it. Run Onyxstrap's installer again (just run the exe from the install folder) to reclaim it.

**Windows SmartScreen warns about Onyxstrap**

Our builds aren't code-signed, so Windows shows "unknown publisher" on first run. Choose *More info → Run anyway* — or build from source yourself.

## Account switching

**Launched into the wrong account / no switch happened**

- Is the account **Active** on the Accounts page, or did you press **Launch** on it? Switching only happens through an account pick.
- A launch without a game (opening the app) may not switch — join a place.

**"Invalid or expired token" when adding an account**

The `.ROBLOSECURITY` cookie was copied incompletely or the session is gone. Log in again, copy the *full* value (it starts and ends with `|`), and retry.

**Account stopped launching into the right session**

Tokens can be invalidated (logout-everywhere, long inactivity, some IP changes). Use the **Update token** button on the account card (circular arrow) to paste a fresh token without removing the account.

## FastFlags / mods not applying

- Make sure **Use FastFlag manager** is on (Engine Settings).
- Remember most arbitrary flags are allowlisted out by Roblox — UI presets still apply.
- Check the file actually lands in `Versions\<version>\ClientSettings\ClientAppSettings.json` after a launch.

## Self-updates

This fork does not pull updates from Bloxstrap's releases. Its update check points at Onyxstrap's own releases; until that repository is live, the check simply finds nothing and Onyxstrap keeps working — update manually by running a newer exe.
