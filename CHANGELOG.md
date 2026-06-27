# Changelog

All notable changes to this mod are documented in this file. The format is
loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
without strict adherence — entries describe what shipped per release, not
every commit. The topmost `## [x.y.z]` entry is the version `upload.sh`
publishes.

## [1.0.2] - 2026-06-27

A performance patch.

### Fixed

- **No more occasional stutter in your base.** The possession tracker recounts
  what you own every few seconds, and in a built-up base that recount could
  briefly hitch the frame rate each time it ran. It now reads the world far more
  efficiently — the recount is roughly twice as fast and its worst case no longer
  overruns a single frame, so the periodic stutter is gone. Your possession
  counts are unchanged.

## [1.0.1] - 2026-06-26

A bug-fix patch for the scrollable filter introduced in 1.0.0.

### Fixed

- **The filter popup no longer cuts off its top and bottom rows.** With the
  taller filter list shown in 1.0.0, the first and last entries were clipped and
  the scrollbar didn't match the visible area. The popup now sizes its visible
  window and scrollbar to the row limit, so every entry is fully visible and the
  scrollbar fills and travels the whole list.

## [1.0.0] - 2026-06-26

The 1.0 milestone. The checklist is now feature-complete on three axes —
**discovery**, **possession**, and **collection** of pets, critters and farm
animals — with native item tooltips, a scrollable filter, and original
pixel-art throughout. Everything in 0.9.x is carried forward; the headline
additions below are all new since the 0.9 series.

### Added

- **Possession tracking.** Each row now shows how many of that item you
  currently **own** — your carried inventory plus the contents of storage and
  display furniture around your base. The checkbox tints **blue** when you own
  at least one, and a new **"in / not in possession"** filter lets you hunt
  down what you've discovered but don't actually have. Items stored at a remote
  base stay counted while you're away. Spoiler-safe: an undiscovered (`???`)
  row never reveals possession.
- **Pet, critter and farm-animal collection.** Tamed pets are tracked **per
  skin** — each colour variant of a pet is its own collectible row with its own
  recoloured icon. Catchable critters (including fireflies) and farm livestock
  now get rows too, under new **Pets**, **Critters** and **Cattle** filter
  categories.
- **Per-colour tracking for cattle and paintable items.** Farm animals get one
  row per colour (five colour slots each), and paintable furniture and rugs get
  a row per paint colour, labelled with the real colour name (e.g. "(Red)").
  Where the game counts a placed object's colour, the owned count is per-colour
  too.
- **Row-hover tooltips.** Hover any row to see Core Keeper's native item
  tooltip — name, description and stats — plus an inventory-slot hover
  highlight. Discovered rows show the full tooltip; undiscovered rows highlight
  but reveal only a minimal `??? - not yet discovered`, so there are no
  spoilers.
- **Scrollable, collapsible filter.** The filter popup now scrolls (mouse wheel + 
  draggable scrollbar) instead of overflowing, and each section can be folded
  by clicking its header — sections fold independently and all start open.

### Changed

- **Original pixel-art throughout.** Every remaining placeholder sprite was
  replaced with original art, including a dedicated "unknown object" icon for
  undiscovered rows; item icons now render at their native size with the game's
  per-item offset.
- **The discovery counter can reach 100 %.** Collecting pet skins now counts
  toward the `N / M` total (footer and HUD), the **Discovered** filter shows
  collected skins, and "· N shown" counts them — so a full collection actually
  reads as complete.
- **Small UI labels render accents and umlauts correctly.** The sort/filter
  and header/footer labels use Core Keeper's small font, which ships without
  accented letters; the mod now supplies its own, so German (and other
  Western/Eastern-European) text reads correctly at that size.
- **Tidier combobox header** with a better-aligned search caret.

### Fixed

- **Typing in the search field no longer spams errors.** A latent Core Keeper
  word-wrap bug threw an exception every frame while typing in the search box;
  the field is now kept off that path.
- **A rebound toggle key now fully takes effect.** Previously F1 kept opening
  the checklist even after you rebound the toggle to another key; only the bound
  key opens it now.

### Known limitations

- After the list refreshes, the **search field** may not register the first
  keystrokes until you click it again (or click another widget first). It's a
  focus-timing quirk with no crash — a fix is tracked for a later release.
- **Per-colour possession** is counted only for placeable furniture, not for
  floors or walls (those are map tiles, not objects, so they show "—").
- **Non-paintable design variants** (e.g. the different Stalagmite shapes) are
  still tracked as a single row rather than one row per variant.
- **Cooked-food Rare/Epic tiers** are included but not yet verified against live
  cooking events; any unreachable tier simply stays greyed out.

## [0.9.1] - 2026-06-07

### Fixed

- The always-on HUD counter no longer shows up on the world-loading screen
  when you enter a world, and no longer lingers during the fade-out when you
  leave to the main menu. Opening the checklist with F1 is likewise blocked
  while either loading screen is up.

## [0.9.0] - 2026-06-05

First public release — a deliberate pre-1.0 version. The feature set is
complete; what remains for 1.0 is cosmetic (real pixel-art) and internal
cleanup.

### Added

- **Discovery checklist window (F1).** A scrollable, near-fullscreen window
  listing every discoverable item — vanilla and mod-added alike — each with its
  icon, localized name and discovered/undiscovered state. Undiscovered items are
  spoiler-hidden (shown as `???`). Discovery is tracked per world × per player
  and auto-checked on pickup or initial container scan.
- **Always-on HUD counter.** A permanent readout in the top-right corner (above
  the minimap) shows `N / M (p.p%)` discovered during gameplay and updates live.
  It hides automatically while an inventory, a menu, or the checklist window is
  open, and during the world-load screen.
- **Sorting.** Sort the list by **name**, **rarity**, **level** or **value**,
  each ascending or descending.
- **Faceted filtering.** A multi-select filter across four dimensions —
  **discovery** (discovered / undiscovered), **category** (weapons, armor &
  accessories, tools, food, placeables, materials, valuables, key items,
  instruments, other), **rarity** (Poor … Epic) and **craftability**. Options
  are OR within a dimension and AND across dimensions; a "Clear all" action
  resets every dimension at once.
- **Name search.** Filter the list by typing; matches the localized name in your
  game's language (a search in German finds German names). Undiscovered matches
  still appear as `???`.
- **Per-row Level and Value columns.** Each row shows the item's level (`Lv N`)
  and its sell value in Ancient Coins (with the coin glyph). Both show `—` for
  undiscovered rows and for items that have no level or no sell value.
- **Rarity colouring.** Item names are tinted by rarity, and Uncommon-and-above
  items get a rarity-coloured border around their icon — applied to undiscovered
  rows too, so an unfound Legendary is already distinguishable.
- **Per-permutation cooked-food tracking.** Each concrete `(ingredient1,
  ingredient2)` cooking permutation is tracked separately (e.g. Mushroom Soup ≠
  Tomato Soup), across the Base/Rare/Epic tiers, mirroring Core Keeper's own
  per-permutation discovery. The catalog totals ~10,800 entries.
- **Localisation (English + German).** All checklist UI text follows the game
  language and ships in English and German — including the F1 keybind name in the
  game's Controls menu. Switching the game language updates the checklist live.
  More languages can be added later as data.
- **Functional scrollbar.** A draggable, proportionally-sized scrollbar using
  Core Keeper's native widget, with mouse-wheel support.
