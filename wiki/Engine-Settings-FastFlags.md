# Engine Settings (FastFlags)

FastFlags are Roblox's internal engine configuration variables. Onyxstrap gives you a friendly editor for them — plus some Onyxstrap-specific behaviour:

## The editor

Open **Onyxstrap → Settings → Engine Settings**. The simple view covers common presets (MSAA, textures, framerate behaviour); the JSON editor handles everything else.

- **Use the FastFlag manager** keeps the manager applying flags at launch. If you turn it off, Onyxstrap leaves `ClientSettings` untouched and you can manage the file by hand in the [modifications folder](Mods-and-Customization).
- Flags are written to `Modifications\ClientSettings\ClientAppSettings.json` in the install folder and copied into the installed Roblox version at every launch — so they **survive Roblox updates**.
- Most custom flags are ignored by the modern client due to Roblox's **flag allowlist** — presets in the UI still work, arbitrary flags may not.

## Onyx quick presets

The top of the Engine Settings page has one-click bundles applied to the active account's flag set:

- **Max FPS** — removes the internal FPS cap (`DFIntTaskSchedulerTargetFps=999` + cap-off flag). Hover a button to see exactly which flags it sets.
- **Balanced** — 2x MSAA, texture quality 2.
- **Potato** — MSAA 1, texture quality 0 (for weak hardware).
- **Future lighting** — forces Roblox's newer lighting (experimental: heavier GPU load, not all games like it).
- **Reset** — removes every flag these presets can set; your other flags are untouched.

Presets are committed together with your other flag edits via the Save button, and (like all your flags) they are saved per-account.

> **Why there's no "force dark theme" preset:** older theme-forcing flags (`FStringForcedTheme` and friends) are stripped by Roblox's flag allowlist and no longer work. The in-game menu follows the theme setting of the account you're logged into (in-game menu → Settings → Dark).

## Game presets

Under the quick presets there's a **Game presets** selector - flag bundles tuned for a game *genre*, since engine flags are global and what differs is the right trade-off per game type:

- **Simulators** (Blox Fruits, Pet Sim) - uncapped FPS, everything low; grinding games are particle/UI heavy
- **Shooters** (Arsenal, Phantom Forces) - uncapped FPS, low detail
- **Obby / Parkour** - frame consistency over eye candy
- **Roleplay** (Brookhaven, Adopt Me) - 120 FPS, keep it pretty
- **Story / Horror** (Doors) - quality and atmosphere, modest 120 target
- **Competitive** - solid 240 target, low detail

Every bundle uses only the long-proven flags (FPS cap pair, texture quality, MSAA) - no speculative flags that get stripped by Roblox's allowlist or stutter on some GPUs.

Pick one, hit **Apply** - it lands in the active account's flag set like everything else. **Reset all presets** clears every flag any preset can set.

A note on honesty: the FPS-cap and quality flags are the long-standing workhorses; shadow intensity, post-FX and the D3D11 preference depend on what Roblox's allowlist permits at any given time - hover tooltips tell you exactly what gets set, and if a flag is ignored it simply does nothing.

## Flag autocomplete

In the flag editor, click into the **Name** field of the Add Flag dialog and a list of known flags pops up - type to filter (prefix matches first), click an entry or press Enter/Tab to complete it. The catalog ships with 376 known flag names and refreshes daily from Roblox's own settings endpoint (cached to `FlagCatalog.json` in the install folder; the embedded list always works offline).

## Per-account flag sets

This is Onyxstrap's twist on FastFlags: **each saved account has its own flag set**.

- Whichever account is *active* is what the editor edits.
- Switch active account (or launch another account from the Accounts page) and the editor now shows and edits *that* account's flags — applied at the next launch.
- Want the same flags everywhere? Save your current set as a **preset** and apply it to each account, or just keep one account active and don't save others.

## Presets

On the **Accounts** page:

- **Save current flags as preset** snapshots the active account's flags under a name.
- **Apply** copies a preset onto the active account's flag set (overwriting it — save first if unsure).
- **Remove** deletes the preset. Accounts' own sets are untouched.
