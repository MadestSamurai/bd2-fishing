# PR #2 integration review for 0.4.0

Reviewed head: `cf89a63aa6dfc4a1646c96d7832fbd373682f1bf` (bupt-lxc). Decision: accepted with integration corrections against 0.3.2. The original commits are retained as merge ancestry.

## Reviewed behavior

- Whole-bag, per-species extrema; Legendary and effective-locked groups have independent extrema, and protection is their union. Stable ties prefer locked fish and then inventory ID.
- Unknown sizes retain the affected species; invalid fish-table/sale metadata is never eligible. A plan is limited to 100 fish.
- Still-locked fish fail the final sale guard. Native unlock requires a successful response for the same 64-bit inventory ID and an unlocked inventory readback. The entire bag is replanned after each unlock, limited to the originally authorized batch.
- Owner, PID, lease, sale preference and retention fingerprint gate subsequent actions. Stop and rule changes cancel the remainder. Timeout does not authorize retry; already completed unlocks are not silently reversed.

## Integration corrections

- Preserved Runtime9's shoreline approach, native CharacterController fallback, movement cancellation and navigation regressions. The PR's older base must not replace these.
- Unified the merged contract and bumped runtime identity to Runtime10, including compiler, CLI, package identity and ABI assertion.
- Migrated the released 0.3.2 locked-only preference without enabling locked-fish sales. New explicit independent rules take precedence.
- Persist settings before replacing the live command, so a failed settings write does not publish a partially applied preference through the heartbeat.
- Localized retention selectors without changing enum values or active execution ownership. Game-provided fish names remain unchanged.

## Validation

- 14,979 logic/localization assertions and 24 compatibility assertions.
- Both known real client metadata sets: 35 types / 111 members resolved and 83 ABI assertions per client. The older client required 15 type / 77 member remappings. No game requests or injection during these checks.
- Isolated WPF checks cover independent options, persisted species rules, language switching with a live lease, and narrow-window scrolling.

The new native unlock → sale → continued fishing flow still needs server-side runtime observation. Offline success does not assert a live sale occurred. Existing 0.3.2 movement/catch evidence applies to its previously tested behavior, not to the new unlock flow.
