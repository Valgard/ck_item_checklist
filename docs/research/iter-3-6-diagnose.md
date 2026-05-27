# Iter-3.6 Diagnose Results

**Date:** 2026-05-28
**Branch:** iter-3-6
**Spike-Build-Commits:** 80fd5a9 (initial), c317514 (cosmetic tightening), d9767ad (sandbox-fix for `ex.GetType().Name`)
**Player.log location:** `/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log`

---

## D1 — Why does GetObjectName fail in Init()?

**Result:** THREW NullReferenceException ("Object reference not set to an instance of an object")
**Player.log line 1412:**
```
[Iter36Diag] anchor=D1:Init probeID=5 THREW NullReferenceException: Object reference not set to an instance of an object managerMainPlayerNull=True
```

**Conclusion:** `PlayerController.GetObjectName` dereferences `Manager.main.player` (or one of its sub-fields) internally. At `IMod.Init()` time, `Manager.main.player` is `null` (verified — the probe logs `managerMainPlayerNull=True`). The NRE is raised inside `GetObjectName` before any localization lookup happens. `IMod.Init()` is therefore not a viable anchor for DisplayName resolution.

---

## D2 — Which world-load anchor is correct?

**Tested anchors (chronological by Player.log line):**

| Order | Line | Anchor | Result | `managerMainPlayer` |
|---|---|---|---|---|
| 1 | 1384 | `PugDatabase.UpdateEntityMonos` (D2a) | THREW NRE | null |
| 2 | 1412 | `IMod.Init` (D1) | THREW NRE | null |
| 3 | 1442 | `SaveManager.SetWorldId` (D2b) | THREW NRE | null |
| 4 | 1542 | `PlayerController.OnOccupied` (D2c) | text="Items/LargeWaterCan" | non-null |

**Winner:** `PlayerController.OnOccupied` — the only anchor that does NOT throw, and the only one with `Manager.main.player != null`. This is the anchor for `ItemCatalogWorldLoadHook` in Plan-Task 6.

**Substitutions used in the spike:**
- `OnSpawn` did not exist on `PlayerController`; substituted with `OnOccupied` (the actual spawn entry point that calls `PlayerInit()`).
- `OnSaveLoaded` did not exist on `SaveManager`; substituted with `SetWorldId` (the world-select entry point).
Both substitutions are documented inline in `Iter36DiagnoseSpike.cs`.

**Probe-ID-comment correction:** The spike's comment claims ID 5 is "Wood". Actual D2c output shows ID 5 resolves to the I2 term path `"Items/LargeWaterCan"`. Both are vanilla items; nothing substantive changes. Future readers: ignore the "Wood" comment in the spike code.

---

### ⚠️ Gotcha for Plan-Task 5 — Term-path vs. localized name

**D2c returns `"Items/LargeWaterCan"` (the I2.Loc term path), NOT a localized name like `"Large Water Can"`.**

Root cause: the spike called `PlayerController.GetObjectName(buf, false)` — the second parameter is `bool localize`. Passing `false` intentionally produces the _unlocalized_ form, which for I2.Loc items is the raw term path.

**This is not a timing problem with the anchor.** `OnOccupied` IS the correct anchor. The spike just measured the wrong parameter variant.

**What ItemBrowser does (verified from decompiled source — `ObjectUtility.cs:97-108`):**

```csharp
var localizedNameFormatFields   = PlayerController.GetObjectName(buf, true);   // localize=true
var unlocalizedNameFormatFields = PlayerController.GetObjectName(buf, false);  // localize=false

localizedName   = localizedNameFormatFields.text;
unlocalizedName = unlocalizedNameFormatFields.text;

// Fallback: if the localizer signals "don't localize this item", use the raw name
if (localizedNameFormatFields.dontLocalize)
    localizedName = unlocalizedNameFormatFields.text;
```

**What ItemBrowser also does before baking** (`ItemBrowserAPI.cs:193-216`): it starts a coroutine from `OnOccupied` that first waits for `ClientWorldStateSystem.HasRunAtLeastOnce` before calling `Bake()`. This gates the bake until the ECS client world has done at least one update, ensuring entity data is ready.

**Action items for Plan-Task 5 / Plan-Task 6:**
1. Call `PlayerController.GetObjectName(buf, true)` (localize=true) for the localized display name, with the `dontLocalize` fallback pattern from ItemBrowser.
2. Gate the `Catalog.Bake()` call behind `WaitUntil(() => ClientWorldStateSystem.HasRunAtLeastOnce)` in the coroutine started from `OnOccupied`. Do NOT bake synchronously in the Harmony postfix — start a coroutine from `__instance.StartCoroutine(...)`.
3. For language-change rebake: see D3 below. ItemBrowser uses the polling approach (check `LocalizationManager.CurrentLanguage` in `ManagedUpdate`), not `OnLocalizeEvent`. Either approach is viable.

---

## D3 — I2.Loc language-change event symbol

**DLL:** `CoreKeeperModSDK/Assets/Plugins/CoreKeeper/I2.dll`
**Namespace:** `I2.Loc`
**Class:** `LocalizationManager` (source file embedded in PDB: `Assets/External/I2/Localization/Scripts/Manager/LocalizationManager_Targets.cs`)

**Event found via binary analysis of I2.dll:**
- Accessor methods in DLL string table: `add_OnLocalizeEvent`, `remove_OnLocalizeEvent`
- Backing field: `_OnLocalize`
- No generic type arguments visible near the accessors (`Action<T>` would show `Action`1` or similar in the strings; none present)

**Symbol:** `I2.Loc.LocalizationManager.OnLocalizeEvent`
**Type signature:** `public static event Action OnLocalizeEvent` (no parameters; fires after language change or localization source update)

**How I2.Loc fires it:** called from `LocalizationManager.DoLocalizeAll()` and `LocalizationManager.Coroutine_LocalizeAll()` (both appear in the DLL string table). Fires once per language switch, after all `Localize` components have been updated.

**Sandbox status:** Presumed sandbox-safe. Subscribing to a public static event on a trusted DLL (`I2.dll`) is property/field access on a type that is not in the banned-API list. The actual invocation goes through the event mechanism in `I2.dll` itself, not through mod-code reflection. Will be verified empirically when Plan-Task 8 lands.

**Fall-back plan if subscription is banned:** Drop `OnLocalizeEvent` subscription. Use the ItemBrowser polling approach instead: hook `PlayerController.ManagedUpdate`, compare `LocalizationManager.CurrentLanguage` against a cached value, trigger rebake on change. This is already proven sandbox-safe by ItemBrowser shipping with it.

**Alternative confirmed-working approach (ItemBrowser pattern, `ItemBrowserAPI.cs:179-191`):**
```csharp
[HarmonyPatch(typeof(PlayerController), "ManagedUpdate")]
[HarmonyPostfix]
private static void PlayerController_ManagedUpdate(PlayerController __instance) {
    if (!__instance.isLocal || _lastLanguage == LocalizationManager.CurrentLanguage)
        return;
    // rebake
    _lastLanguage = LocalizationManager.CurrentLanguage;
}
```

---

## Sandbox discovery — `MemberInfo.get_Name()` is banned

The original Plan-Task 2 spike-code template called `ex.GetType().Name` inside a `catch` block. `Type.Name` resolves to `MemberInfo.get_Name()` via inheritance — that property access is blocked by the Roslyn sandbox.

**Player.log evidence (first failed run, before commit d9767ad):**
```
Assembly 'ItemChecklist' has failed code security verification.
Illegal Assembly Reference = '0', Illegal Namespace References = '1',
Illegal Type References = '1', Illegal Member References = '1'
```
Followed by `mod ItemChecklist load error: CompileFailed` and `Exit blocked by ModManager` — the classic compile-fail-cascade quit-deadlock from `project_corekeeper_compile_fail_cascade`.

**Fix (commit d9767ad):** Split the catch into:
- `catch (NullReferenceException ex)` — typed catch, compile-time name, no reflection needed
- `catch (Exception ex)` — logs only `ex.Message` (sandbox-safe property, returns `string`, not `MemberInfo.Name`)

Memory `project_pugstorm_sandbox_rules` was updated to reflect this discovery and the CoreLib `GetMembersChecked()` / `GetNameChecked()` workaround pattern.

**Plan-Bug note:** The original Plan-Task 2 step-1 template in `docs/superpowers/plans/2026-05-28-itemchecklist-displayname-iter3-6.md` carries the un-fixed `ex.GetType().Name` pattern. The plan is not edited mid-flight; this diagnose-doc captures the lesson and the memory update prevents repetition in future iters.

---

## Summary — decisions crystallized by this diagnose

| Question | Answer |
|---|---|
| Anchor for Catalog.Bake() | `PlayerController.OnOccupied` (Harmony postfix, local player only) |
| Bake timing within anchor | Coroutine with `WaitUntil(() => ClientWorldStateSystem.HasRunAtLeastOnce)` |
| GetObjectName parameter for localized name | `localize: true` (second param = `true`) with `dontLocalize` fallback |
| Language-change rebake trigger | `OnLocalizeEvent` (static event `Action`, I2.Loc) OR polling in `ManagedUpdate` |
| Sandbox risk | `OnLocalizeEvent` subscription: presumed safe. Fallback: `ManagedUpdate` poll (proven safe via ItemBrowser) |
