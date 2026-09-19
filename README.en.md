# BD2 Fishing

> **Disclaimer:** Using this assistant carries risks, including account penalties or bans, game errors, and data loss. This project is not affiliated with the game publisher and does not guarantee safe use. Assess the risks and follow the game's rules; you assume responsibility for all risks and consequences of using the tool.

English · [简体中文](README.md)

[Download latest release](https://github.com/MadestSamurai/bd2-fishing/releases/latest) · [Report an issue](https://github.com/MadestSamurai/bd2-fishing/issues)

A standalone fishing assistant for the BrownDust II Windows client. Uses the game’s normal manual-fishing flow for repeated catches, bait, fish-bag management and map renewal.

## Download

Current version: **0.4.0**. Both editions have the same features and include Simplified Chinese / English.

| Edition | Runtime requirement | Recommended for |
| --- | --- | --- |
| **Portable** | .NET included | Most users; download and run |
| **Lite** | [.NET Desktop Runtime 8 x64](https://dotnet.microsoft.com/download/dotnet/8.0) | Smaller download if the runtime is installed |

Download one edition: the EXE runs on its own; ZIPs include both READMEs and licenses. No Python, development SDK or other BD2 tools are required. Lite needs the **Desktop Runtime**, not just .NET Runtime or ASP.NET Runtime. Verify downloads against `SHA256SUMS.txt`.

## Quick start

**Before upgrading:** pause and close the old assistant, restart the game normally, then connect with the new version.

1. Enter an unlocked fishing map and turn off the game’s built-in automatic fishing.
2. Open the assistant, click **Connect game**, and wait for the fishing area to be recognized.
3. Review bait and retention settings, then click **Start fishing**. Automatic approach is enabled by default if you land away from water.
4. Click **Stop fishing** or close the window to stop. A cast already in progress may complete through the normal release callback.

## Features and settings

| Setting | Behavior |
| --- | --- |
| Next cast delay | Default 1000 ms; accepts 0–60000 ms. Reeling and holds use game frames independently. |
| Cast charge | Default 90%; accepts 5–95%. |
| Prefer weak points | Uses a normal hit if the weak point cannot be reached in time. |
| Sell when full | On by default; sells only fish allowed by the rules below. |
| Keep all Legendary / locked / unknown fish | Three independent protections, on by default. |
| Size records by species | Keep no extra fish, MAX only, MIN only, or MAX and MIN. Applies separately to Legendary and locked fish. |
| Automatic bait | On by default; uses one existing bait when the buff expires. Continues without bait when depleted. |
| Automatic approach | On by default; uses native navigation or character movement to reach a casting area. |
| Map renewal | On by default; with 5 minutes left, finishes the catch, visits the lobby, then returns to the same map. |

### MAX / MIN retention

Protections are combined. **Keep all Legendary fish** still protects every Legendary fish even when MAX is selected. To retain only records, turn off the matching **Keep all** option, choose a species, and select MAX, MIN or both. Sizes are compared within each species and selected category. Ties retain one fish, preferring a locked fish and then the lowest inventory ID; identical MIN and MAX retain one fish. Missing size data protects the affected species.

Turning off **Keep all locked fish** allows the tool to unlock only fish selected for sale. It waits for the game's reply and checks inventory before replanning and selling. Still-locked fish are never sent in a sale request. Stopping, disabling sales or changing retention cancels subsequent actions; already unlocked fish are not automatically relocked. A failed or unconfirmed request pauses instead of being blindly resent.

The 0.3.2 “keep locked fish only” preference migrates to Legendary retention off and locked/unknown retention on. New independent rules take precedence.

Day/night transitions resume after the current catch and results. Moving stops when fishing, scene changes or popups take over. If approach remains blocked after three routes, the tool pauses with a reason.

## Language

Use **语言 / Language** in the top bar to switch between Simplified Chinese and English. The first launch uses Chinese on Chinese systems and English otherwise, then remembers your choice. Switching does not restart automation or change settings. Game-provided names and images keep their game language; raw diagnostics remain unchanged.

See [translation maintenance](docs/LOCALIZATION.md).

## Compatibility and limits

Supports the official Windows x64 PC client, one game process at a time, with the same privilege level as the game. Mobile and Android emulator clients are not supported. First connection resolves local interfaces and builds the component, which may take a few seconds. Uncertain interface matches stop connection with a diagnostic; adaptation does not guarantee every future update will work without maintenance.

Releases contain no game DLLs, resources, account inventories or private captures. Does not use built-in automatic fishing, buy supplies or unlock maps.

## Diagnostics and feedback

Settings and diagnostics are under `%LOCALAPPDATA%\BD2Fishing`; click **Open diagnostics** to open the folder.

| File | Purpose |
| --- | --- |
| `compatibility.json` | Local interface matching and failures |
| `runtime.json` / `latest.json` | Component state and latest fishing snapshot |
| `runtime.log` | Actions, replies, inventory checks and map transitions |

These files stay on your computer. An unconfirmed sale or bait request pauses rather than being blindly resent.

When reporting an issue, include the version, visible message and relevant log excerpts. Remove account information and personal paths first. Do not upload game DLLs, complete inventories or connection credentials.

## Development and contributions

Requires Windows x64, PowerShell and the .NET 8 SDK. Normal builds and regression tests do not need or connect to the game.

```powershell
.\build.ps1 -Locked
.\package.ps1 -Locked
```

Assets are written to `dist/v<version>/`. Packaging checks both runtime configurations and runs UI checks.

[Development and release workflow](docs/RELEASING.md) · [Documentation and release format](docs/PUBLICATION_STYLE.md) · [Current release notes](docs/RELEASE_NOTES.md)

## License

Project code is [MIT licensed](LICENSE). Dependencies retain their own licenses; see [third-party notices](THIRD_PARTY_NOTICES.md). This project is not affiliated with the game developer or publisher.
