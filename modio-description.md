# Item Checklist

**Item Checklist** adds a scrollable, searchable checklist of every discoverable
item in Core Keeper — vanilla and mod-added alike — so you always know what's
left to find, and how much of it you own.

Press **F1** to open it. Discovery and possession are tracked per world × per player.

## Two ways to complete the game
- **Discovery** — every item you've found, auto-checked on pickup. Undiscovered
  items stay spoiler-hidden as `???`.
- **Possession** — how many of each item you currently own (carried inventory +
  base storage and display furniture). The checkbox turns **blue** when you own
  at least one, with an "in / not in possession" filter to hunt down what you've
  seen but don't actually have.

## Collect them all
- **Pets** — tracked per skin: every colour variant of a pet is its own row.
- **Critters & farm animals** — catchable critters (including fireflies) and
  livestock get rows too, under dedicated Pets / Critters / Cattle categories.
- **Per colour** — cattle (five colour slots each) and paintable furniture & rugs
  get a row per colour, labelled with the real colour name.

## Everything you'd expect
- **Live HUD counter** — `N / M (p.p%)` discovered, top-right, updating as you play.
- **Row-hover tooltips** — Core Keeper's native item tooltip on hover (spoiler-safe
  on undiscovered rows).
- **Sort** by name, rarity, level or value · **faceted filter** (discovery,
  category, rarity, craftability) in a scrollable, collapsible popup · **name
  search** in your game's language.
- **Per-row Level & Value**, rarity colouring, and per-permutation cooked-food
  tracking (~11,100 entries).
- **In-game settings** under Options → Mod Settings — a master Enabled toggle,
  the discovered/owned counter mode, the possession scan interval, and the base
  anchor radius.
- **English & German**, following your in-game language live.

## Requirements
Requires **CoreLib** and **Mod Settings Menu** (both pulled in automatically when
you subscribe).

## Known limitations
Per-colour possession is counted for placeable furniture only (not floor or wall
tiles); non-paintable shape variants share a single row; and right after the list
refreshes the search field may need one extra click to take focus.

---

*Built with the official Pugstorm Core Keeper Mod SDK. Personal-use,
non-commercial (Core Keeper EULA). Not affiliated with or endorsed by
Pugstorm.*
