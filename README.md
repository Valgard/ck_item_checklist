# Item Checklist

A Core Keeper mod that tracks which items you have discovered in a world. Press
**F1** for a scrollable checklist of every discoverable item — vanilla and
mod-added alike — that auto-checks items on pickup, hides undiscovered items to
avoid spoilers, and keeps a live discovery counter on the HUD.

Discovery is tracked **per world × per player**.

## Features

- **Discovery checklist (F1)** — every discoverable item with its icon, localized
  name and discovered/undiscovered state; undiscovered items show as `???`. The
  toggle key is rebindable in settings, but a known bug (roadmap Iter-23) means F1
  still also opens it regardless of the rebind.
- **Always-on HUD counter** — `N / M (p.p%)` discovered, top-right above the
  minimap, updating live.
- **Possession tracking** — per row, how many of an item you currently own
  (carried + base storage/display); the checkbox tints **blue** when owned ≥ 1,
  with an "in / not in possession" filter. Spoiler-safe: undiscovered (`???`) rows
  never reveal possession.
- **Sort** by name, rarity, level or value (ascending/descending).
- **Faceted filter** — discovery, category, rarity and craftability
  (multi-select; OR within a dimension, AND across dimensions).
- **Name search** — matches the localized name in your game's language;
  undiscovered matches stay `???`.
- **Per-row Level and Value** columns (sell value in Ancient Coins).
- **Rarity colouring** of item names and icon borders (undiscovered rows too).
- **Per-permutation cooked-food tracking** (~10,800 catalog entries; Mushroom
  Soup ≠ Tomato Soup, across Base/Rare/Epic tiers).
- **English and German**, following the in-game language; switches live.

## Requirements

- Core Keeper (verified on 1.2.1.4)
- [CoreLib](https://mod.io/g/corekeeper/m/corelib) — required dependency

## Installation

Subscribe in-game through the **Mods** menu (or on the mod.io website) and
restart. [CoreLib](https://mod.io/g/corekeeper/m/corelib) must be installed
alongside it.

## Known Limitations

- **No per-variation tracking** (one exception). Each item family is tracked once;
  colour / state variants do not get their own row. The exception, since v0.10.0,
  is **pet skins** — each pet skin is a separate collectible with its own row.
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
