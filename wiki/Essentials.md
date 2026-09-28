# Onyxstrap 1.4.0 — Essentials

Open Essentials from the settings sidebar.

## Quick-launch favorites
Save up to 50 named games using a Roblox HTTPS /games/ link or a positive place ID. Click Play to launch through Onyxstrap using the current Roblox session. Duplicate games and invalid links are rejected. Click Save to persist additions/removals.

The generated link uses Roblox's documented deep-link format: https://create.roblox.com/docs/production/promotion/deeplinks . Input URLs are reduced to a validated numeric place ID; private-server parameters are not retained.

## Spotify edge snapping
Drag the player within 24 logical pixels of an edge of the visible Roblox area to snap with a 12-pixel margin. Corners snap on both axes. Positions are saved separately for each monitor; right/bottom anchors follow window resizing and compact/full player changes. The visible area respects the monitor work area. Turn snapping off in Essentials for free placement, then Save. Existing position files remain compatible.

## Preference backups
Export/import portable appearance, Spotify, Discord, color-profile, favorite, and selected app preferences. Import validates the format, version, colors, shortcuts, collection sizes, and game IDs before offering a confirmation. Click Save afterward, then reopen settings to refresh all controls.

Backups exclude account sessions, executable paths, integration commands, FastFlags, local theme/font files, and monitor positions. Import preserves excluded local settings. Files are limited to 1 MiB and unsupported fields are rejected. Export uses a temporary file followed by replacement.

## Account switcher notice
The Accounts page now prominently says “Experimental — known issues” and explains that signing in or switching can still fail. It advises checking the account shown in Roblox after launch.

## Validation
196 checks passed: 38 Essentials checks, 4 Essentials UI checks, 31 existing regression checks, 85 overlay/theme checks, and 38 synthetic account checks. Dark and light UI previews were rendered and inspected, including scrolling to backup controls. Build succeeded; the existing Wpf.Ui Source Link metadata warning remains. Microsoft Defender found no threats.

Favorites link parsing and launch handoff were tested without joining a live game. Monitor geometry and persistence were tested with synthetic monitor layouts; physically dragging across multiple monitors was not tested. Real-account switching remains experimental. The executable remains unsigned.
