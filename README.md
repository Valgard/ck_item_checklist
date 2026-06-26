# Item Checklist

A Core Keeper mod that tracks which items you have discovered in a world. Press
**F1** for a scrollable checklist of every discoverable item — vanilla and
mod-added alike — that auto-checks items on pickup, hides undiscovered items to
avoid spoilers, and keeps a live discovery counter on the HUD.

Discovery is tracked **per world × per player**.

## Features

- **Discovery checklist (F1)** — every discoverable item with its icon, localized
  name and discovered/undiscovered state; undiscovered items show as `???`. The
  toggle key is rebindable in the game's input settings (default F1).
- **Always-on HUD counter** — `N / M (p.p%)` discovered, top-right above the
  minimap, updating live.
- **Possession tracking** — per row, how many of an item you currently own
  (carried + base storage/display); the checkbox tints **blue** when owned ≥ 1,
  with an "in / not in possession" filter. Spoiler-safe: undiscovered (`???`) rows
  never reveal possession.
- **Row-hover tooltips** — hover a row for Core Keeper's native item tooltip
  (name / description / stats) and an inventory-slot hover highlight. Discovered
  rows show the full tooltip; undiscovered rows highlight but show only a minimal
  `??? - not yet discovered` — no spoilers.
- **Sort** by name, rarity, level or value (ascending/descending).
- **Faceted filter** — discovery, category, rarity and craftability
  (multi-select; OR within a dimension, AND across dimensions). The filter popup
  is **scrollable** (mouse wheel + draggable scrollbar) and its sections are
  **collapsible** — click a section header to fold it; sections fold
  independently and all start open.
- **Name search** — matches the localized name in your game's language;
  undiscovered matches stay `???`.
- **Per-row Level and Value** columns (sell value in Ancient Coins).
- **Rarity colouring** of item names and icon borders (undiscovered rows too).
- **Per-permutation cooked-food tracking** (~11,119 catalog entries; Mushroom
  Soup ≠ Tomato Soup, across Base/Rare/Epic tiers).
- **Creature collection** — tamed pets are tracked **per skin** (each colour
  variant is its own row with its own recoloured icon), and catchable critters
  and farm animals get rows too, under dedicated **Pets**, **Critters** and
  **Cattle** filter categories.
- **Per-colour cattle and paint-colour tracking** — farm animals get one row per
  colour (5 colour slots each), and paintable items (furniture, rugs) get a row
  per paint colour with its real colour name (e.g. "(Red)").
- **English and German**, following the in-game language; switches live.

## Requirements

- Core Keeper (verified on 1.2.1.4)
- [CoreLib](https://mod.io/g/corekeeper/m/corelib) — required dependency

## Installation

Subscribe in-game through the **Mods** menu (or on the mod.io website) and
restart. [CoreLib](https://mod.io/g/corekeeper/m/corelib) must be installed
alongside it.

## Known Limitations

- **The search field may not register the first keystrokes right after the list
  refreshes.** It's a focus-timing quirk (no crash); if typing doesn't take,
  click the search field again — or click another widget first, then the field.
- **Per-colour possession is only counted for placeable furniture, not floors or
  walls.** Cattle and paintable furniture/rugs each get one row per colour with a
  per-colour owned count, but floors and walls are map tiles rather than objects, so
  they have no per-colour count and show "—" in the possession column.
- **Non-paintable design variants aren't split.** Items that come in fixed shape /
  state variants without a paint colour (e.g. the different Stalagmite shapes) are
  still tracked as a single row rather than one row per variant.
- **Cooked-food Rare/Epic tiers** are included but not yet verified against live
  cooking events — unreachable tiers, if any, simply stay greyed out.

## Localisation

The mod UI is localised in **English and German** and follows the in-game
language. Additional languages can be added as data — add one entry per language
in `localization/localization.yaml` and rebuild.

## Credits

All shipped UI sprites are original pixel-art. Early development used placeholder
sprites from [Item Browser](https://github.com/moorowl/ItemBrowser) by **moorowl**
(MIT License, © 2026 moorowl); these were fully replaced with original art in
Iter-12 and no longer ship.

## License

Personal-use, non-commercial — Pugstorm Core Keeper EULA. Built against the
official `CoreKeeperModSDK`. Source on GitHub; contributions and translations
welcome.

---

## Development

### Building

```bash
cd item-checklist
source .envrc && ../utils/build.sh "$REPO_ROOT"
# Prefer the explicit path arg over $PWD / ../../../ — it survives a cwd reset
# (e.g. a stray `cd` elsewhere) instead of misfiring silently against the wrong dir.
# On macOS: auto-runs install-macos.sh at the end.
# Unity Editor must be closed before build.sh (the Editor locks the project).
```

The build script refreshes SDK symlinks, runs a Unity batchmode build, and on
macOS places the freshly built mod into the fake-ID loader locations so Core
Keeper picks it up on next launch. See the parent `CLAUDE.md` for the full
build/install system and CrossOver/macOS specifics.

**Building from a git worktree:** pass the worktree path explicitly
(`build.sh "$WT"`) rather than relying on a relative `../../../utils/build.sh` plus
the right cwd. The `.envrc` env chain also breaks in a worktree (its
`source ../.envrc` fallback points one level too shallow), so run the build through
`direnv exec "$WT" bash -c '…'`, which walks the real `source_up` chain
(worktree → mod → parent). Full recipe in
`docs/conventions.md § Worktree Conventions`.

### Publishing

Publishing runs through the SDK's mod.io plugin via `../utils/upload.sh`, which
reads the published version and changelog from the topmost `## [x.y.z]` entry in
`CHANGELOG.md`. See the parent `CLAUDE.md` (sections "mod.io publishing" and
"The three mod IDs") for the full flow.

### Documentation

- [Architecture overview](docs/architecture.md)
- [Code conventions](docs/conventions.md)
- [Known gotchas](docs/gotchas.md)
- [Pixel-art authoring](docs/pixel-art-authoring.md)
- [Iteration history](docs/iteration-history.md)
- [Future roadmap](docs/roadmap.md)
- [Mod-internal CLAUDE.md](CLAUDE.md) (for AI assistants working in this repo)
