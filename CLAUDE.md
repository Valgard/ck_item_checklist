# CLAUDE.md — ItemChecklist mod

ItemChecklist is a Core Keeper mod that tracks which items the player has
discovered, showing them as a scrollable checklist UI in-game. Parent
guidance (build setup, sandbox rules, macOS/CrossOver workflow,
`utils/build.sh`, fake-ID install) lives in the parent directory's
`CLAUDE.md` (sibling to this mod's repo root). This file holds
**ItemChecklist-specific** detail that other Core Keeper mods would not
need.

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
| `CookBookUI` | `Pug.Other.dll` | CK's own cooked-food browser. **Not** a viewport recycler: `ItemSlotsUIContainer.InstantiateItemSlots` builds a *fixed* pool of `MAX_ROWS × MAX_COLUMNS` slots (CookBook: 50×5=250) once, and `UpdateFilter` **breaks at `num >= itemSlots.Count`** — entries past slot 250 are never shown; it scrolls by translating the whole pool under the clip mask, recycling nothing. Fine for ≤250 recipes, unusable for ItemChecklist's ~8,120 entries. True viewport virtualization (Iter-3.8) is mod-built on `IScrollable.UpdateContainingElements`; CK ships no recycler template. |
| `I2.Loc.LocalizationManager.OnLocalizeEvent` | `I2.Loc.dll` | Public static `event Action` (no params). Fires after language change via `DoLocalizeAll()` / `Coroutine_LocalizeAll()`. Sandbox-safe (public static event on trusted I2.dll). Fallback if banned: poll `LocalizationManager.CurrentLanguage` in a `ManagedUpdate` postfix (ItemBrowser-proven pattern — compare against cached value, trigger on change). |
| `PlayerController.GetObjectName` | `Pug.Other.dll` | `GetObjectName(buf, localize: bool)` — second param is `bool localize`. Passing `false` yields the raw I2 term path (e.g. `"Items/LargeWaterCan"`), not the display name. IB pattern (ObjectUtility.cs:97–108): `localizedName = GetObjectName(buf, true).text`; `unlocalizedName = GetObjectName(buf, false).text`; if `fields.dontLocalize` → use unlocalizedName as fallback. |
| `UIScrollWindow.SetScrollValue` | `Pug.Other.dll` | `SetScrollValue(float normalizedScrollValue)`: `1f` → top of list (lerp → minScrollPos=0, content.localY=0); `0f` → bottom (content shifted up by full height). Use `scrollWindow.ResetScroll()` for "go to top" — equivalent to `SetScrollValue(1f)`. Post-content-spawn sequence (IB EntriesList.SetEntries pattern): set scrollable via `API.Reflection.SetValue`, invoke `UpdateScrollHeight` via `API.Reflection.Invoke`, then call `SetScrollValue(1f)`. Full internals in `docs/architecture.md § UIScrollWindow Reference`. |
| `ScrollBar` / `ScrollBarHandle` | `Pug.Other.dll` | Native scrollbar (Iter-5, prefab-wired). `ScrollBar` fields: `scrollWindow`, `root` (a **child** GO it toggles — not the component's own GO, else it can self-deactivate before `ScrollHeight` is set), `background` (track `SpriteRenderer`), `handle` (`ScrollBarHandle`). `ScrollBarHandle : ButtonUIElement` fields: `handleSpriteRenderer`, `handleCollider` (**3D `BoxCollider`** `!u!65`, not `!u!61` — CK `UIMouse` raycasts in 3D), `handleSpritesToResize`. **No mod C#:** `UIScrollWindow.LateUpdate → UpdateScrollbar → ScrollBar.UpdateScrollBarPosition` does sizing + position + mouse-wheel sync once `UIScrollWindow.scrollBar` is wired (verified against Item Browser, which ships no scrollbar C#). Handle drag = `ScrollBarHandle.onLeftClick` UnityEvent → `ScrollBar.OnHandleLeftClick` (`m_TargetAssemblyTypeName: ScrollBar, Pug.Other`, `m_Mode: 1`). Handle size ∝ `VisibleRatio`, min 0.625. **`ButtonUIElement.LateUpdate` toggles GO activity** of `spritesShownUnpressed` (active when `!leftClickIsHeldDown`) and `spritesShownPressed` (active when held) — a GO in **both** lists ends up visible only while held; with one handle sprite keep both lists empty and let `handleSpriteRenderer` be the always-on handle, with the selected-border as `optionalSelectedMarker`. Renderers need `maskInteraction: None`. See `docs/architecture.md § Scrollbar (Iter-5)` and the `project-corekeeper-script-fileid-derivation` memory. |
| `CoreLib UserInterfaceModule` | `CoreLib.UserInterface.dll` | Version 4.0.4 (stable Feb–May 2026). `LoadSubmodule` in `EarlyInit`; `RegisterModUI(GameObject)` in `ModObjectLoaded`. UI class must extend `UIelement` AND implement `IModUI`. Mount: auto into `UIManager.chestInventoryUI.transform.parent`. Open: `UserInterfaceModule.OpenModUI("ItemChecklist:Window")`. Auto-hide on vanilla `HideAllInventoryAndCraftingUI`; zero patches needed for cursor/WASD-block/Escape. `Awake()` must call `HideUI()`. **Iter-4 F1 toggle + menu-exclusion:** `OpenModUI` is not toggle-capable, so the mod toggles itself — close via `Manager.ui.HideAllInventoryAndCraftingUI(forceClose: false)` (mirrors `PlayerController.CloseAnyOpenInventory`; CoreLib's postfix clears `currentInterface`), open-state read from `Instance.Root.activeSelf` not `currentInterface`. Guard with `Manager.ui.isPlayerInventoryShowing` (per-UI `isShowing` getters on `UIManager` in `Pug.Other.dll` are **unpatched** — CoreLib only patches the aggregate `isAnyInventoryShowing`). `InventoryOpenAutoHidePatch` postfixes `UIManager.OnPlayerInventoryOpen` — the single funnel every Vanilla menu open routes through — with a **bare** `HideUI()` to enforce no-overlap; coherent only while `ShowWithPlayerInventory == false`. Background: `Manager.ui.GetCraftingUITheme(UIManager.CraftingUIThemeType.Wood).background` (enum param, not string → 9-slice wood frame, zero custom art). `BoxCollider2D` required on root. Production refs: limoka/BookMod (~145 IMod LoC + ~162 UI LoC), limoka/DummyMod (~87+84). |
| `UIManager.GetSlotBorderRarityColor` / `ObjectInfo.rarity` / `enum Rarity` | `Pug.Other.dll` / `Pug.Base.dll` | Iter-6 rarity colouring. `enum Rarity { Poor=-1, Common, Uncommon, Rare, Epic, Legendary }`; `ObjectInfo.rarity` is a plain `public Rarity rarity` field. `Color GetSlotBorderRarityColor(Rarity rarity, bool useDefaultColorForCommon, Color defaultColor)` returns `defaultColor` when `useDefaultColorForCommon && (rarity == Common \|\| rarity == Poor)`, else `Manager.ui.slotBorderRarityColors[(int)(rarity + 1)]` (a `List<Color>`). `Manager.ui.*` is sandbox-safe (already used in this mod). `PugText.color` setter = `SetTempColor(value)` (paints the glyph SpriteRenderers `Render()` rebuilds → set after Render; pass `keepColorOnStart: true` to survive `renderOnStart`). See `docs/architecture.md § Rarity Colouring (Iter-6)` + `docs/gotchas.md § PugText tint`. |
| `ButtonUIElement` | `Pug.Other.dll` | Iter-7 dropdown/toggle buttons. Clickable widget base class. Override `public override void OnLeftClicked(bool mod1, bool mod2)` — **guard `if (!canBeClicked) return;` FIRST, then call `base.OnLeftClicked(mod1, mod2)`** (the uniform ItemChecklist convention across `DropdownOptionButton`/`DropdownToggleButton`/`AscDescToggle`/`ClearSearchButton`/`FilterCheckboxButton`; when not clickable, base is not run). As of Iter-14.2 this prologue lives **once** in `abstract ClickButton : ButtonUIElement` (a `sealed override OnLeftClicked` → `protected abstract OnClick()`); the five subclasses now extend `ClickButton` and implement only `OnClick()`. Requires a **3D `BoxCollider`** (`!u!65`) — CK `UIMouse` raycasts in 3D. Leave `spritesShownUnpressed` and `spritesShownPressed` empty so `ButtonUIElement.LateUpdate` doesn't toggle GO activity and hide the button's SpriteRenderer (same rule as `ScrollBarHandle`). |
| `TextInputField` / `CharacterMarkBlinker` | `Pug.Other.dll` | Iter-8 search field — **CK-native text input, NOT uGUI**. `TextInputField : UIelement, InputManager.TextInputInterface` renders via `pugText`/`hintText` (PugText), caret via `characterMarkBlinker` (a `CharacterMarkBlinker` whose single serialized field `sr` is the caret SpriteRenderer); `OnLeftClicked` self-activates (`Manager.input.SetActiveInputField(this)`). Subclass it (`SearchBar`) + poll `GetInputText()` in `LateUpdate`. Key serialized fields: `maxWidth`; **`trim` — keep `0`** (else leading/trailing spaces are stripped per keystroke, breaking multi-word search); **`dontDeactivateOnDeselect` — set `true`** so the field stays focused when the mouse leaves its collider (CK selection is hover-based), then call `Deactivate(false)` on window close or WASD stays blocked. uGUI `InputField` is the wrong abstraction (`UnityInputFieldAdapter` deleted). See `docs/architecture.md § Filter & Search (Iter-8)`. **Iter-19 word-wrap crash:** `Awake` sets `pugText.maxWidth = maxWidth + (dontAllowNewLines ? 1 : 0)` (here `8.5`), so every `pugText.Render()` runs CK's buggy `PugFont.AddNewLinesToLinesExceedingMaxWidth` (`text[num3-1]` index-out-of-range → per-frame `IndexOutOfRangeException` while typing; CK bug, on stock too). A single-line field must never word-wrap → `SearchBar` overrides `Awake` (`private new` — base is non-virtual; `base.Awake()` then `pugText.maxWidth = 0f`). Visual width still clips via the field's own `maxWidth` through `TrimTextToFitRestrictions` (char-trim, independent of word-wrap). A prefab `pugText.maxWidth = 0` is a **no-op** — `Awake` rewrites it; the fix must be code. See `docs/gotchas.md § Search Field / Header`. |
| `PugFont / TextManager.FontFace / PugFont.GlyphData` | `Pug.Other.dll` | PugText.style.fontFace is enum TextManager.FontFace — a packed bitfield (thinTiny=16777344, thinSmall=16777232, button=134217744). Each face = a separate PugFont ScriptableObject with its own atlas in the rrs* family (resolved by Manager.text.GetFont): thinTiny→rrs5 (256x40, 114 glyphs — CK's reduced face: basic Latin from ASCII 33 up, but NO accented characters, so no German umlauts; it is not digits-only, despite how it is used), thinSmall→rrsthin8 (331 glyphs), boldHuge→rrs18, etc. PugFont.GetGlyphData fallback chain on a missing char: button → thinTiny → chinese → japanese → korean → "?" — so a missing ö in a thinTiny label silently renders from the CHINESE font (CJK metric, deformed, no warning), which is why this mod depends on the Complete Tiny Font mod rather than patching thinTiny itself (Iter-46). See docs/architecture.md § Runtime Glyph Injection + the reference-ck-pugfont-architecture memory. |
| `SceneHandler.cutsceneIsPlaying` / `CutsceneHandler` | `Pug.Other.dll` | Iter-15 cutscene gate. `cutsceneIsPlaying` is a public `bool` getter on `SceneHandler` delegating to `optionalCutsceneHandler.isPlaying` — returns `false` when no handler exists, so no null-guard is needed at the call site. Set `true` in `CutsceneHandler.StartPlaying()` (`Pug.Other` ~364007), cleared on completion/skip — brackets exactly the spawn-from-Core intro cutscene. **The intro cutscene fades CK's own HUD via `Manager.ui.FadeOutAllGameplayUI()`, NOT `CameraManager.ShowHUD(false)`** — so a mod's layer-27 HUD is *not* culled for free during it and must be gated explicitly. CK itself gates a discovery path on `cutsceneIsPlaying` (`Pug.Other` ~301674), flanked by the same companions `WorldState.IsInPlayableWorld` uses. Property access on the already-used `Manager.sceneHandler` is sandbox-safe (verified: 0 `CompileFailed`). Rejected the broader `SendClientInputSystem.PlayerInputBlocked()` (overlaps the menu/inventory checks the F1 guard already does + per-frame UI-input logic). Consumed by `WorldState.IsInPlayableWorld` (HUD + F1 guard). |
| `UIMouse` / `SlotUIBase` / `UIelement` hover virtuals / `TextAndFormatFields` | `Pug.Other.dll` | Iter-22 row-hover tooltips. CK's tooltip is **selection-driven, not entity-driven**: `UIMouse.UpdateHoverText` (~356342) reads `Manager.ui.currentSelectedUIElement` and calls its virtual `UIelement.GetHoverTitle()`/`GetHoverDescription()`/`GetHoverStats(bool)`/`GetContainedObject()`, rendering at the **cursor** (~357077). `UIMouse.UpdateMouseUIInput` (~355773) re-derives the selection **every frame** from a 3D `Physics.Raycast` against `ObjectLayerID.UILayerMask` (~355871) — the nearest visible `UIelement` collider under the cursor becomes selected (`TrySelectNewElement`/`UIManager.OnUIElementSelected` ~273416); leaving it → `DeselectAnySelectedUIElement` (~273433). So **no live ECS entity is needed** — any `UIelement` with a 3D collider that returns a `ContainedObjectsBuffer{ objectData = ObjectDataCD{ objectID, variation, amount=1 } }` shows a tooltip. `SlotUIBase.GetHoverStats(ContainedObjectsBuffer, bool, bool)` (~327477) is an **instance** method (needs a `SlotUIBase`); a bare `new GameObject().AddComponent<MySlot>()` NREs in `SlotUIBase.Awake` on `animator.enabled` → override `Awake` empty. ItemChecklist drives tooltips from one shared `TooltipSlot : SlotUIBase` (`Awake` skipped) fed an injected object; rows delegate to it. Precedent: ItemBrowser `ItemBrowserSlot`. All read-only (`PugDatabase`/`Manager.ui`/null-checked `Manager.main.player`) → sandbox-safe. See `docs/iteration-history.md § Iter-22`. |

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
- **Mod settings (via Mod Settings Menu)** — five live in-game settings under
  Options → Mod settings, registered in `ItemChecklistMod.Init` (that call order
  is the render order) and read live: `ModConfig.Enabled` (Toggle, default on —
  master switch; when off the mod is fully inert: no possession scan, window won't
  open, HUD hidden), `.Mode` (Choice `CounterMode` {Discovery, Possession},
  Iter-36 — what the HUD + footer counter shows), `.AnchorRadius` (Slider 16-96,
  default 48), `.ScanIntervalSeconds` (Choice of preset seconds
  {1,2,3,5,8,10,15,20,25,30}, default 3; Iter-38 — read live each cycle in
  `Update()`) and `.Diagnostics` (Toggle, default off). `ModConfig`
  (`unity/ItemChecklist/ModConfig.cs`, root `ItemChecklist` namespace) is the mod's
  config adapter — renamed from `PossessionConfig` so every mod exposes a uniform
  root-namespace `ModConfig`. This replaced the former
  CoreLib `API.Config` surface; the framework persists them to
  `mods/ItemChecklist/config.cfg`. The loader now hard-depends on `ModSettingsMenu`
  (runtime asmdef ref + `.asset` `dependencies`, `required: 1`), alongside CoreLib.
  The possession *ledger* still persists separately via `API.ConfigFilesystem`
  (unchanged). Labels/hint live in `localization/localization.yaml`
  (`ItemChecklist-Config` namespace).
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
