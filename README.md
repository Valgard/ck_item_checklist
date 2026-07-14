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
- **Per-permutation cooked-food tracking** — every ingredient combination is its
  own dish (Mushroom Soup ≠ Tomato Soup) across the Base/Rare/Epic tiers Core
  Keeper can actually cook: roughly **~6,000 cooked dishes** of the ~8,100
  discoverable items in the catalog. Unreachable tier permutations (e.g. an Epic
  variant a recipe can never produce) are excluded, so 100 % stays attainable.
- **Creature collection** — tamed pets are tracked **per skin** (each colour
  variant is its own row with its own recoloured icon), and catchable critters
  and farm animals get rows too, under dedicated **Pets**, **Critters** and
  **Cattle** filter categories.
- **Per-colour cattle and paint-colour tracking** — farm animals get one row per
  colour (5 colour slots each), and paintable items (furniture, rugs) get a row
  per paint colour with its real colour name (e.g. "(Red)").
- **English and German**, following the in-game language; switches live.
- **In-game settings** (Options → Mod Settings) — a master Enabled toggle to
  switch the whole mod on/off without uninstalling, the discovered/owned counter
  mode, the possession scan interval, and the base anchor radius.

## Requirements

- Core Keeper (verified on 1.2.1.5)
- [CoreLib](https://mod.io/g/corekeeper/m/corelib) — required dependency
- **Mod Settings Menu** — required dependency (drives the in-game settings)

## Installation

Subscribe in-game through the **Mods** menu (or on the mod.io website) and
restart. [CoreLib](https://mod.io/g/corekeeper/m/corelib) and Mod Settings Menu
must be installed alongside it — both are pulled in automatically when you
subscribe.

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
- **Possession base detection is workbench-anchored.** "Owned" base storage is the
  contents of containers near one of your **workbenches** (and the crafting stations
  beside it). Because the game never places a workbench in a world structure, loot in
  camps, vaults, ruins or unopened world chests you merely explored past no longer
  counts as yours — there are no remote-structure false positives. (ItemChecklist's
  possession scan is lightweight — about 1 ms — so it isn't the source of
  exploration-time frame hitches; an observed lag spike turned out to be a different
  mod.)

## Localisation

The mod UI is localised in **English and German** and follows the in-game
language. Additional languages can be contributed as data — one entry per
language in the mod's localization data file — and the mod rebuilt.

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

Built with the official Pugstorm Core Keeper Mod SDK.

- [Architecture overview](docs/architecture.md)
- [Code conventions](docs/conventions.md)
- [Known gotchas](docs/gotchas.md)
- [Pixel-art authoring](docs/pixel-art-authoring.md)
- [Iteration history](docs/iteration-history.md)
- [Future roadmap](docs/roadmap.md)
- [Mod-internal CLAUDE.md](CLAUDE.md) (mod-specific development notes)
