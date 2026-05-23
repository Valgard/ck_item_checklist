# Spike #3 — Pugstorm Player-Save API

**Date:** 2026-05-23
**Status:** Concluded by symbol-table analysis. Live-verification TODO.

## Question

Does PugMod expose a per-player data-blob API (write + read) we can use for
ItemChecklist's persistence, or do we need to fall back to Unity
`PlayerPrefs` or a `skipSafetyChecks: true` file?

## Method

`strings PugMod.SDK.Runtime.dll | grep` against the symbol table; no
disassembler available. The full SDK Runtime DLL has exactly **295
PascalCase types** — small enough to enumerate.

## Findings

### `PugMod.API` exposes these sub-interfaces

```
IAudio, IAuthoring, IClient, IConfig, IConfigEntry, IConfigFilesystem,
IEffects, IExperimental, IInput, ILocalization, IMod, IModLoader,
IReflection, IRendering, IServer
```

There is **no** `IPlayerProfile`, `IPlayerData`, `ISaveData`, `IStore`,
`IPersistence`, or similar. The only player surface is `API.LocalPlayer`
(a getter returning a player wrapper — exact type not visible at symbol
level).

### `IConfigFilesystem` is the closest match — but READ-only

Method table at offset `0x3fb8` lists exactly:

```
GetAllFiles        (0x443d)
ReadAllBytes       (0x4492)
FileExists         (0x45bc)
```

No `Write*`, no `Save*`, no `Put*`, no `Store*`. Intended for mods that
ship a user-editable `config.json` next to their assembly — read-side
only.

### No `Save`/`Store`/`Persist` symbols anywhere in the SDK Runtime DLL

```bash
strings PugMod.SDK.Runtime.dll | grep -iE "(Save|Store|Persist)"
# returns no Save/Store/Persist methods on any PugMod API surface
```

## Conclusion

**PugMod has no schreibbare Player-Save API.** The three viable options
collapse to two:

1. ~~Pugstorm Player API~~ — does not exist.
2. **Unity `PlayerPrefs` with composite key** — sandbox-safe, works
   without any manifest changes. **Default choice for Phase 1.**
3. **Own file with `skipSafetyChecks: true`** — possible, but trades
   away the sandbox for a Personal-Use-only convenience that PlayerPrefs
   already covers.

## Decision

**Use Unity `PlayerPrefs` with key schema:**

```
ItemChecklist.<playerId>.<worldId>  →  base64(encoded byte blob)
```

`playerId` resolution open (see Live-Verification below). Fallback to
literal `"local"` is acceptable in single-player.

PlayerPrefs has a per-key size limit on Windows (~1 MB) and is effectively
unlimited on macOS — our ~6 KB payload at 1500 items is well under either
limit.

## Live-Verification TODOs (USER)

These must be done in-game before `ChecklistStore` ships:

- [ ] **Confirm `PlayerPrefs` survives mod reloads.** Write a key in the
  ItemChecklistMod stub, restart the game, confirm the value is still
  there. (3 min)
- [ ] **Find a stable Player-ID.** Try `API.LocalPlayer.id`,
  `API.LocalPlayer.entity`, `API.LocalPlayer.playerName`,
  `API.LocalPlayer.steamId` — whichever resolves to a stable, unique
  string across game restarts. Log it from `Init()` and grep for it in
  Player.log. If none works, just key by world only and accept that
  all local players on the same machine share a list. (5 min)
- [ ] **Confirm world-ID surface.** Try `Manager.world.Settings.worldId`,
  `API.Client.World.CurrentWorldId`, or similar — log it from a
  world-loaded hook. (3 min)

## Implication for the plan

- `ChecklistStore.cs` (Plan Task D2) gets the **PlayerPrefs backend**
  directly — no longer "Option B fallback", it's THE backend. The Option
  A comment block in Task D2.2 can be removed.
- `ItemChecklistMod.cs` (Plan Task G1.2) `GetLocalPlayerId()` and
  `GetCurrentWorldId()` resolutions are the live-verification TODOs
  above — they belong in the implementation phase, not as code stubs
  today.
