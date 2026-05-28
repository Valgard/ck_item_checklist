# ItemChecklist Iter-3.7 (Variation-aware Cooked-Food Tracking) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Catalog + DiscoveredState + Hooks + UI auf variation-aware Tracking umstellen, sodass jede konkrete `(objectID, variation)`-Cooked-Food-Permutation als eigener Discovery-Token zählt (~9680 Catalog-Entries statt heute ~200).

**Architecture:** Daten-Modell-Key wechselt von `int` (objectID-only) auf gepackt-`long` (`objectID << 32 | variation`). `ItemCatalog.Bake()` bekommt zweite Loop-Phase mit α-Enumeration (Pre-Cache `ingredient → turnsIntoFood`, dann `n(n+1)/2`-symmetrisches Cartesian × 3 Tier-Items pro Pair via `CookedFoodCD.GetPrimaryIngredient` + `turnsIntoFood`-Lookup). Hooks erweitern ihre Signatur um `variation`. UI zeigt eine einzige Gesamt-Quote im Title via `state.Count / catalog.Count`.

**Tech Stack:** Unity 6000.0.59f2 (Mono), CoreKeeperModSDK mit Game-Runtime CoreLib 4.0.3, Harmony (`HarmonyLib`), Pugstorm Roslyn-Sandbox, CrossOver-Bottle „Core Keeper" auf macOS, CK 1.2.1.4-7f74.

**Spec:** `docs/superpowers/specs/2026-05-28-itemchecklist-cooked-food-iter3-7-design.md`

**Branch:** `iter-3-7` (von `main @ 94baf8c`)
**Worktree:** `REPO_ROOT/.worktrees/iter-3-7/` (bereits angelegt)
**Build-Command (jeder Task endet mit diesem):**
```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7 && \
  source .envrc && ../../../utils/build.sh
```
**Player.log-Path (CrossOver):**
`/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log`

**Test-Stage Reminder:** ItemChecklist hat **kein Unit-Test-Framework**. Jeder Task endet mit Build → Sven startet CK + lädt Welt → grep `Player.log` für erwartete Marker. Falls Marker nicht stimmen, vor dem Commit fixen.

---

## File Structure (alle Pfade relativ zum Mod-Repo-Root)

| Datei | Status | Verantwortung |
|---|---|---|
| `unity/ItemChecklist/DiscoveredState.cs` | MODIFIED | Key-Schema `HashSet<int>` → `HashSet<long>`, `PackKey`-Helper, neue API-Signaturen für `IsDiscovered`/`AddOne`/`Snapshot`, `Discovered`-Event zu `Action<int,int>` |
| `unity/ItemChecklist/SaveManagerDiscoveryHook.cs` | MODIFIED | 1-Zeilen-Edit: `AddOne(objId, variation)` statt `AddOne(objId)` |
| `unity/ItemChecklist/CharacterDataDiscoverySnapshot.cs` | MODIFIED | `Cache<string, int[]>` → `Cache<string, long[]>`, read both fields via `PackKey` |
| `unity/ItemChecklist/ItemCatalog.cs` | MODIFIED | `idToIndex: Dictionary<int,int>` → `keyToIndex: Dictionary<long,int>`, `TryGetIndex(objId, variation)`, Bake-Loop 1 mit `IsCookedFood`-Skip, neue Bake-Loop 2 mit α-Enumeration, PERF-Logging, TrimStart-Workaround entfernt |
| `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | MODIFIED | `SpawnRows`: `IsDiscovered(objId, variation)`, neuer `FormatTitle()`-Helper, `Discovered`-Event-Subscription für live Title-Refresh, PERF-Logging in `SpawnRows` |
| `unity/ItemChecklist/ui/FilterAndSearchModel.cs` | MODIFIED | `IsDiscovered(e.ObjectId)` → `IsDiscovered(e.ObjectId, e.Variation)` (1-Zeilen-Edit) |
| `unity/ItemChecklist/ItemChecklistMod.cs` | INDIRECT | Keine expliziten Code-Edits — `var ids = …Cache.TryGetValue(…)` macht type inference auf `long[]` automatisch, sobald Cache-Typ in Task 1 umgestellt wird |

**Nicht modifiziert (bewusst):**
- `unity/ItemChecklist/ui/ItemRow.cs` — `Bind(objectId, sprite, name, isDiscovered)`-Signatur bleibt
- `unity/ItemChecklist/ui/ItemChecklistContent.cs` — IScrollable bleibt unverändert
- `unity/ItemChecklist/ItemCatalogWorldLoadHook.cs` — Bake-Trigger bleibt
- `unity/ItemChecklist/ItemCatalogLocChangeHook.cs` — Loc-Change-Trigger bleibt
- `unity/ItemChecklist/SaveManagerActiveSelectHook.cs` — Active-Char-Resolution bleibt
- `unity/ItemChecklist/PascalCaseSplitter.cs` — bleibt unverändert

---

## Task 1: Foundation — DiscoveredState Key-Schema + alle Konsumenten

**Atomarer Refactor:** `DiscoveredState` API ändert sich (Signatur `int → (int, int)` über fünf Methoden + Event), alle vier Konsumenten müssen gleichzeitig migrieren, sonst kompiliert nichts. Ein Commit für die ganze Foundation.

**Files:**
- Modify: `unity/ItemChecklist/DiscoveredState.cs`
- Modify: `unity/ItemChecklist/SaveManagerDiscoveryHook.cs`
- Modify: `unity/ItemChecklist/CharacterDataDiscoverySnapshot.cs`
- Modify: `unity/ItemChecklist/ItemCatalog.cs` (nur Key-Schema-Teil; α-Loop kommt in Task 3)
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (nur SpawnRows-IsDiscovered-Aufruf; Title-Quote kommt in Task 4)
- Modify: `unity/ItemChecklist/ui/FilterAndSearchModel.cs` (1-Zeilen-Edit für `IsDiscovered`-Aufruf)
- Indirect: `unity/ItemChecklist/ItemChecklistMod.cs` (type inference auf `long[]` via `var ids` in `Cache.TryGetValue` — kein expliziter Edit nötig)

- [ ] **Step 1: Rewrite `DiscoveredState.cs` komplett**

Ersetze den ganzen Datei-Inhalt mit:

```csharp
using System;
using System.Collections.Generic;

namespace ItemChecklist
{
    /// <summary>
    /// In-memory mirror of <c>CharacterData.discoveredObjects2</c> for the
    /// currently active character. Keys are packed (objectId, variation)
    /// tuples — see <see cref="PackKey"/>.
    ///
    /// Populated by:
    /// <list type="bullet">
    ///   <item><see cref="CharacterDataDiscoverySnapshot"/> on save-load
    ///     (Harmony postfix on <c>CharacterData.OnAfterDeserialize</c>)</item>
    ///   <item><see cref="SaveManagerDiscoveryHook"/> on every new pickup
    ///     (Harmony postfix on <c>SaveManager.SetObjectAsDiscovered</c>)</item>
    /// </list>
    /// Read-only from the consumer's perspective — no public mutator. Both
    /// hook classes are in the same assembly so they can call the
    /// <c>internal</c> mutators.
    /// </summary>
    public sealed class DiscoveredState
    {
        private static readonly DiscoveredState _instance = new DiscoveredState();
        public static DiscoveredState Instance => _instance;

        private readonly HashSet<long> keys = new HashSet<long>();

        /// <summary>
        /// Pack an (objectId, variation) pair into a single long key.
        /// Upper 32 bits: objectId. Lower 32 bits: variation (as uint, to
        /// preserve sign-bit identity since CookedFoodCD encodes via
        /// <c>(primary &lt;&lt; 16) | secondary</c>).
        /// </summary>
        public static long PackKey(int objectId, int variation) =>
            ((long)objectId << 32) | (uint)variation;

        public int Count => keys.Count;
        public bool IsDiscovered(int objectId, int variation) =>
            keys.Contains(PackKey(objectId, variation));

        /// <summary>Raised when a single new (objectId, variation) is added.</summary>
        public event Action<int, int> Discovered;
        /// <summary>Raised after any mutation (Snapshot or AddOne).</summary>
        public event Action Changed;

        internal void AddOne(int objectId, int variation)
        {
            if (keys.Add(PackKey(objectId, variation)))
            {
                UnityEngine.Debug.Log(
                    $"[ItemChecklist] AddOne: ({objectId}, {variation}) (total {keys.Count})");
                Discovered?.Invoke(objectId, variation);
                Changed?.Invoke();
            }
        }

        internal void Snapshot(IEnumerable<long> packedKeys)
        {
            keys.Clear();
            foreach (var k in packedKeys) keys.Add(k);
            Changed?.Invoke();
        }
    }
}
```

- [ ] **Step 2: Patch `SaveManagerDiscoveryHook.cs` — Einzeile**

In `unity/ItemChecklist/SaveManagerDiscoveryHook.cs`, ersetze die `After`-Methode:

Vorher:
```csharp
[HarmonyPostfix]
static void After(ObjectDataCD objectData, bool __result)
{
    if (__result)
        DiscoveredState.Instance.AddOne((int) objectData.objectID);
}
```

Nachher:
```csharp
[HarmonyPostfix]
static void After(ObjectDataCD objectData, bool __result)
{
    if (!__result) return;
    DiscoveredState.Instance.AddOne((int) objectData.objectID, objectData.variation);
}
```

- [ ] **Step 3: Patch `CharacterDataDiscoverySnapshot.cs` — Cache-Typ + Read-Loop**

In `unity/ItemChecklist/CharacterDataDiscoverySnapshot.cs`:

**3a — Cache-Deklaration (Zeile ~47):**

Vorher:
```csharp
internal static readonly Dictionary<string, int[]> Cache = new Dictionary<string, int[]>();
```

Nachher:
```csharp
internal static readonly Dictionary<string, long[]> Cache = new Dictionary<string, long[]>();
```

**3b — Read-Loop in `After` (Zeile ~57-68):**

Vorher:
```csharp
int count = __instance.discoveredObjects2.Count;
int[] ids;
if (count == 0)
{
    ids = System.Array.Empty<int>();
}
else
{
    ids = new int[count];
    for (int i = 0; i < count; i++)
        ids[i] = (int) __instance.discoveredObjects2[i].objectID;
}

// Cache by guid for later lookup.
Cache[guid] = ids;
```

Nachher:
```csharp
int count = __instance.discoveredObjects2.Count;
long[] packedKeys;
if (count == 0)
{
    packedKeys = System.Array.Empty<long>();
}
else
{
    packedKeys = new long[count];
    for (int i = 0; i < count; i++)
    {
        var record = __instance.discoveredObjects2[i];
        packedKeys[i] = DiscoveredState.PackKey((int) record.objectID, record.variation);
    }
}

// Cache by guid for later lookup.
Cache[guid] = packedKeys;
```

- [ ] **Step 4: Patch `ItemCatalog.cs` — Key-Schema (NOCH KEIN α-Loop)**

In `unity/ItemChecklist/ItemCatalog.cs`:

**4a — Dictionary-Typ (Zeile ~62):**

Vorher:
```csharp
private readonly Dictionary<int, int> idToIndex = new Dictionary<int, int>();
```

Nachher:
```csharp
private readonly Dictionary<long, int> keyToIndex = new Dictionary<long, int>();
```

**4b — TryGetIndex-Signatur (Zeile ~66):**

Vorher:
```csharp
public bool TryGetIndex(int objectId, out int index) => idToIndex.TryGetValue(objectId, out index);
```

Nachher:
```csharp
public bool TryGetIndex(int objectId, int variation, out int index) =>
    keyToIndex.TryGetValue(DiscoveredState.PackKey(objectId, variation), out index);
```

**4c — idToIndex-Building am Bake-Ende (Zeile ~197-199):**

Vorher:
```csharp
idToIndex.Clear();
for (int i = 0; i < entries.Length; i++)
    idToIndex[entries[i].ObjectId] = i;
```

Nachher:
```csharp
keyToIndex.Clear();
for (int i = 0; i < entries.Length; i++)
    keyToIndex[DiscoveredState.PackKey(entries[i].ObjectId, entries[i].Variation)] = i;
```

- [ ] **Step 5a: Patch `ItemChecklistWindow.cs` — SpawnRows-Aufruf**

In `unity/ItemChecklist/ui/ItemChecklistWindow.cs`, Zeile ~104:

Vorher:
```csharp
row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName, state.IsDiscovered(entry.ObjectId));
```

Nachher:
```csharp
row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName,
    state.IsDiscovered(entry.ObjectId, entry.Variation));
```

- [ ] **Step 5b: Patch `FilterAndSearchModel.cs` — IsDiscovered-Aufruf**

In `unity/ItemChecklist/ui/FilterAndSearchModel.cs`, Zeile ~62:

Vorher:
```csharp
bool isDisc = state.IsDiscovered(e.ObjectId);
```

Nachher:
```csharp
bool isDisc = state.IsDiscovered(e.ObjectId, e.Variation);
```

**Hinweis zu `ItemChecklistMod.cs:123`:** Kein expliziter Edit nötig.
Die Zeile ist `…Cache.TryGetValue(activeGuid, out var ids)` gefolgt von
`DiscoveredState.Instance.Snapshot(ids)`. Sobald Step 3a `Cache` zu
`Dictionary<string, long[]>` ändert, macht type inference `var ids` zu
`long[]`, was zur neuen `Snapshot(IEnumerable<long>)`-Signatur passt.

- [ ] **Step 6: Build + smoke-test**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7 && \
  source .envrc && ../../../utils/build.sh 2>&1 | tail -10
```

Erwartet: `✓ Build complete.` + `✓ Install complete.`

Falls Compile-Errors: alle Konsumenten der alten API checken (grep `idToIndex`, `\.AddOne(`, `IsDiscovered(`, `Snapshot(` in `unity/`-Tree).

Sven startet CK, lädt Welt. Dann:
```bash
grep -E '\[ItemChecklist\] (ItemCatalog baked|AddOne)' \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -10
```

Erwartet:
- `[ItemChecklist] ItemCatalog baked: ~200 items` (noch keine Cooked-Foods-Enumeration — Task 3 wird das erhöhen)
- Keine `code security verification failed` Events:
  ```bash
  grep -c "code security verification failed" "$HOME/.../Player.log"
  ```
  Erwartet: `0`

- [ ] **Step 7: Commit**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7
git add unity/ItemChecklist/DiscoveredState.cs \
        unity/ItemChecklist/SaveManagerDiscoveryHook.cs \
        unity/ItemChecklist/CharacterDataDiscoverySnapshot.cs \
        unity/ItemChecklist/ItemCatalog.cs \
        unity/ItemChecklist/ui/ItemChecklistWindow.cs \
        unity/ItemChecklist/ui/FilterAndSearchModel.cs
git commit -m "$(cat <<'EOF'
refactor(state): variation-aware key schema (Iter-3.7 foundation)

DiscoveredState.keys: HashSet<int> → HashSet<long>, packed via
new PackKey(objId, variation) helper. IsDiscovered/AddOne/Snapshot
signatures take (objId, variation). Discovered event becomes
Action<int,int>.

All four consumers (Hook, Snapshot, Catalog, Window) migrated atomically.
Save-Migration: none — CK's discoveredObjects2 always had both fields,
Iter-3.6 just ignored variation on read.

Catalog still single-loop (~200 items); α-enumeration follows in Task 3.
EOF
)"
```

---

## Task 2: ItemCatalog.Bake() — Loop 1 mit IsCookedFood-Skip + TrimStart-Cleanup

Filtert die 45 Cooked-Food-Family-Items aus dem Standard-Loop und entfernt das Iter-3.6-TrimStart-Workaround, das jetzt obsolet ist.

**Files:**
- Modify: `unity/ItemChecklist/ItemCatalog.cs`

- [ ] **Step 1: IsCookedFood-Skip in Loop 1 einfügen**

In `unity/ItemChecklist/ItemCatalog.cs`, nach dem `od.variation != 0`-Skip (Zeile ~120) eine Zeile addieren:

Vorher:
```csharp
foreach (var od in PugDatabase.objectsByType.Keys)
{
    // Phase-1 scope: one tick per item family. Skip colour/skin
    // variations (variation > 0). Phase-2 may revisit if per-skin
    // tracking becomes desirable.
    if (od.variation != 0) continue;

    var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
```

Nachher:
```csharp
foreach (var od in PugDatabase.objectsByType.Keys)
{
    // Phase-1 scope: one tick per item family. Skip colour/skin
    // variations (variation > 0). Phase-2 may revisit if per-skin
    // tracking becomes desirable.
    if (od.variation != 0) continue;

    // Iter-3.7: Cooked-Food family-items (IDs in [9500,9599]) are
    // handled by the α-enumeration loop further down — skip them here
    // so they don't appear as variation=0 placeholder entries.
    if (od.objectID.IsCookedFood()) continue;

    var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
```

- [ ] **Step 2: TrimStart-Workaround entfernen**

In `ItemCatalog.cs`, Zeilen ~150-159 (`CK's DE-locale builds Cooked-Food display names…` Block):

Vorher:
```csharp
// CK's DE-locale builds Cooked-Food display names as "{ingredient} -{base}"
// (e.g. "Pilz -Suppe"). When the ingredient placeholder is empty at
// variation 0 (no Recipe-ContainedObjects set), the result starts with
// " -" — empirically verified 2026-05-28 via DIAG: " -Pudding", " -Salat",
// " -Suppe" for CookedPudding/CookedSalad/CookedSoup etc. (firstCharU+0020,
// pos1 = U+002D). Strip leading whitespace + dashes — safe because
// legitimate names don't start with that pattern; legitimate compound
// names like "Pilz-Suppe" have the dash interior, not leading.
if (!string.IsNullOrEmpty(locText)) locText = locText.TrimStart(' ', '-');
if (!string.IsNullOrEmpty(rawText)) rawText = rawText.TrimStart(' ', '-');
```

Nachher: **komplett entfernen** (die 10 Zeilen). Begründung: Loop 1 schließt alle Cooked-Food-Family-Items via `IsCookedFood()`-Filter aus, deshalb taucht das `" -Suppe"`-Garbage hier nicht mehr auf. Loop 2 setzt immer konkrete Pair-Ingredients in den Placeholder ein, was real-named Outputs erzeugt.

- [ ] **Step 3: Build + smoke-test**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7 && \
  source .envrc && ../../../utils/build.sh 2>&1 | tail -5
```

Sven startet CK, lädt Welt. Dann:
```bash
grep "ItemCatalog baked" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -2
```

Erwartet: Catalog-Größe um ~45 geschrumpft (was vorher ~200, jetzt ~155). Wenn vorher exakt z.B. 207, jetzt 162.

- [ ] **Step 4: Commit**

```bash
git add unity/ItemChecklist/ItemCatalog.cs
git commit -m "$(cat <<'EOF'
refactor(catalog): skip Cooked-Food family-items in Loop 1

ItemCatalog.Bake() Loop 1 now skips items with objectID in [9500,9599]
(via IsCookedFood()). The α-enumeration loop in Task 3 will add them back
as concrete permutations. Catalog shrinks by ~45 entries (the 15 base ×
3 tier family-items).

Iter-3.6's DE-locale TrimStart-workaround for " -Suppe"-garbage is
removed — no longer needed since family-items with variation=0 are no
longer fed to GetObjectName here.
EOF
)"
```

---

## Task 3: ItemCatalog.Bake() — Loop 2 α-Enumeration

Der eigentliche Iter-3.7-Kern: Cartesian-Enumeration aller `n(n+1)/2`-Pairs × 3 Tier-Familien, via deterministischer `family = primary.turnsIntoFood`-Pick-Logik (siehe ILSpy-Decompile in Spec § "Empirical Findings").

**Files:**
- Modify: `unity/ItemChecklist/ItemCatalog.cs`

- [ ] **Step 1: Hilfs-Helper `AddCookedEntry` als private Methode hinzufügen**

In `ItemCatalog.cs`, vor der `Bake()`-Methode oder am Klassen-Ende, eine private Helper-Methode:

```csharp
/// <summary>
/// Resolve+add a concrete cooked-food permutation entry. Shares the
/// two-pass name/icon resolution + accepted-list with Loop 1 by writing
/// into the same Dictionary instances (passed via parameters since they
/// are local to Bake()).
/// </summary>
private void AddCookedEntry(
    ObjectDataCD od,
    Dictionary<long, string> localizedNames,
    Dictionary<long, string> unlocalizedNames,
    Dictionary<long, Sprite> iconCache,
    List<ObjectDataCD> accepted)
{
    var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
    if (info == null) return;

    var (locText, locDontLocalize) = ResolveOne(od, localize: true);
    var (rawText, _)               = ResolveOne(od, localize: false);
    if (locDontLocalize && !string.IsNullOrEmpty(rawText))
        locText = rawText;
    if (string.IsNullOrEmpty(locText))
        locText = PascalCaseSplitter.Split(od.objectID.ToString());
    if (string.IsNullOrEmpty(rawText))
        rawText = PascalCaseSplitter.Split(od.objectID.ToString());

    long key = DiscoveredState.PackKey((int)od.objectID, od.variation);
    localizedNames[key]   = locText;
    unlocalizedNames[key] = rawText;
    accepted.Add(od);
    iconCache[key] = info.smallIcon != null ? info.smallIcon : info.icon;
}
```

- [ ] **Step 2: Loop 1 anpassen — gleiche Dict-Typen wie Loop 2**

In `Bake()`, die Dict-Deklarationen für Loop 1 von `Dictionary<ObjectDataCD, ...>` auf `Dictionary<long, ...>` umstellen, damit Loop 1 und Loop 2 dieselben Container teilen:

Vorher (Zeile ~110-113):
```csharp
var localizedNames   = new Dictionary<ObjectDataCD, string>();
var unlocalizedNames = new Dictionary<ObjectDataCD, string>();
var accepted         = new List<ObjectDataCD>();
var iconCache        = new Dictionary<ObjectDataCD, Sprite>();
```

Nachher:
```csharp
var localizedNames   = new Dictionary<long, string>();  // key = PackKey(objId, variation)
var unlocalizedNames = new Dictionary<long, string>();
var accepted         = new List<ObjectDataCD>();
var iconCache        = new Dictionary<long, Sprite>();
```

Und in Loop 1, wo die Dicts geschrieben werden (Zeile ~167-170), den Key umbauen:

Vorher:
```csharp
localizedNames[od]   = locText;
unlocalizedNames[od] = rawText;
accepted.Add(od);
iconCache[od] = info.smallIcon != null ? info.smallIcon : info.icon;
```

Nachher:
```csharp
long key = DiscoveredState.PackKey((int)od.objectID, od.variation);
localizedNames[key]   = locText;
unlocalizedNames[key] = rawText;
accepted.Add(od);
iconCache[key] = info.smallIcon != null ? info.smallIcon : info.icon;
```

Und der zweite Pass (Conflict-Disambiguation, Zeile ~180-191):

Vorher:
```csharp
foreach (var od in accepted)
{
    string finalName = localizedNames[od];
    if (nameCount[finalName] > 1)
    {
        string rawName = unlocalizedNames[od];
        if (!string.IsNullOrEmpty(rawName) && rawName != finalName)
            finalName = $"{finalName} ({rawName})";
    }
    string modOrigin = ResolveModOrigin(od, modIdToName);
    list.Add(new Entry((int)od.objectID, od.variation, finalName, iconCache[od], modOrigin));
}
```

Nachher:
```csharp
foreach (var od in accepted)
{
    long key = DiscoveredState.PackKey((int)od.objectID, od.variation);
    string finalName = localizedNames[key];
    if (nameCount[finalName] > 1)
    {
        string rawName = unlocalizedNames[key];
        if (!string.IsNullOrEmpty(rawName) && rawName != finalName)
            finalName = $"{finalName} ({rawName})";
    }
    string modOrigin = ResolveModOrigin(od, modIdToName);
    list.Add(new Entry((int)od.objectID, od.variation, finalName, iconCache[key], modOrigin));
}
```

- [ ] **Step 3: Loop 2 — α-Enumeration einbauen**

In `Bake()`, NACH Loop 1 und VOR der `// Conflict detection`-Section, den neuen α-Loop einfügen:

```csharp
// ─── Loop 2: α-Enumeration für Cooked-Food-Permutationen ────────
// Pre-cache: ingredient → turnsIntoFood (Base-Tier-Family)
//            family → (rareId, epicId) tier-versions
var turnsInto = new Dictionary<ObjectID, ObjectID>();
var tierMap = new Dictionary<ObjectID, (ObjectID rare, ObjectID epic)>();
foreach (var od in PugDatabase.objectsByType.Keys)
{
    if (od.variation != 0) continue;
    if (PugDatabase.TryGetComponent<CookingIngredientCD>(od, out var ing)
        && !CookedFoodCD.IsIngredientObsolete(od.objectID))
    {
        turnsInto[od.objectID] = ing.turnsIntoFood;
    }
    if (od.objectID.IsCookedFood()
        && PugDatabase.TryGetComponent<CookedFoodCD>(od, out var cf))
    {
        tierMap[od.objectID] = (cf.rareVersion, cf.epicVersion);
    }
}

// Symmetric cartesian: GetFoodVariation(a,b) == GetFoodVariation(b,a),
// so we only iterate j >= i and add each pair once.
var ingredients = new List<ObjectID>(turnsInto.Keys);
for (int i = 0; i < ingredients.Count; i++)
for (int j = i; j < ingredients.Count; j++)
{
    var i1 = ingredients[i];
    var i2 = ingredients[j];
    var primary = CookedFoodCD.GetPrimaryIngredient(i1, i2);
    if (!turnsInto.TryGetValue(primary, out var baseFamily)) continue;
    int variation = CookedFoodCD.GetFoodVariation(i1, i2);

    // 3 tier-variants per pair, same variation, different objectIDs.
    AddCookedEntry(
        new ObjectDataCD { objectID = baseFamily, variation = variation },
        localizedNames, unlocalizedNames, iconCache, accepted);
    if (tierMap.TryGetValue(baseFamily, out var tiers))
    {
        if (tiers.rare != ObjectID.None)
            AddCookedEntry(
                new ObjectDataCD { objectID = tiers.rare, variation = variation },
                localizedNames, unlocalizedNames, iconCache, accepted);
        if (tiers.epic != ObjectID.None)
            AddCookedEntry(
                new ObjectDataCD { objectID = tiers.epic, variation = variation },
                localizedNames, unlocalizedNames, iconCache, accepted);
    }
}
```

- [ ] **Step 4: Build + smoke-test**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7 && \
  source .envrc && ../../../utils/build.sh 2>&1 | tail -5
```

Sven startet CK, lädt Welt. Dann:
```bash
grep "ItemCatalog baked" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -2
```

Erwartet: Catalog-Größe ~9680 (155 Non-Cooked + 9480 Cooked-Permutationen). Wenn vorher 155, jetzt 9635 oder ähnlich.

```bash
grep -c "code security verification failed" "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
```

Erwartet: 0 (oder kein Anstieg gegenüber pre-Iter-3.7).

- [ ] **Step 5: Commit**

```bash
git add unity/ItemChecklist/ItemCatalog.cs
git commit -m "$(cat <<'EOF'
feat(catalog): α-enumeration for cooked-food permutations

Bake() Loop 2 cartesians all (ingredient, ingredient)-pairs from the
~79-item CookingIngredientCD pool, picks the resulting family via
primary.turnsIntoFood (CK's deterministic logic from InventoryUtility.cs),
and emits 3 entries per pair (Base, Rare, Epic tier).

Catalog grows from ~155 to ~9680 entries. Pick-logic empirically verified
against Iter-3.6 discovery-spike (Mushroom×Mushroom → CookedSoup
var=360453500, Mushroom×Tulip → CookedSalad var=524686716 — see spec).

Loop 1 dict containers switched from ObjectDataCD-key to long PackKey
to share with Loop 2.
EOF
)"
```

---

## Task 4: Quote-Display im Window-Title

Der Title zeigt jetzt `Item Checklist — N / M` mit live discovered-Count + Catalog-Total.

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs`

- [ ] **Step 1: `FormatTitle`-Helper hinzufügen**

In `unity/ItemChecklist/ui/ItemChecklistWindow.cs`, eine neue private Methode:

```csharp
private string FormatTitle()
{
    var catalog = ItemChecklistMod.Catalog;
    var state = DiscoveredState.Instance;
    if (catalog == null || catalog.Count == 0)
        return "Item Checklist";
    return $"Item Checklist — {state.Count} / {catalog.Count}";
}
```

- [ ] **Step 2: `ApplyTheme`-Aufrufer für Title umstellen**

In `ApplyTheme()` (Zeile ~74-75):

Vorher:
```csharp
if (title != null)
    title.Render("Item Checklist");
```

Nachher:
```csharp
if (title != null)
    title.Render(FormatTitle());
```

- [ ] **Step 3: Build + smoke-test**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7 && \
  source .envrc && ../../../utils/build.sh 2>&1 | tail -5
```

Sven startet CK, lädt Welt, **öffnet das ItemChecklist-Window** (via F1 oder anderen Trigger, je nach Iter-3.6-Stand). Erwartet im Title: `"Item Checklist — N / ~9680"` (N = Anzahl bereits discoverten Items aus Save).

Bei einem fresh-start-Save: `Item Checklist — 0 / ~9680`. Bei einem alten Iter-3.6-Save: N > 0 mit den schon entdeckten (objId, var)-Tupeln.

- [ ] **Step 4: Commit**

```bash
git add unity/ItemChecklist/ui/ItemChecklistWindow.cs
git commit -m "$(cat <<'EOF'
feat(ui): show discovered/total quote in window title

Title format: "Item Checklist — N / M" where N = DiscoveredState.Count,
M = catalog.Count. Computed via FormatTitle() helper. Single total quote
across all categories per design — no family/tier sub-counters.
EOF
)"
```

---

## Task 5: Live-Title-Refresh via Discovered-Event

Aktualisiert den Title sofort beim ersten Cooking-Event statt erst beim nächsten Window-Open.

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs`

- [ ] **Step 1: Subscribe + OnDiscoveryChanged in Awake / OnDestroy**

In `unity/ItemChecklist/ui/ItemChecklistWindow.cs`:

**1a — Awake erweitern:**

Vorher:
```csharp
protected void Awake()
{
    Instance = this;
    HideUI();
}
```

Nachher:
```csharp
protected void Awake()
{
    Instance = this;
    DiscoveredState.Instance.Changed += OnDiscoveryChanged;
    HideUI();
}
```

**1b — OnDestroy erweitern:**

Vorher:
```csharp
private void OnDestroy()
{
    if (Instance == this) Instance = null;
}
```

Nachher:
```csharp
private void OnDestroy()
{
    DiscoveredState.Instance.Changed -= OnDiscoveryChanged;
    if (Instance == this) Instance = null;
}
```

**1c — Handler hinzufügen (private Methode, sinnvoll neben `FormatTitle`):**

```csharp
private void OnDiscoveryChanged()
{
    // Title-Refresh ist billig; row-level update kommt erst beim nächsten
    // Window-Open (Iter-3.8 könnte single-row-live-update nachrüsten).
    if (title == null) return;
    if (!gameObject.activeSelf) return;
    title.Render(FormatTitle());
}
```

- [ ] **Step 2: Build + smoke-test**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7 && \
  source .envrc && ../../../utils/build.sh 2>&1 | tail -5
```

Sven startet CK, lädt Welt, öffnet ItemChecklist-Window. Notiert N-Wert im Title.

Dann **ein konkretes Cooking-Event triggern** (z.B. ein Cooking-Pot benutzen mit zwei Ingredients, die noch nicht-entdeckte Permutation produzieren). Beobachten ob der Title sich von `N / ~9680` zu `(N+1) / ~9680` aktualisiert, ohne Window-Reopen.

```bash
grep "AddOne" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -5
```

Erwartet: ein `[ItemChecklist] AddOne: (CookedSoup, 360453500) (total N+1)` oder ähnlich pro Cooking-Event.

- [ ] **Step 3: Commit**

```bash
git add unity/ItemChecklist/ui/ItemChecklistWindow.cs
git commit -m "$(cat <<'EOF'
feat(ui): live title-refresh on Discovered events

Window.Awake() subscribes DiscoveredState.Changed → OnDiscoveryChanged,
which re-renders the title via FormatTitle(). Counter updates immediately
when the player cooks a new permutation, without window-reopen. Row-level
live-update remains Iter-3.8 (would require 9680-GameObject spawn per
discovery — needs virtualization first).
EOF
)"
```

---

## Task 6: Performance-Instrumentation

Loggt Bake- und SpawnRows-Zeiten zur Iter-3.8-Trigger-Schwellen-Entscheidung.

**Files:**
- Modify: `unity/ItemChecklist/ItemCatalog.cs`
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs`

- [ ] **Step 1: Bake-Timing-Brackets einbauen**

In `unity/ItemChecklist/ItemCatalog.cs`, im `Bake()`-Body:

**1a — Direkt nach `baking = true; try {` (oben im Try-Block):**

```csharp
float perfT0 = UnityEngine.Time.realtimeSinceStartup;
```

**1b — Direkt vor dem schließenden `}` von `try` (nach dem `Debug.Log($"[ItemChecklist] ItemCatalog baked: {entries.Length} items");`):**

```csharp
float perfTotalMs = (UnityEngine.Time.realtimeSinceStartup - perfT0) * 1000f;
UnityEngine.Debug.Log(
    $"[ItemChecklist] PERF bake-total={perfTotalMs:F0}ms catalog-size={entries.Length}");
```

- [ ] **Step 2: SpawnRows-Timing-Brackets einbauen**

In `unity/ItemChecklist/ui/ItemChecklistWindow.cs`, in der `SpawnRows()`-Methode:

**2a — Direkt nach `ClearRows();` am Anfang:**

```csharp
float perfT0 = UnityEngine.Time.realtimeSinceStartup;
```

**2b — Am Ende der Methode (nach dem `if (scrollWindow != null && ...)`-Block):**

```csharp
float perfMs = (UnityEngine.Time.realtimeSinceStartup - perfT0) * 1000f;
UnityEngine.Debug.Log(
    $"[ItemChecklist] PERF spawn={perfMs:F0}ms rows={_spawnedRows.Count}");
```

- [ ] **Step 3: Build + smoke-test**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-7 && \
  source .envrc && ../../../utils/build.sh 2>&1 | tail -5
```

Sven startet CK, lädt Welt, öffnet ItemChecklist-Window. Dann:
```bash
grep "PERF" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -5
```

Erwartet (mit ~9680 Catalog):
- `[ItemChecklist] PERF bake-total=3000-6000ms catalog-size=9680±50`
- `[ItemChecklist] PERF spawn=Xms rows=9680±50`

**Trigger-Schwellen-Auswertung:**
- `bake-total > 8000ms` → Iter-3.8-Coroutine-Bake-Refactor planen
- `spawn > 2000ms` → Iter-3.8-Virtualisierung dringend
- `spawn < 500ms` → akzeptabel, kein Iter-3.8-UI-Refactor nötig

- [ ] **Step 4: Commit**

```bash
git add unity/ItemChecklist/ItemCatalog.cs unity/ItemChecklist/ui/ItemChecklistWindow.cs
git commit -m "$(cat <<'EOF'
chore(perf): instrument bake + spawn with timing logs

[ItemChecklist] PERF bake-total=…ms catalog-size=… in Catalog.Bake().
[ItemChecklist] PERF spawn=…ms rows=… in Window.SpawnRows().

Iter-3.8 trigger thresholds (per spec): bake > 8s → coroutine refactor;
spawn > 2s → UI virtualization. Both measured empirically post-deploy.
EOF
)"
```

---

## Task 7: End-to-End Verification + Acceptance Smoke-Test

Komplette Spec-Compliance-Validierung im laufenden Spiel. Geht alle 7 Testing-Strategie-Punkte aus der Spec durch.

**Files:** keine (rein Verifikations-Task)

- [ ] **Step 1: Bake-Sanity (Spec-Testing §1)**

Sven startet frisch CK + lädt Welt:
```bash
grep "ItemCatalog baked" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -1
```

Erwartet: `~9680 items` (± mod-set-Variation).

- [ ] **Step 2: Sandbox-Smoke (Spec-Testing §2)**

```bash
grep -i "code security verification failed" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
```

Erwartet: leer (oder kein Iter-3.7-related Event).

- [ ] **Step 3: Discovery-Flow (Spec-Testing §3)**

Sven kocht eine konkrete Mushroom-Suppe. Beobachtet im Title: `N → N+1`.
```bash
grep "AddOne" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -3
```

Erwartet: zumindest ein `[ItemChecklist] AddOne: (objId, variation)`-Eintrag pro Cooking-Event.

- [ ] **Step 4: Loc-Refresh (Spec-Testing §4)**

Sven öffnet in-game Settings → Sprache wechseln (DE↔EN). Catalog re-bakes automatisch.
```bash
grep -E "(ItemCatalog baked|loc-change)" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -5
```

Erwartet: zweiter (oder dritter, …) `ItemCatalog baked: ~9680 items`-Eintrag nach dem Sprach-Switch.

Sven öffnet das Window — Item-Namen sollen in der neuen Sprache sein.

- [ ] **Step 5: Performance-Check (Spec-Testing §5)**

```bash
grep "PERF" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -10
```

Werte vergleichen mit Iter-3.8-Trigger-Schwellen (siehe Task 6 Step 3). Falls Schwellen überschritten: Iter-3.8-Tickets anlegen.

- [ ] **Step 6: Old-Save-Compat (Spec-Testing §6)**

Sven lädt eine alte Iter-3.6-Welt (vor Iter-3.7) mit bereits existierenden Cooked-Food-Discoveries. Title soll N > 0 zeigen — die alten Discoveries werden via `discoveredObjects2` mit den korrekten `(objId, variation)`-Tupeln neu interpretiert.

```bash
grep "AddOne\|Snapshot\|baked" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -10
```

Falls N == 0 trotz alter Discoveries: Save-Migration-Bug, debuggen.

- [ ] **Step 7: Multi-Char-Slot (Spec-Testing §7)**

Sven hat eine Welt mit 2+ Characters. Lädt unterschiedliche Chars, prüft ob der jeweils korrekte Snapshot geladen wird (= der char-spezifische N im Title sich pro Char unterscheidet).

```bash
grep -E "(Snapshot|guid|active)" \
  "$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log" | tail -10
```

Erwartet: pro Char-Switch ein Snapshot-Restore mit dem entsprechenden characterGuid.

- [ ] **Step 8: Final-Commit (falls Tweaks aus den Steps 1-7 nötig waren)**

Falls bei den Step-1-7-Tests Issues gefunden + gefixt wurden, ein Final-Commit:

```bash
git add <modified files>
git commit -m "fix: address acceptance-test findings from Iter-3.7 verification"
```

Sonst: kein Commit, Iter-3.7-Implementation ist fertig.

- [ ] **Step 9: CHANGELOG.md Update (analog Iter-3.6-Pattern)**

`unity/ItemChecklist/`-Repo hat eine `CHANGELOG.md` (laut Spec § "mod.io publishing" im Parent-CLAUDE.md). Eintrag oben:

```markdown
## [0.X.0] — 2026-05-28

### Added
- Variation-aware Cooked-Food tracking — each concrete (ingredient1,
  ingredient2)-permutation is now a separate discovery token in the
  checklist (e.g. Mushroom-Soup ≠ Tomato-Soup), mirroring CK's own
  per-permutation tracking. Catalog grows from ~200 to ~9680 entries
  (15 base recipes × 3 tier-items × symmetric ingredient pairs).
- Discovery-quote display in window title: "Item Checklist — N / M".
- Live title-refresh on discovery events (no window-reopen needed).
- Performance instrumentation via `[ItemChecklist] PERF` log entries.

### Changed
- `DiscoveredState` key schema: `HashSet<int>` → `HashSet<long>` with
  packed `(objectId, variation)` keys.
- `ItemCatalog.Bake()` two-loop architecture: standard items + new
  α-enumeration for Cooked-Food permutations.
- Removed Iter-3.6 DE-locale TrimStart-workaround (no longer needed
  since family-items with variation=0 are filtered out).

### Notes
- Save-compatible with Iter-3.6 saves; no migration code.
- Tier-3 (Rare/Epic) tracking is included but empirically unverified
  with current test character (low cooking talent); will be observed
  post-deploy.
```

Version-Bump abhängig vom aktuellen Stand (siehe `CHANGELOG.md` top). Major-Refactor → minor-version bump (0.X+1.0).

Commit:
```bash
git add CHANGELOG.md
git commit -m "docs(changelog): Iter-3.7 — variation-aware cooked-food tracking"
```

---

## Spec-Coverage-Check

Vor Task-1-Start (oder spätestens vor PR/Merge) gegen die Spec validieren:

| Spec-Section | Plan-Task | Abgedeckt? |
|---|---|---|
| Goals 1 (variation-aware tracking) | Task 1+3 | ✓ |
| Goals 2 (alle 3 Tiers) | Task 3 (3-fach AddCookedEntry) | ✓ |
| Goals 3 (α-Algorithmus) | Task 3 (Loop 2 explizit α) | ✓ |
| Goals 4 (eine Gesamt-Quote) | Task 4 | ✓ |
| Goals 5 (zero-regression) | Task 7 (Acceptance) | ✓ |
| Goals 6 (Save-Kompatibilität) | Task 7 Step 6 | ✓ |
| Architecture: Data-Model | Task 1 | ✓ |
| ItemCatalog.Bake() Refactor | Task 2 + 3 | ✓ |
| Hooks | Task 1 (atomarer Refactor) | ✓ |
| UI: ItemRow | (keine Änderung nötig) | ✓ |
| UI: SpawnRows | Task 1 Step 5 | ✓ |
| UI: Quote-Display | Task 4 | ✓ |
| UI: Live-Refresh | Task 5 | ✓ |
| Sandbox-Risiken | Spike + Task 7 Step 2 | ✓ |
| Performance-Mess-Plan | Task 6 | ✓ |
| Mod-Origin-Pragmatik | (keine Änderung — heutige Logik beibehalten) | ✓ |
| Testing-Strategie | Task 7 (alle 7 Punkte) | ✓ |

---

## Notes für den ausführenden Engineer

- **Reihenfolge ist verbindlich:** Task 1 ist die atomare Foundation. Wenn du sie aufteilst, hast du compile-broken-Zwischenzustände.
- **Build-Time:** Unity-batchmode-Build dauert ~1-3 Min pro Task. Plane das ein.
- **In-Game-Test ist verbindlich:** Sandbox-Pass ist nicht durch Build-Success bewiesen — siehe Memory `project_pugstorm_sandbox_rules`. Welt laden, Player.log greppen.
- **Wenn Schwellen-Werte in Task 6 überschritten werden:** Iter-3.8-Tickets sofort anlegen (Coroutine-Bake / UI-Virtualisierung), nicht in Iter-3.7-Scope ziehen.
- **CK-Start nicht auto-möglich:** Der Engineer muss zwischen jedem Task-Build und Test-Run Sven CK starten + Welt laden lassen. Async!
