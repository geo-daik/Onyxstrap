# Mods & Customization

## File mods

Anything you place in `Modifications\` inside Onyxstrap's install folder gets copied over the Roblox install at every launch — and restored/cleaned up automatically when Roblox updates, so mods never break an update.

Examples: custom textures (in the right `content\...` subfolder), custom fonts (use the font option in settings, it's safer), sounds.

## Onyx loading screen

**Settings → Mods → Onyx loading screen** *(off by default)* replaces the Roblox loading screen background with a deep onyx gradient with a violet glow — in both light and dark client themes. The violet loading spinners are included too. It works by shipping our own version of the loading textures in the `content\textures\loading\` folder through the mods pipeline, so it survives Roblox updates and disappears cleanly when toggled off.

Combined with the cutscene bootstrapper and the gem taskbar icon, the whole launch flow is Onyx-branded.

## Bootstrapper styles

**Settings → Behaviour → Bootstrapper style** picks what you see while Onyxstrap prepares Roblox:

- **OnyxDialog** *(default)* — Onyxstrap's own cutscene: the animated gem, wordmark and a slim progress bar.
- **ByfronDialog** — a replica of the modern Roblox loading box.
- plus the classic Roblox-era styles, Fluent, and fully custom XML bootstrappers.

The style's title and icon are configurable on the same page.

## Game window rebrand

**Settings → Onyxstrap → Rebrand the game window icon** *(on by default)* swaps the icon of the Roblox window and taskbar to the Onyxstrap gem while you play.

How it works, without the magic words: Onyxstrap's session watcher sends a standard Windows message (`WM_SETICON`) to the game's window telling it to use a different icon. No client files are modified, nothing is injected into the process, and the change evaporates when the game closes. It covers every Roblox instance started during the session, including multi-instance launches.

## Launch menu and shortcuts

- The **launch menu** (`Onyxstrap.exe` with no arguments) offers quick Roblox/Studio/settings launches.
- **Settings → Shortcuts** can create desktop/start-menu shortcuts for specific actions.
