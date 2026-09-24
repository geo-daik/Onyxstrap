# Engine Settings (FastFlags)

FastFlags are Roblox's internal engine configuration variables. Onyxstrap gives you a friendly editor for them — plus some Onyxstrap-specific behaviour:

## The editor

Open **Onyxstrap → Settings → Engine Settings**. The simple view covers common presets (MSAA, textures, framerate behaviour); the JSON editor handles everything else.

- **Use the FastFlag manager** keeps the manager applying flags at launch. If you turn it off, Onyxstrap leaves `ClientSettings` untouched and you can manage the file by hand in the [modifications folder](Mods-and-Customization).
- Flags are written to `Modifications\ClientSettings\ClientAppSettings.json` in the install folder and copied into the installed Roblox version at every launch — so they **survive Roblox updates**.
- Most custom flags are ignored by the modern client due to Roblox's **flag allowlist** — presets in the UI still work, arbitrary flags may not.

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
