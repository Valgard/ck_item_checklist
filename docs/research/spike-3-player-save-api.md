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

## Sandbox lesson learned (2026-05-24)

The Editor's C# compile and the in-game RoslynCSharp sandbox apply
**different rules**. The Editor only checks that types resolve (so
`System.IO.MemoryStream` compiles fine because `System.IO.dll` is in
the asmdef's precompiled references). The in-game sandbox refuses to
load any assembly that references the `System.IO` namespace AT ALL —
even purely in-memory types like `MemoryStream`, `BinaryWriter`,
`BinaryReader`, `EndOfStreamException`. The loader marks the assembly
as "failed code security verification" with counts that match the
banned-type footprint and refuses to compile any scripts in that mod.

Symptom: `Assembly 'ItemChecklist' has failed code security
verification. Illegal Namespace References = '1', Illegal Type
References = '4', Illegal Member References = '13'` followed by `mod
ItemChecklist load error: CompileFailed` in Player.log.

Concrete rule for this mod: **No `System.IO` references anywhere,
period.** Persistence encoding/decoding is hand-rolled byte packing
via bit-shifts. `Convert.FromBase64String` / `Convert.ToBase64String`
live in `System` (not `System.IO`) and are allowed. `PlayerPrefs` is
in `UnityEngine` and is allowed.

Likely also banned without testing (don't reach for them): `System.IO.*`
of any kind, `System.Diagnostics.Process`, `System.Reflection.Emit.*`,
reflection-emit, anything that would let scripts touch the filesystem
or escape the managed sandbox. When in doubt, check sandbox status
with the same "load and grep Player.log for code security
verification" probe.
