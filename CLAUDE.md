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
| `ItemCatalog` | Static catalog of every discoverable item, baked once per world-load. **Three-loop architecture:** Loop 1 (Iter-3.7) enumerates standard items from `PugDatabase.objectsByType.Keys` (skipping `IsCookedFood()` and pets, both re-emitted later); Loop 2 (α-enumeration) cartesians ingredient-pairs to emit cooked-food permutations × 3 tier-variants; Loop 3 (Iter-16.1) emits one entry per `(petObjectID, skinIndex)`. Catalog grows to ~10,900 entries. |
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
| `CookBookUI` | `Pug.Other.dll` | CK's own cooked-food browser. **Not** a viewport recycler: `ItemSlotsUIContainer.InstantiateItemSlots` builds a *fixed* pool of `MAX_ROWS × MAX_COLUMNS` slots (CookBook: 50×5=250) once, and `UpdateFilter` **breaks at `num >= itemSlots.Count`** — entries past slot 250 are never shown; it scrolls by translating the whole pool under the clip mask, recycling nothing. Fine for ≤250 recipes, unusable for ItemChecklist's ~10,900 entries. True viewport virtualization (Iter-3.8) is mod-built on `IScrollable.UpdateContainingElements`; CK ships no recycler template. |
| `I2.Loc.LocalizationManager.OnLocalizeEvent` | `I2.Loc.dll` | Public static `event Action` (no params). Fires after language change via `DoLocalizeAll()` / `Coroutine_LocalizeAll()`. Sandbox-safe (public static event on trusted I2.dll). Fallback if banned: poll `LocalizationManager.CurrentLanguage` in a `ManagedUpdate` postfix (ItemBrowser-proven pattern — compare against cached value, trigger on change). |
| `PlayerController.GetObjectName` | `Pug.Other.dll` | `GetObjectName(buf, localize: bool)` — second param is `bool localize`. Passing `false` yields the raw I2 term path (e.g. `"Items/LargeWaterCan"`), not the display name. IB pattern (ObjectUtility.cs:97–108): `localizedName = GetObjectName(buf, true).text`; `unlocalizedName = GetObjectName(buf, false).text`; if `fields.dontLocalize` → use unlocalizedName as fallback. |
| `UIScrollWindow.SetScrollValue` | `Pug.Other.dll` | `SetScrollValue(float normalizedScrollValue)`: `1f` → top of list (lerp → minScrollPos=0, content.localY=0); `0f` → bottom (content shifted up by full height). Use `scrollWindow.ResetScroll()` for "go to top" — equivalent to `SetScrollValue(1f)`. Post-content-spawn sequence (IB EntriesList.SetEntries pattern): set scrollable via `API.Reflection.SetValue`, invoke `UpdateScrollHeight` via `API.Reflection.Invoke`, then call `SetScrollValue(1f)`. Full internals in `docs/architecture.md § UIScrollWindow Reference`. |
| `ScrollBar` / `ScrollBarHandle` | `Pug.Other.dll` | Native scrollbar (Iter-5, prefab-wired). `ScrollBar` fields: `scrollWindow`, `root` (a **child** GO it toggles — not the component's own GO, else it can self-deactivate before `ScrollHeight` is set), `background` (track `SpriteRenderer`), `handle` (`ScrollBarHandle`). `ScrollBarHandle : ButtonUIElement` fields: `handleSpriteRenderer`, `handleCollider` (**3D `BoxCollider`** `!u!65`, not `!u!61` — CK `UIMouse` raycasts in 3D), `handleSpritesToResize`. **No mod C#:** `UIScrollWindow.LateUpdate → UpdateScrollbar → ScrollBar.UpdateScrollBarPosition` does sizing + position + mouse-wheel sync once `UIScrollWindow.scrollBar` is wired (verified against Item Browser, which ships no scrollbar C#). Handle drag = `ScrollBarHandle.onLeftClick` UnityEvent → `ScrollBar.OnHandleLeftClick` (`m_TargetAssemblyTypeName: ScrollBar, Pug.Other`, `m_Mode: 1`). Handle size ∝ `VisibleRatio`, min 0.625. **`ButtonUIElement.LateUpdate` toggles GO activity** of `spritesShownUnpressed` (active when `!leftClickIsHeldDown`) and `spritesShownPressed` (active when held) — a GO in **both** lists ends up visible only while held; with one handle sprite keep both lists empty and let `handleSpriteRenderer` be the always-on handle, with the selected-border as `optionalSelectedMarker`. Renderers need `maskInteraction: None`. See `docs/architecture.md § Scrollbar (Iter-5)` and the `project-corekeeper-script-fileid-derivation` memory. |
| `CoreLib UserInterfaceModule` | `CoreLib.UserInterface.dll` | Version 4.0.4 (stable Feb–May 2026). `LoadSubmodule` in `EarlyInit`; `RegisterModUI(GameObject)` in `ModObjectLoaded`. UI class must extend `UIelement` AND implement `IModUI`. Mount: auto into `UIManager.chestInventoryUI.transform.parent`. Open: `UserInterfaceModule.OpenModUI("ItemChecklist:Window")`. Auto-hide on vanilla `HideAllInventoryAndCraftingUI`; zero patches needed for cursor/WASD-block/Escape. `Awake()` must call `HideUI()`. **Iter-4 F1 toggle + menu-exclusion:** `OpenModUI` is not toggle-capable, so the mod toggles itself — close via `Manager.ui.HideAllInventoryAndCraftingUI(forceClose: false)` (mirrors `PlayerController.CloseAnyOpenInventory`; CoreLib's postfix clears `currentInterface`), open-state read from `Instance.Root.activeSelf` not `currentInterface`. Guard with `Manager.ui.isPlayerInventoryShowing` (per-UI `isShowing` getters on `UIManager` in `Pug.Other.dll` are **unpatched** — CoreLib only patches the aggregate `isAnyInventoryShowing`). `InventoryOpenAutoHidePatch` postfixes `UIManager.OnPlayerInventoryOpen` — the single funnel every Vanilla menu open routes through — with a **bare** `HideUI()` to enforce no-overlap; coherent only while `ShowWithPlayerInventory == false`. Background: `Manager.ui.GetCraftingUITheme(UIManager.CraftingUIThemeType.Wood).background` (enum param, not string → 9-slice wood frame, zero custom art). `BoxCollider2D` required on root. Production refs: limoka/BookMod (~145 IMod LoC + ~162 UI LoC), limoka/DummyMod (~87+84). |
| `UIManager.GetSlotBorderRarityColor` / `ObjectInfo.rarity` / `enum Rarity` | `Pug.Other.dll` / `Pug.Base.dll` | Iter-6 rarity colouring. `enum Rarity { Poor=-1, Common, Uncommon, Rare, Epic, Legendary }`; `ObjectInfo.rarity` is a plain `public Rarity rarity` field. `Color GetSlotBorderRarityColor(Rarity rarity, bool useDefaultColorForCommon, Color defaultColor)` returns `defaultColor` when `useDefaultColorForCommon && (rarity == Common \|\| rarity == Poor)`, else `Manager.ui.slotBorderRarityColors[(int)(rarity + 1)]` (a `List<Color>`). `Manager.ui.*` is sandbox-safe (already used in this mod). `PugText.color` setter = `SetTempColor(value)` (paints the glyph SpriteRenderers `Render()` rebuilds → set after Render; pass `keepColorOnStart: true` to survive `renderOnStart`). See `docs/architecture.md § Rarity Colouring (Iter-6)` + `docs/gotchas.md § PugText tint`. |
| `ButtonUIElement` | `Pug.Other.dll` | Iter-7 dropdown/toggle buttons. Clickable widget base class. Override `public override void OnLeftClicked(bool mod1, bool mod2)` — **guard `if (!canBeClicked) return;` FIRST, then call `base.OnLeftClicked(mod1, mod2)`** (the uniform ItemChecklist convention across `DropdownOptionButton`/`DropdownToggleButton`/`AscDescToggle`/`ClearSearchButton`/`FilterCheckboxButton`; when not clickable, base is not run). As of Iter-14.2 this prologue lives **once** in `abstract ClickButton : ButtonUIElement` (a `sealed override OnLeftClicked` → `protected abstract OnClick()`); the five subclasses now extend `ClickButton` and implement only `OnClick()`. Requires a **3D `BoxCollider`** (`!u!65`) — CK `UIMouse` raycasts in 3D. Leave `spritesShownUnpressed` and `spritesShownPressed` empty so `ButtonUIElement.LateUpdate` doesn't toggle GO activity and hide the button's SpriteRenderer (same rule as `ScrollBarHandle`). |
| `TextInputField` / `CharacterMarkBlinker` | `Pug.Other.dll` | Iter-8 search field — **CK-native text input, NOT uGUI**. `TextInputField : UIelement, InputManager.TextInputInterface` renders via `pugText`/`hintText` (PugText), caret via `characterMarkBlinker` (a `CharacterMarkBlinker` whose single serialized field `sr` is the caret SpriteRenderer); `OnLeftClicked` self-activates (`Manager.input.SetActiveInputField(this)`). Subclass it (`SearchBar`) + poll `GetInputText()` in `LateUpdate`. Key serialized fields: `maxWidth`; **`trim` — keep `0`** (else leading/trailing spaces are stripped per keystroke, breaking multi-word search); **`dontDeactivateOnDeselect` — set `true`** so the field stays focused when the mouse leaves its collider (CK selection is hover-based), then call `Deactivate(false)` on window close or WASD stays blocked. uGUI `InputField` is the wrong abstraction (`UnityInputFieldAdapter` deleted). See `docs/architecture.md § Filter & Search (Iter-8)`. **Iter-19 word-wrap crash:** `Awake` sets `pugText.maxWidth = maxWidth + (dontAllowNewLines ? 1 : 0)` (here `8.5`), so every `pugText.Render()` runs CK's buggy `PugFont.AddNewLinesToLinesExceedingMaxWidth` (`text[num3-1]` index-out-of-range → per-frame `IndexOutOfRangeException` while typing; CK bug, on stock too). A single-line field must never word-wrap → `SearchBar` overrides `Awake` (`private new` — base is non-virtual; `base.Awake()` then `pugText.maxWidth = 0f`). Visual width still clips via the field's own `maxWidth` through `TrimTextToFitRestrictions` (char-trim, independent of word-wrap). A prefab `pugText.maxWidth = 0` is a **no-op** — `Awake` rewrites it; the fix must be code. See `docs/gotchas.md § Search Field / Header`. |
| `SceneHandler.cutsceneIsPlaying` / `CutsceneHandler` | `Pug.Other.dll` | Iter-15 cutscene gate. `cutsceneIsPlaying` is a public `bool` getter on `SceneHandler` delegating to `optionalCutsceneHandler.isPlaying` — returns `false` when no handler exists, so no null-guard is needed at the call site. Set `true` in `CutsceneHandler.StartPlaying()` (`Pug.Other` ~364007), cleared on completion/skip — brackets exactly the spawn-from-Core intro cutscene. **The intro cutscene fades CK's own HUD via `Manager.ui.FadeOutAllGameplayUI()`, NOT `CameraManager.ShowHUD(false)`** — so a mod's layer-27 HUD is *not* culled for free during it and must be gated explicitly. CK itself gates a discovery path on `cutsceneIsPlaying` (`Pug.Other` ~301674), flanked by the same companions `WorldState.IsInPlayableWorld` uses. Property access on the already-used `Manager.sceneHandler` is sandbox-safe (verified: 0 `CompileFailed`). Rejected the broader `SendClientInputSystem.PlayerInputBlocked()` (overlaps the menu/inventory checks the F1 guard already does + per-frame UI-input logic). Consumed by `WorldState.IsInPlayableWorld` (HUD + F1 guard). |

Iter-3.7's α-algorithm derives directly from `InventoryUtility.cs:~1626`:
for any ingredient pair `(i1, i2)`, the resulting family is
`CookedFoodCD.GetPrimaryIngredient(i1, i2).turnsIntoFood`, and the
variation is `CookedFoodCD.GetFoodVariation(i1, i2)`. Tiers (Base/Rare/
Epic) are looked up via `tierMap[baseFamily]` from `CookedFoodCD`.

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
- **No unit-test framework** — testing = `utils/build.sh` + in-game Player.log grep
  + manual UI verification; canonical 7-phase list in `docs/conventions.md § Testing`.
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

## Iter-Roadmap (live)

As of 2026-06-21: Iter-3.5 through Iter-12 (incl. the Iter-12 extension), Iter-13, Iter-14.1, Iter-18, Iter-14.2, Iter-15, Iter-19, Iter-20, Iter-21, Iter-16.1 (per-skin pet collection), and Iter-16.2 (critter collection) are DONE on main. Full per-iteration narrative:

@docs/iteration-history.md

### Future roadmap (frozen 2026-06-04)

Backlog of planned iterations (Iter-12 onward):

@docs/roadmap.md

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
- **For CK-UI "how does CK do X?" questions, read the working reference mod (Item
  Browser) before decompile guessing.** Decompile agents repeatedly guessed wrong on
  CK-UI internals; the ground truth came from IB source. The gradient shader name
  (`Amplify/UISpriteColorReplace`, not the guessed `Radical/SpritesDefault`) and the
  tooltip/gradient `SlotUIBase` architecture both came from IB after decompile agents
  guessed wrong. IB's working code beats a plausible-looking decompile inference.
