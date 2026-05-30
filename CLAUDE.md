# CLAUDE.md — ItemChecklist mod

ItemChecklist is a Core Keeper mod that tracks which items the player has
discovered, showing them as a scrollable checklist UI in-game. Parent
guidance (build setup, sandbox rules, macOS/CrossOver workflow,
`utils/build.sh`, fake-ID install) lives in the parent directory's
`CLAUDE.md` (sibling to this mod's repo root). This file holds
**ItemChecklist-specific** detail that other Core Keeper mods would not
need.

## Architecture (post-Iter-3.8)

Discovery state is split across four collaborating classes:

| Class | Responsibility |
|---|---|
| `ItemCatalog` | Static catalog of every discoverable item, baked once per world-load. Iter-3.7: two-loop architecture — Loop 1 enumerates standard items from `PugDatabase.objectsByType.Keys` (skipping items with `IsCookedFood()`); Loop 2 (α-enumeration) cartesians ingredient-pairs to emit cooked-food permutations × 3 tier-variants. Catalog grows to ~10720 entries. |
| `DiscoveredState` | In-memory mirror of `CharacterData.discoveredObjects2` for the active character. Keyed on packed `long` (`(objectId << 32) \| (uint)variation`) via `PackKey`. Two events: `Discovered(int, int)` per new pickup, `Changed` after any mutation. |
| `SaveManagerDiscoveryHook` | Harmony postfix on `SaveManager.SetObjectAsDiscovered`. Filters `__result == true` (CK fires the method ~261×/30s including non-new from `DetectUndiscoveredObjectsInInventory`). Mirrors `(objectID, variation)` into `DiscoveredState`. |
| `CharacterDataDiscoverySnapshot` | Harmony postfix on `CharacterData.OnAfterDeserialize`. Cache keyed on `characterGuid` (read directly via `__instance.characterGuid` field-access — the sandbox-safe path after several banned alternatives). Active-char resolution piggybacks on `SaveManagerActiveSelectHook.AwaitingActiveDeserialize`. |

Lifecycle:
- `IMod.EarlyInit` loads the CoreLib `UserInterfaceModule` **and**
  `ControlMappingModule` submodules — the latter registers the F1 keybind
  (polled in `IMod.Update` to open the window).
- `IMod.ModObjectLoaded` registers the window prefab via
  `UserInterfaceModule.RegisterModUI`.
- `IMod.Init` subscribes the loc-change hook; **no Bake-call here** (too
  early — `PugDatabase.objectsByType` is null).
- `PlayerController.OnOccupied` (D2 anchor) Harmony-postfix kicks the
  bake coroutine. `Manager.main.player` is non-null at this point;
  earlier anchors (`PugDatabase.UpdateEntityMonos`, `SaveManager.SetWorldId`,
  `IMod.Init`) all produce NRE. The bake call is launched via
  `__instance.StartCoroutine`; the coroutine does
  `WaitUntil(() => ClientWorldStateSystem.HasRunAtLeastOnce)` before calling
  `ItemCatalog.Bake()`. Never call Bake synchronously inside the postfix —
  that races ECS world readiness.
- `LocalizationManager.OnLocalizeEvent` re-bakes synchronously and
  triggers `ItemChecklistWindow.Instance.RebindRows()`.
- `ItemChecklistWindow.Awake` subscribes `DiscoveredState.Changed`
  for live title-quote refresh.

## CK Decompile References (relevant for ItemChecklist)

Re-derivation via ILSpy: `~/.dotnet/tools/ilspycmd -t <Type> <DLL>`.
DLLs are in CK's installation: `…/Core Keeper/CoreKeeper_Data/Managed/`.

| Type | DLL | Key facts |
|---|---|---|
| `CookedFoodCD` | `Pug.ECS.Components.dll` | `GetFoodVariation(p, s) = (primary << 16) \| secondary`, symmetric via deterministic `FirstIngredientIsPrimary` tiebreaker (Seed 87931). `rareVersion`/`epicVersion` fields point to tier-variant ObjectIDs. |
| `CookingIngredientCD` | `Pug.ECS.Components.dll` | Discriminator for ingredients. Fields `turnsIntoFood: ObjectID` (default family per ingredient) + `ingredientType: IngredientType`. |
| `IngredientType` (enum) | `Pug.Base.dll` | `{None, Plant, Fish, Meat}` — 4 values, 3 nutzbar. |
| `ObjectIDExtensions.IsCookedFood` | `Pug.Base.dll` | Range check `objectID ∈ [9500, 9599]` (max 100 family-item slots). |
| `ObjectIDExtensions.IsGoldenPlant` | `Pug.Base.dll` | Range `[8100, 8149]`. Used in `GetPrimaryIngredient` tiebreaker. |
| `InventoryUtility` (~line 1626) | `Pug.Other.dll` | Pick-family logic in non-Burst form: `family = ingredientLookup[primaryPrefabEntity].turnsIntoFood`. The Burst-compiled `ConvertCookedFoodsSystem` does the same. |
| `CookBookUI` | `Pug.Other.dll` | CK's own cooked-food browser. **Not** a viewport recycler: `ItemSlotsUIContainer.InstantiateItemSlots` builds a *fixed* pool of `MAX_ROWS × MAX_COLUMNS` slots (CookBook: 50×5=250) once, and `UpdateFilter` **breaks at `num >= itemSlots.Count`** — entries past slot 250 are never shown; it scrolls by translating the whole pool under the clip mask, recycling nothing. Fine for ≤250 recipes, unusable for ItemChecklist's ~10720 entries. True viewport virtualization (Iter-3.8) is mod-built on `IScrollable.UpdateContainingElements`; CK ships no recycler template. |
| `I2.Loc.LocalizationManager.OnLocalizeEvent` | `I2.Loc.dll` | Public static `event Action` (no params). Fires after language change via `DoLocalizeAll()` / `Coroutine_LocalizeAll()`. Sandbox-safe (public static event on trusted I2.dll). Fallback if banned: poll `LocalizationManager.CurrentLanguage` in a `ManagedUpdate` postfix (ItemBrowser-proven pattern — compare against cached value, trigger on change). |
| `PlayerController.GetObjectName` | `Pug.Other.dll` | `GetObjectName(buf, localize: bool)` — second param is `bool localize`. Passing `false` yields the raw I2 term path (e.g. `"Items/LargeWaterCan"`), not the display name. IB pattern (ObjectUtility.cs:97–108): `localizedName = GetObjectName(buf, true).text`; `unlocalizedName = GetObjectName(buf, false).text`; if `fields.dontLocalize` → use unlocalizedName as fallback. |
| `UIScrollWindow.SetScrollValue` | `Pug.Other.dll` | `SetScrollValue(float normalizedScrollValue)`: `1f` → top of list (lerp → minScrollPos=0, content.localY=0); `0f` → bottom (content shifted up by full height). Use `scrollWindow.ResetScroll()` for "go to top" — equivalent to `SetScrollValue(1f)`. Post-content-spawn sequence (IB EntriesList.SetEntries pattern): set scrollable via `API.Reflection.SetValue`, invoke `UpdateScrollHeight` via `API.Reflection.Invoke`, then call `SetScrollValue(1f)`. Full internals in `docs/architecture.md § UIScrollWindow Reference`. |
| `CoreLib UserInterfaceModule` | `CoreLib.UserInterface.dll` | Version 4.0.4 (stable Feb–May 2026). `LoadSubmodule` in `EarlyInit`; `RegisterModUI(GameObject)` in `ModObjectLoaded`. UI class must extend `UIelement` AND implement `IModUI`. Mount: auto into `UIManager.chestInventoryUI.transform.parent`. Open: `UserInterfaceModule.OpenModUI("ItemChecklist:Window")`. Auto-hide on vanilla `HideAllInventoryAndCraftingUI`; zero patches needed for cursor/WASD-block/Escape. `Awake()` must call `HideUI()`. Background: `Manager.ui.GetCraftingUITheme(UIManager.CraftingUIThemeType.Wood).background` (enum param, not string → 9-slice wood frame, zero custom art). `BoxCollider2D` required on root. Production refs: limoka/BookMod (~145 IMod LoC + ~162 UI LoC), limoka/DummyMod (~87+84). |

Iter-3.7's α-algorithm derives directly from `InventoryUtility.cs:~1626`:
for any ingredient pair `(i1, i2)`, the resulting family is
`CookedFoodCD.GetPrimaryIngredient(i1, i2).turnsIntoFood`, and the
variation is `CookedFoodCD.GetFoodVariation(i1, i2)`. Tiers (Base/Rare/
Epic) are looked up via `tierMap[baseFamily]` from `CookedFoodCD`.

## Mod-Specific Gotchas

- **No unit-test framework** — "testing" is build (`utils/build.sh`) +
  in-game smoke-test via Player.log grep + manual UI verification.
  Every Iter ends with a multi-point acceptance test list. See
  `docs/conventions.md § Testing Conventions` for the canonical 7-phase
  pattern.
- **PugText pixel-font U+2014 (em-dash) renders as U+002D (hyphen).**
  Cosmetic only — title format `"Item Checklist — N / M"` shows as
  `"… - N / M"` in-game.
- **`Manager.saves.*` property-access is sandbox-banned**, but
  field-access on serialized struct-fields (e.g.
  `__instance.characterGuid`, `objectData.variation`) is OK. This
  unlocks the character-GUID cache key.
- **PugText pool-leak:** spawned rows must `Clear()` their `PugText`
  children, or the shared pool leaks and main-menu PugTexts go blank. As
  of Iter-3.8 rows live in a persistent pool (no per-close Destroy), so
  the `Clear()` moved to `ItemChecklistContent.OnDestroy` (teardown only)
  and the blanking symptom can no longer occur. Detail in
  `docs/architecture.md § Viewport Virtualization`.
- **`ex.GetType().Name` inside a catch block is sandbox-banned.** `Type.Name`
  resolves to `MemberInfo.get_Name()` which is blocked by Roslyn's code-security
  verification. Symptom: `Illegal Member References = '1'`, `CompileFailed`.
  Fix: use typed catches (`catch (NullReferenceException ex)`) or log only
  `ex.Message` (sandbox-safe). Never call any `.Name` property on a reflected
  type in mod code.
- **uGUI (Canvas/Image) structurally fails in CK** — no `Collider`, so
  CK's `Physics.Raycast`-based `UIMouse` never sees it. Use
  `SpriteRenderer` + Layer 5 + `UIelement` (all 10 surveyed CK UI mods do).
  Full explanation: `docs/gotchas.md § uGUI structurally fails in CK`.
- **`PugMod.MemberInfo` vs `System.Reflection.MemberInfo` conflict.** Adding
  `using System.Reflection;` in any mod file causes `CS0104` ambiguity because
  `PugMod` also exports a `MemberInfo`. Solution: never add
  `using System.Reflection;` in mod code. Use `API.Reflection.SetValue` /
  `API.Reflection.Invoke` wrappers directly — they accept `PugMod.MemberInfo`.

## UI Clipping Pattern

SpriteMask + Custom Sorting-Layer Range (`"GUI"` layer, range `40..55`, all
renderers + PugText `style.sortingLayer` forced to `"GUI"`, mask sprite
`spritePixelsToUnits: 1`). Full working recipe (Iter-3.5c) and the aborted
Iter-3.5b lessons: `docs/gotchas.md § SpriteMask Clipping`.

## Iter-Roadmap (live)

As of 2026-05-30: Iter-3.5/3.5c/3.6/3.7/3.8 are DONE on main. Iter-3.8
replaced the per-entry SpawnRows (one GameObject per ~10718 catalog
entries, ~905 ms open freeze) with viewport virtualization: a fixed ~5-row
pool recycled from `IScrollable.UpdateContainingElements`, reporting the
full catalog height via `GetCurrentWindowHeight`. Open latency dropped to
~0-7 ms. Invariant from the geometry fix: `UIScrollWindow.windowHeight`
must equal the SpriteMask height (and the mask top align to row 0's top)
for the first/last rows to sit flush. F1 already opens the window (wired
since Iter-1 via the CoreLib `ControlMappingModule` keybind); it does
**not** close it (`UserInterfaceModule.OpenModUI` is not toggle-capable) —
ESC closes. Pending: Iter-4 (real F1 *toggle* — open-and-close on one key),
Iter-5 (Listen-Sortierung), Iter-6 (Filter+Suche — a discovered-only filter was
prototyped as a throwaway test scaffold in Iter-3.8 and removed; it can
seed Iter-6), Iter-7 (Window-/Style-Polish inkl. Footer-Move + perfectly
flush window needs a panel/mask resize to an integer row multiple, and a
scrollbar is not yet wired in the prefab). See `git log` for canonical
per-iter merge points and `docs/superpowers/specs/` for design docs.

## Conventions

- Documentation files (`README.md`, this `CLAUDE.md`, `docs/`) are
  English. Inline code-comments are mixed (English for class/method
  doc-comments, occasional German in research/spec narrative where
  shorter to express).
- Branch naming: `iter-<n>[.<m>[-letter]]`, e.g. `iter-3-7`, `iter-3-5c`.
  Each iter ends with a ff-merge to main (no squash, per parent global
  convention). See `docs/conventions.md` for full commit-type conventions,
  worktree hygiene, and the canonical per-iter test-phase structure.
- File layout: `docs/conventions.md § File Layout` for the authoritative
  `unity/ItemChecklist/` directory map.
- Build-verify (Editor compile ≠ sandbox pass): after a build, grep
  `Player.log` for the load/compile markers —
  `grep -iE "error CS|Build complete|Install complete|CompileFailed" Player.log`.
  A clean Editor build can still `CompileFailed` in the runtime sandbox.
- Spec lives in `docs/superpowers/specs/`, plan in
  `docs/superpowers/plans/`, exploratory research in `docs/research/`.
  Every iter has a 1:1:1 spec/plan/(optional)research mapping.
