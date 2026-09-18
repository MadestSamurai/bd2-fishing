# BD2 Fishing

> **Disclaimer:** Using this assistant carries risks, including account penalties or bans, game errors, and data loss. This project is not affiliated with the game publisher and does not guarantee safe use. Assess the risks and follow the game?s rules; you assume responsibility for all risks and consequences of using the tool.

English · [简体中文](README.md)

A standalone Windows tool for continuous fishing in **BrownDust II**. It shows live state, reels using the game's normal input flow, manages bait and the fish bag, and can return to the same fishing map before its time limit.

## Download

Get **0.4.0** from [Releases](https://github.com/MadestSamurai/bd2-fishing/releases/latest). Both editions include Chinese and English and have the same features.

| Edition | Required runtime | Choose this if… |
| --- | --- | --- |
| Portable, Windows x64 | Included | You want to run without installing .NET. |
| Lite, Windows x64 | [.NET Desktop Runtime 8 x64](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) | You already have the desktop runtime and prefer a smaller download. |

Download the EXE directly, or the ZIP containing it, both READMEs and licenses. Neither edition requires Python, Visual Studio or a game SDK. `SHA256SUMS.txt` contains file checksums.

## Start fishing

1. Close the old tool and restart the game when upgrading so the previous component is unloaded.
2. Enter an unlocked fishing map and turn off the game's built-in automatic fishing.
3. Run `BD2Fishing-0.4.0-Portable-win-x64.exe` (or Lite). Select **English** in **语言 / Language** if needed.
4. Click **Connect game**. First connection checks the local client interfaces and may take a few seconds.
5. Review retention and bait settings, then click **Start fishing**. If you landed away from the water, automatic approach walks to a reachable casting area.
6. Click **Stop fishing** or close the window to stop.

## Settings

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

## Language and data

Language defaults to Chinese on Chinese systems and English otherwise. Switch at any time; your choice is saved without restarting fishing. Both editions contain both languages. Game-provided species names use the game's language; diagnostics retain original evidence.

Settings and diagnostics are under `%LOCALAPPDATA%\BD2Fishing`. **Open diagnostics** opens that folder.

## Compatibility and limits

The component is compiled against the installed client's interfaces when connecting. Missing or ambiguous interfaces stop connection with a diagnostic; adaptation does not guarantee that every future update works without maintenance. No game assemblies or account data are distributed.

Use one game instance and one BD2 component at a time. The tool does not buy supplies or unlock maps. If connection status remains unavailable, let the game finish loading; restart it if necessary rather than repeatedly reconnecting.

## Build and contribute

Requires Windows, the .NET 8 SDK and PowerShell:

```powershell
./build.ps1 -Locked
./package.ps1 -Locked
```

Build checks cover retention, movement, execution leases, compatibility and localization. Packaging verifies Portable/Lite runtime configuration and runs isolated UI checks. See [compatibility](docs/COMPATIBILITY.md), [release workflow](docs/RELEASING.md) and [translation maintenance](docs/LOCALIZATION.md).

## License

Project code: [MIT](LICENSE). Dependencies have their own licenses; see [third-party notices](THIRD_PARTY_NOTICES.md). Not affiliated with the game developer or publisher.
