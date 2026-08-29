# CLAUDE.md — ItemChecklist mod

ItemChecklist is a Core Keeper mod that tracks which items the player has
discovered, showing them as a scrollable checklist UI in-game. Parent
guidance (build setup, sandbox rules, macOS/CrossOver workflow,
`utils/build.sh`, fake-ID install) lives in the parent directory's
`CLAUDE.md` (sibling to this mod's repo root). This file holds
**ItemChecklist-specific** detail that other Core Keeper mods would not
need.

## Dependencies

Hard-depends on **CoreLib** and **Mod Settings Menu**, each wired twice — a
runtime asmdef reference so the code compiles, and `required: 1` in the
ModBuilderSettings `.asset` so the loader refuses to start the mod without
them.

The five in-game settings are declared through Mod Settings Menu, and how that
framework behaves is its documentation rather than this repo's:
`../mod-settings-menu/docs/tutorial.md` covers the widget kinds, the
`ModConfig` adapter pattern this mod follows, where values persist, the
localization term scheme, and how options are ordered. Read it there rather
than trusting a copy here — a copy of § 9 in this file had already drifted,
stating declaration order as fixed when it is the default and `.SortOptions(…)`
overrides it. What this mod's five settings *are* is the `ModConfig` row in
`docs/architecture.md § Possession (Iter-20) + Discovery Gate (Iter-21)`.

## Architecture

Discovery state is split across four collaborating classes: `ItemCatalog` (the
catalog of every discoverable item, baked once per world-load),
`DiscoveredState` (the in-memory mirror of the character's discoveries), and
two Harmony postfixes that feed it. Driving them is a load order in which
three steps sit where they do because the obvious earlier place throws — the
bake in particular hangs off `PlayerController.OnOccupied`, never off
`IMod.Init`.

`docs/architecture.md` carries all of it, and is **deliberately not an
`@`-reference**: § Mod Lifecycle for the load order and the bake anchor,
§ Data Architecture for the four classes and the catalog's four-loop bake, and
one section per feature for everything built on top.

## CK decompile references

The mod binds against a long tail of Core Keeper internals — the scroll window
and its scrollbar, the native text input, `PugFont`'s glyph fallback, `UIMouse`
hover selection, the cooked-food component data. Field names, call sequences
and the traps that only a decompile reveals are collected in
`docs/ck-decompile-reference.md`, one row per type, together with the ILSpy
command to re-derive any of them. **Deliberately not an `@`-reference.**

## Mod-Specific Gotchas

**Sandbox bans (AI guardrails — each `CompileFailed`s the whole mod on first
reference; keep these in mind when writing mod code):**
- **`Manager.saves.*` property-access is banned** — but field-access on serialized
  struct-fields (`__instance.characterGuid`, `objectData.variation`) is OK.
- **`.Name` on a reflected type inside a catch is banned** (`Type.Name` →
  `MemberInfo.get_Name()`; symptom `Illegal Member References`). Use typed catches
  (`catch (NullReferenceException ex)`) or log only `ex.Message`.
- **Never `using System.Reflection;`** — it `CS0104`-clashes with
  `PugMod.MemberInfo`. Use `API.Reflection.SetValue` / `.Invoke` directly.
- **`System.IO.*` is banned** (also reflection-emit, `System.Diagnostics.Process`).
  Persist via `API.ConfigFilesystem` (hand-ASCII), not file I/O.

**Other:**
- **Almost no offline testing** — testing = `utils/build.sh` + in-game Player.log grep
  + manual UI verification; canonical 7-phase list in `docs/conventions.md § Testing`.
  The **one** exception (Iter-44) is `dotnet run --project tests/possession-harness`:
  `PossessionLedger` + `PetCollection` are pure logic, so the harness compiles the real
  sources against ~40 lines of Unity stubs and asserts the shrink/prune/parse rules
  offline. Run it after touching either file. Nothing else — ECS, Harmony, UI, and
  above all the Roslyn sandbox — can be checked outside the game.
- **uGUI (Canvas/Image) structurally fails in CK** (no `Collider` → CK's
  `Physics.Raycast` `UIMouse` never sees it) — use `SpriteRenderer` + Layer 5 +
  `UIelement`. See `docs/gotchas.md § uGUI structurally fails in CK`.
- **PugText pool-leak** — spawned rows must `Clear()` their `PugText` children on
  teardown (now in `ItemChecklistContent.OnDestroy`). See `docs/architecture.md
  § Viewport Virtualization`.
- **Em-dash cosmetic** — PugText's pixel-font renders U+2014 as `-` (the title
  `"Item Checklist — N / M"` shows as `"… - N / M"`).

## UI Clipping Pattern

SpriteMask + Custom Sorting-Layer Range (`"GUI"` layer, range `40..55`, all
renderers + PugText `style.sortingLayer` forced to `"GUI"`, mask sprite
`spritePixelsToUnits: 1`). Full working recipe (Iter-3.5c) and the aborted
Iter-3.5b lessons: `docs/gotchas.md § SpriteMask Clipping`.

## Iterations

Work here is organised as numbered iterations (Iter-N), carried by two files in
`docs/`. Both are **deliberately not `@`-references**: together they are ~3,300
lines, and a session needs one of them at a specific moment rather than both at
every start.

- **`docs/roadmap.md`** — the live ledger, opened 2026-06-04. Every iteration's
  status: DONE entries link into the history, the rest is the open backlog. Read
  it before picking up work, and write a deferred point into it — including ones
  that are not code, such as the gallery screenshots.
- **`docs/iteration-history.md`** — the per-iteration narrative: what each one
  changed, why, and what a later iteration had to correct about it. Open it for a
  named Iter-N; it is not an overview.

Two things that mislead when read cold. Where the history says "the frozen
roadmap", it means the ledger's **original** 2026-06-04 backlog — the guesses
later iterations re-scoped against — not its current contents. And this section
used to carry its own running list of DONE iterations, which every iteration
plan dutifully appended to until it reached 15 KB and a stale "as of" date; the
ledger already answers that question, so do not start a second copy here.

## Conventions

- **Docs in English** (this `CLAUDE.md`, `README.md`, `docs/`); chat answers German.
  Inline code-comments mixed (English doc-comments; occasional German in spec/research).
- **Branch** `iter-<n>[.<m>[-letter]]`; each iter ends with a **ff-merge to main, no
  squash**. Full commit-type / worktree / per-iter test conventions + the authoritative
  `unity/ItemChecklist/` File Layout map: `docs/conventions.md`.
- **Editor compile ≠ sandbox pass.** After a build, grep `Player.log` for
  `error CS|Build complete|CompileFailed`; a clean Editor build can still
  `CompileFailed` in the runtime sandbox. `Player.log` is per-launch (prior session
  rotates to `Player-prev.log`). A **new `.cs` file** must also land in the install
  `Scripts/` **and** `ModManifest.json`, else the sandbox compile fails on the missing
  type (invisible to the Editor build).
- **superpowers spec → `docs/specs/`** (tracked; a `PostToolUse` hook rejects specs
  written to `docs/superpowers/specs/`); **plan → `docs/superpowers/plans/`**
  (gitignored); research → `docs/research/`. **Author spec + plan in the MAIN tree**
  (a worktree's gitignored plan is lost on `git worktree remove`). Spec retention is
  **ADR-gated** — commit only when ADR-worthy, else discard after the merge. See
  `docs/conventions.md § Worktree Conventions`.
- **The visual-calibration / in-game loop runs inline, not via subagents** — it needs
  the live CrossOver window and the build lock.
- **In-game-calibration iters run INLINE (`executing-plans`), not subagent-driven** —
  the shared SDK build lock + the live CrossOver hover-verification loop cannot be
  delegated to a subagent (Iter-22 confirmed).
- **For CK-UI "how does CK do X?" questions, read the working reference mod (Item
  Browser) before decompile guessing.** Decompile agents repeatedly guessed wrong on
  CK-UI internals; the ground truth came from IB source. The gradient shader name
  (`Amplify/UISpriteColorReplace`, not the guessed `Radical/SpritesDefault`) and the
  tooltip/gradient `SlotUIBase` architecture both came from IB after decompile agents
  guessed wrong. IB's working code beats a plausible-looking decompile inference.
