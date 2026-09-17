# Localization

Both editions embed Simplified Chinese (`zh-CN`) and English (`en-US`). The language selector uses native language names. Chinese system locales initially select Chinese; other locales select English. The choice persists in `language.json` beside tool settings and never changes commands or leases.

## Catalogs and presentation

`localization/zh-CN.json` and `en-US.json` are UTF-8 source-message catalogs, similar to gettext msgids. Keep their keys identical. Complete messages are preferred; existing composed runtime status messages also have literal fragment entries. `LanguageCatalog` performs one longest-match pass for those legacy messages, preserving numbers and identifiers. Snapshots and logs are never rewritten. Game-provided names and images follow game language.

`WindowLanguage` localizes text, buttons, headers, tooltips and accessible names at the presentation boundary. It retains original source text for reversible live switching and removes property listeners when controls unload. Dropdown choices use the same catalog while retaining enum/ID values; never use translated labels as command identifiers.

## Maintaining translations

1. Update source text and both catalogs. Preserve whitespace in fragments; prefer complete sentences for new messages.
2. Run `./build.ps1 -Locked`. Tests check key parity, source coverage, system defaults and persistence independent of operation settings.
3. Run the packaged `--smoke <evidence-directory>` check. It switches languages during an isolated active run, verifies the owner and settings stay unchanged, and captures normal/minimum window sizes. It does not connect to the game.
4. Inspect English wrapping, dropdown choices, dynamic status, errors and switching back to Chinese. Include both READMEs in ZIPs and both languages in release notes.

Unknown external exception text and game-supplied names are preserved. Raw evidence remains suitable for diagnostics regardless of UI language.
