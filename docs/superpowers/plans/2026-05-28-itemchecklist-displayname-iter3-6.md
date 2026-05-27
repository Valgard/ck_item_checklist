# ItemChecklist Iter-3.6 (DisplayName-Resolution) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lokalisierte DisplayNames für vanilla- und mod-Items in der Item-Checklist anzeigen, mit PascalCase-Fallback für Mods ohne Loc-Eintrag, ItemBrowser-Pattern-Konflikt-Notes, und Live-Re-Bake bei Sprachwechsel.

**Architecture:** `Catalog.Bake()` wird aus `IMod.Init()` (zu früh) in einen Welt-Load-Harmony-Hook verlagert (Anker via D2 ermittelt). `ResolveDisplayName` splittet in zwei `GetObjectName`-Pässe (lokalisiert + unlokalisiert), erkennt Namens-Konflikte und hängt den unlokalisierten Identifier als Note an. Bei `GetObjectName`-Fail kommt ein neuer pure-Function-`PascalCaseSplitter` als Fallback. Ein I2.Loc-Sprachwechsel-Event triggert synchrones Re-Bake plus optional `ItemChecklistWindow.Instance.RebindRows()`.

**Tech Stack:** Unity 6000.0.59f2 (Mono), CoreKeeperModSDK 4.0.4 mit Game-Runtime CoreLib 4.0.3, Harmony (`HarmonyLib`), I2.Loc (`I2.Loc.LocalizationManager`), CrossOver-Bottle „Core Keeper" auf macOS.

**Spec:** `docs/superpowers/specs/2026-05-28-itemchecklist-displayname-iter3-6-design.md`

**Branch:** `iter-3-6` (von `main @ 97afe68` + Spec-Commit `a6de7fa`)
**Worktree:** `REPO_ROOT/.worktrees/iter-3-6/`
**Player.log path (CrossOver):** `/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log`

---

## File Structure (alle Pfade relativ zum Mod-Repo-Root)

| Datei | Status | Verantwortung |
|---|---|---|
| `unity/ItemChecklist/ItemCatalog.cs` | MODIFIED | `Bake()` 2-Pass + Reentrance-Guard; `ResolveOne()` Helper; `warnedIds`-Cache |
| `unity/ItemChecklist/ItemChecklistMod.cs` | MODIFIED | `Init()`: `Bake()`-Call entfernt, `ItemCatalogLocChangeHook.Subscribe()` hinzugefügt |
| `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | MODIFIED | Static `Instance`-Property + `Awake`/`OnDestroy` Wiring + public `RebindRows()` |
| `unity/ItemChecklist/PascalCaseSplitter.cs` | NEW | Pure-Function `Split(string)` — Splitter für CamelCase/PascalCase-Identifier |
| `unity/ItemChecklist/ItemCatalogWorldLoadHook.cs` | NEW | Harmony-Postfix, ruft `Catalog.Bake()` nach Welt-Load |
| `unity/ItemChecklist/ItemCatalogLocChangeHook.cs` | NEW | Subscribed `LocalizationManager.OnLocalizeEvent` → synchron `Catalog.Bake()` + `Window.RebindRows()` |
| `docs/research/iter-3-6-diagnose.md` | NEW | D1/D2/D3 Diagnose-Ergebnisse aus Phase 0 |
| `docs/research/spike-5-uiscrollwindow-decompile.md` | MODIFIED | Iter-3.6-Closing-Addendum am Ende anhängen |

---

## Task 1: Worktree-Setup + Branch + Plan-Commit

**Files:**
- Create: Worktree at `REPO_ROOT/.worktrees/iter-3-6/`
- Commit: `docs/superpowers/plans/2026-05-28-itemchecklist-displayname-iter3-6.md` (this file, already on main)

- [ ] **Step 1: Invoke `using-git-worktrees` skill to set up the worktree**

The skill creates `REPO_ROOT/.worktrees/iter-3-6/` from `main` and sets up a new branch `iter-3-6`. All subsequent tasks run in that worktree.

- [ ] **Step 2: Verify Plan-Commit auf main + Branch-Base**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git log --oneline -3 main
# Expected: top commit should be either the Plan-Commit (if added) or the Spec-Commit a6de7fa
git -C .worktrees/iter-3-6 log --oneline -2
# Expected: branch iter-3-6 inherits main's history
```

- [ ] **Step 3: Commit dieses Plan-Doc auf main (falls noch nicht committed)**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git add docs/superpowers/plans/2026-05-28-itemchecklist-displayname-iter3-6.md
git commit -m "docs(plan): iter-3.6 displayname-resolution implementation plan"
```

Then fast-forward the worktree:

```bash
cd .worktrees/iter-3-6
git pull --ff-only
```

- [ ] **Step 4: Verify build runs in the worktree**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds, mod is installed into the fake-ID dev location. No source changes yet — this just verifies the worktree wiring is correct.

---

## Task 2: Phase-0 Diagnose-Spike — D1 & D2 Harness

**Files:**
- Create (temp): `unity/ItemChecklist/Iter36DiagnoseSpike.cs` — wird in Task 9 wieder gelöscht

**Spec-Reference:** Spec → „Phase 0 — Diagnose-Spike", Fragen D1 + D2

- [ ] **Step 1: Spike-File anlegen mit D1-Logging in Init() und D2-Triple-Hook**

Create `unity/ItemChecklist/Iter36DiagnoseSpike.cs`:

```csharp
using System;
using HarmonyLib;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// TEMPORARY — Phase-0 diagnose for Iter-3.6. Logs three things:
    ///   D1: outcome of PlayerController.GetObjectName called in Init()
    ///   D2a: when PugDatabase.UpdateEntityMonos first runs + GetObjectName result there
    ///   D2b: when SaveManager.OnSaveLoaded first runs + GetObjectName result there
    ///   D2c: when PlayerController.OnSpawn first runs + GetObjectName result there
    /// Removed in Task 9 once the diagnose-doc is written.
    /// </summary>
    public static class Iter36DiagnoseSpike
    {
        // A known-vanilla objectID that should resolve to "Wood" or similar in EN.
        private const int ProbeObjectId = 5; // Wood — small ID, almost certainly vanilla

        public static void LogProbe(string anchorTag)
        {
            try
            {
                var fields = PlayerController.GetObjectName(
                    new ContainedObjectsBuffer { objectData = new ObjectDataCD { objectID = (ObjectID)ProbeObjectId } },
                    false);
                string text = fields.text ?? "<null>";
                Debug.Log($"[Iter36Diag] anchor={anchorTag} probeID={ProbeObjectId} text=\"{text}\" managerMainPlayerNull={Manager.main?.player == null}");
            }
            catch (Exception ex)
            {
                Debug.Log($"[Iter36Diag] anchor={anchorTag} probeID={ProbeObjectId} THREW {ex.GetType().Name}: {ex.Message} managerMainPlayerNull={Manager.main?.player == null}");
            }
        }
    }

    [HarmonyPatch(typeof(PugDatabase), nameof(PugDatabase.UpdateEntityMonos))]
    internal static class Iter36DiagD2A_UpdateEntityMonos
    {
        private static bool fired;
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (fired) return;
            fired = true;
            Iter36DiagnoseSpike.LogProbe("D2a:PugDatabase.UpdateEntityMonos");
        }
    }
}
```

**Why one anchor per Postfix-class:** Harmony resolves patch-attributes per class at patch-discovery time; one class can only target one method cleanly without manual `[HarmonyTargetMethod]` plumbing. Keeping D2a/D2b/D2c as three separate static classes is clearer than a multi-target patch.

- [ ] **Step 2: D2b und D2c als zwei weitere Patch-Klassen im selben File anhängen**

Append to `unity/ItemChecklist/Iter36DiagnoseSpike.cs`:

```csharp
namespace ItemChecklist
{
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.OnSaveLoaded))]
    internal static class Iter36DiagD2B_OnSaveLoaded
    {
        private static bool fired;
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (fired) return;
            fired = true;
            Iter36DiagnoseSpike.LogProbe("D2b:SaveManager.OnSaveLoaded");
        }
    }

    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.OnSpawn))]
    internal static class Iter36DiagD2C_OnSpawn
    {
        private static bool fired;
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (fired) return;
            fired = true;
            Iter36DiagnoseSpike.LogProbe("D2c:PlayerController.OnSpawn");
        }
    }
}
```

**Important:** if any of these three method-names doesn't compile (e.g. `OnSaveLoaded` doesn't exist), the subagent must look at decompiled `SaveManager` / `PlayerController` in `/tmp/ck-ui-research/` (if present) or via `ilspycmd` against `Pug.Other.dll` / similar, pick the closest equivalent anchor, and document the substitution in the diagnose-doc (Task 3).

- [ ] **Step 3: D1-Probe in `ItemChecklistMod.Init()` einfügen**

In `unity/ItemChecklist/ItemChecklistMod.cs` at the end of `Init()` (after `Catalog.Bake()`), add:

```csharp
public void Init()
{
    Debug.Log("[ItemChecklist] Init");
    Catalog = new ItemCatalog();
    Catalog.Bake();
    Iter36DiagnoseSpike.LogProbe("D1:Init");   // <-- NEW
}
```

- [ ] **Step 4: Build + Install**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds. Install-script copies new mod files into fake-ID dev location.

- [ ] **Step 5: Player.log truncieren**

```bash
: > "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
```

- [ ] **Step 6: User launches Core Keeper, loads a world (any), exits to main menu, quits**

This is a manual step. The subagent must surface a STOP and ask the user to perform this in-game flow:
> Bitte CK starten, eine Welt laden (egal welche Charaktere/Welt), zurück ins Hauptmenü, dann das Spiel beenden. Sag bescheid wenn fertig.

- [ ] **Step 7: Commit WIP**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
git add unity/ItemChecklist/Iter36DiagnoseSpike.cs unity/ItemChecklist/ItemChecklistMod.cs
git commit -m "wip(spike): iter-3.6 diagnose harness — D1 in Init + D2a/D2b/D2c at three candidate world-load anchors"
```

---

## Task 3: Phase-0 Diagnose-Spike — D3 + Ergebnisse-Doc

**Files:**
- Create: `docs/research/iter-3-6-diagnose.md` — Diagnose-Ergebnis-Doku

- [ ] **Step 1: D3 — I2.Loc-Event-Name via Decompile ermitteln**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
# Find the I2.Loc DLL inside the SDK
find "$SDK_PATH" -name "I2Loc*.dll" -o -name "I2.Loc*.dll" 2>/dev/null | head -5
# Decompile the LocalizationManager class and grep for events
ilspycmd -t LocalizationManager "$(find "$SDK_PATH" -name "I2Loc*.dll" | head -1)" \
  | grep -A1 -E "event|OnLocalize|LanguageChanged|delegate"
```

Expected: Should reveal a static event or callback. Most likely candidates: `LocalizationManager.OnLocalizeEvent` (static field, callback type `Action`) or `LocalizationManager.OnLocalizeCallback`. Record the exact symbol name and its type signature.

- [ ] **Step 2: Player.log auswerten — D1 + D2 + Anker-Reihenfolge**

```bash
grep "Iter36Diag" "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
```

Expected output structure (example, actual values from user's run):
```
[Iter36Diag] anchor=D1:Init probeID=5 THREW NullReferenceException: ... managerMainPlayerNull=True
[Iter36Diag] anchor=D2a:PugDatabase.UpdateEntityMonos probeID=5 text="Wood" managerMainPlayerNull=False
[Iter36Diag] anchor=D2b:SaveManager.OnSaveLoaded probeID=5 text="Wood" managerMainPlayerNull=False
[Iter36Diag] anchor=D2c:PlayerController.OnSpawn probeID=5 text="Wood" managerMainPlayerNull=False
```

**Anker-Auswahl-Regel:** Der **erste** Anker (chronologisch nach Timestamp) der ein non-leeres `text`-Feld liefert ist der Gewinner für D2. Wenn alle drei gleichzeitig ein non-leeres Ergebnis liefern: nimm `PugDatabase.UpdateEntityMonos` (ItemBrowsers Wahl, billigste Anker-Method).

- [ ] **Step 3: Diagnose-Doc schreiben**

Create `docs/research/iter-3-6-diagnose.md`:

```markdown
# Iter-3.6 Diagnose Results

**Date:** YYYY-MM-DD (fill in actual)
**Branch:** iter-3-6
**Spike-Build-Commit:** <wip-commit-sha from Task 2 Step 7>

## D1 — Why does GetObjectName fail in Init()?

**Result:** <one of: "THREW <ExceptionType>: <msg>" or "returned empty text" or "returned non-empty text — hypothesis wrong">

**Player.log line:** `<exact line>`

**Conclusion:** <one sentence diagnosing why>

## D2 — Which world-load anchor is correct?

**Tested anchors (chronological order from Player.log):**
| Order | Anchor | text result | managerMainPlayer |
|---|---|---|---|
| 1 | <anchor> | <text or THREW> | <True/False> |
| 2 | <anchor> | <text or THREW> | <True/False> |
| 3 | <anchor> | <text or THREW> | <True/False> |

**Winner:** `<AnchorClass>.<AnchorMethod>` — first anchor that returned non-empty text.

**Substitutions (if any anchor-name failed to compile):** <list>

## D3 — I2.Loc language-change event symbol

**Symbol:** `<namespace>.LocalizationManager.<EventName>` (type: `<signature>`)

**Decompile-line:** `<copied from ilspycmd output>`

**Sandbox status:** <one of: "trusted DLL, += subscribe should work" or "BLOCKED — fallback plan needed">
```

The subagent fills the placeholders with actual values from the Player.log probe.

- [ ] **Step 4: Commit D3 + Diagnose-Doc**

```bash
git add docs/research/iter-3-6-diagnose.md
git commit -m "docs(research): iter-3.6 diagnose results D1/D2/D3"
```

---

## Task 4: PascalCaseSplitter (Phase 1)

**Files:**
- Create: `unity/ItemChecklist/PascalCaseSplitter.cs`
- Verify: inline csc-eval-test (no Unity Test Framework — see spec rationale)

**Spec-Reference:** Spec → „Bake-Logic im Detail → `PascalCaseSplitter.Split`"

- [ ] **Step 1: PascalCaseSplitter.cs schreiben**

Create `unity/ItemChecklist/PascalCaseSplitter.cs`:

```csharp
using System.Text;

namespace ItemChecklist
{
    /// <summary>
    /// Pure, sandbox-safe splitter for CamelCase/PascalCase identifiers.
    /// Used as the final fallback in ItemCatalog.ResolveOne when
    /// PlayerController.GetObjectName cannot resolve a localized name.
    ///
    /// Boundary rules (insert space BEFORE the boundary character):
    ///   (a) Upper after Lower:    "AbandonedC..."  → "Abandoned C..."
    ///   (b) Upper after Digit:    "T1S..."         → "T1 S..."
    ///   (c) Upper after Upper, next is Lower:
    ///       "IOP..." where next is "ort" → "IO Port" (initialism ends)
    ///   (d) Digit after Letter:   "AbandonedT1..." → "Abandoned T1..."
    /// </summary>
    public static class PascalCaseSplitter
    {
        public static string Split(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length + 8);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (i > 0)
                {
                    char prev = input[i - 1];
                    bool isUpper = char.IsUpper(c);
                    bool isDigit = char.IsDigit(c);
                    bool prevLower = char.IsLower(prev);
                    bool prevDigit = char.IsDigit(prev);
                    bool prevUpper = char.IsUpper(prev);
                    bool prevLetter = char.IsLetter(prev);

                    bool boundary = false;
                    if (isUpper && prevLower) boundary = true;                     // (a)
                    else if (isUpper && prevDigit) boundary = true;                // (b)
                    else if (isUpper && prevUpper && i + 1 < input.Length
                             && char.IsLower(input[i + 1])) boundary = true;       // (c)
                    else if (isDigit && prevLetter) boundary = true;               // (d)

                    if (boundary) sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 2: Inline-Verifikation via csc-Eval (vor Unity-Build)**

Run this one-liner to verify the 6 spec cases without a Unity build:

```bash
cat > /tmp/pcs-test.cs <<'EOF'
using System;
using System.Text;

public static class PascalCaseSplitter
{
    public static string Split(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder(input.Length + 8);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (i > 0)
            {
                char prev = input[i - 1];
                bool isUpper = char.IsUpper(c);
                bool isDigit = char.IsDigit(c);
                bool prevLower = char.IsLower(prev);
                bool prevDigit = char.IsDigit(prev);
                bool prevUpper = char.IsUpper(prev);
                bool prevLetter = char.IsLetter(prev);
                bool boundary = false;
                if (isUpper && prevLower) boundary = true;
                else if (isUpper && prevDigit) boundary = true;
                else if (isUpper && prevUpper && i + 1 < input.Length && char.IsLower(input[i + 1])) boundary = true;
                else if (isDigit && prevLetter) boundary = true;
                if (boundary) sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}

public class TestRunner
{
    public static void Main()
    {
        var cases = new (string input, string expected)[]
        {
            ("AbandonedCampMattress", "Abandoned Camp Mattress"),
            ("T1Sword",               "T1 Sword"),
            ("IOPort",                "IO Port"),
            ("",                      ""),
            ("Wood",                  "Wood"),
            ("XMLHttpRequest",        "XML Http Request"),
        };
        int fail = 0;
        foreach (var (input, expected) in cases)
        {
            string got = PascalCaseSplitter.Split(input);
            bool ok = got == expected;
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} \"{input}\" → \"{got}\" (expected \"{expected}\")");
            if (!ok) fail++;
        }
        Environment.Exit(fail);
    }
}
EOF
dotnet-script /tmp/pcs-test.cs 2>&1 || csi /tmp/pcs-test.cs 2>&1
```

Expected: All 6 cases PASS, exit code 0.

If `dotnet-script` and `csi` are both missing, the subagent can defer to the Unity build (Step 3) and verify by adding a one-off `Debug.Log` block in `Init()` that calls all 6 cases. Document the deferral in the commit message.

- [ ] **Step 3: Build**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds (no consumers yet — file just sits in the asmdef).

- [ ] **Step 4: Commit**

```bash
git add unity/ItemChecklist/PascalCaseSplitter.cs
git commit -m "feat(util): PascalCaseSplitter — pure-function splitter for camel-case identifiers, used as DisplayName fallback"
```

---

## Task 5: ItemCatalog Bake-Logic erweitern (Phase 2)

**Files:**
- Modify: `unity/ItemChecklist/ItemCatalog.cs`

**Spec-Reference:** Spec → „Bake-Logic im Detail → Algorithmus" und „ResolveOne(od, dontLocalize)"

- [ ] **Step 1: Imports + private static `warnedIds` + private `baking` Flag hinzufügen**

In `unity/ItemChecklist/ItemCatalog.cs`, after the `public sealed class ItemCatalog` line and before the `Entry` struct, add:

```csharp
        // Per-objectID one-time warning cache. Static so it survives re-bakes
        // (loc-change, world-reload) — the symptom is build-level, not world-level.
        // Not reset by Bake().
        private static readonly System.Collections.Generic.HashSet<ObjectID> warnedIds = new();

        // Re-entrance guard. Bake() must be safe against nested calls when
        // a Loc-change event fires during an in-flight bake. Single-threaded
        // assumption (Unity main thread for Harmony + I2.Loc events).
        private bool baking;
```

- [ ] **Step 2: `Bake()` umbauen — Reentrance-Guard + 2-Pass + Conflict-Notes**

Replace the existing `public void Bake()` method body in `unity/ItemChecklist/ItemCatalog.cs`. The new version:

```csharp
        public void Bake()
        {
            if (baking)
            {
                Debug.LogWarning("[ItemChecklist] Bake() re-entered — skipping nested call");
                return;
            }
            baking = true;
            try
            {
                if (PugDatabase.objectsByType == null)
                {
                    Debug.LogWarning("[ItemChecklist] ItemCatalog.Bake called before PugDatabase ready — skipping");
                    return;
                }

                var modIdToName = new Dictionary<long, string>();
                foreach (var mod in API.ModLoader.LoadedMods)
                {
                    string name = !string.IsNullOrWhiteSpace(mod.Metadata.displayName)
                        ? mod.Metadata.displayName
                        : mod.Metadata.name;
                    modIdToName[mod.ModId] = name ?? string.Empty;
                }

                // First pass: collect localized + unlocalized names per accepted od.
                var localizedNames = new Dictionary<ObjectDataCD, string>();
                var unlocalizedNames = new Dictionary<ObjectDataCD, string>();
                var accepted = new List<ObjectDataCD>();
                var iconCache = new Dictionary<ObjectDataCD, Sprite>();

                foreach (var od in PugDatabase.objectsByType.Keys)
                {
                    if (od.variation != 0) continue;

                    var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
                    if (info == null) continue;

                    if (info.objectType == ObjectType.NonUsable) continue;
                    if (info.objectType == ObjectType.NonObtainable) continue;
                    if (info.objectType == ObjectType.Creature) continue;
                    if (info.objectType == ObjectType.Critter) continue;
                    if (info.objectType == ObjectType.PlayerType) continue;

                    string locName = ResolveOne(od, dontLocalize: false);
                    if (string.IsNullOrWhiteSpace(locName)) continue;

                    string rawName = ResolveOne(od, dontLocalize: true);

                    localizedNames[od] = locName;
                    unlocalizedNames[od] = rawName;
                    accepted.Add(od);
                    iconCache[od] = info.smallIcon != null ? info.smallIcon : info.icon;
                }

                // Conflict detection: count occurrences of each localized name.
                var nameCount = new Dictionary<string, int>(System.StringComparer.Ordinal);
                foreach (var name in localizedNames.Values)
                {
                    nameCount[name] = nameCount.TryGetValue(name, out var c) ? c + 1 : 1;
                }

                // Second pass: build final entries, applying conflict-disambiguation note.
                var list = new List<Entry>(accepted.Count);
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

                entries = list
                    .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                idToIndex.Clear();
                for (int i = 0; i < entries.Length; i++)
                    idToIndex[entries[i].ObjectId] = i;

                Debug.Log($"[ItemChecklist] ItemCatalog baked: {entries.Length} items");
            }
            finally
            {
                baking = false;
            }
        }
```

- [ ] **Step 3: `ResolveDisplayName` ersetzen durch `ResolveOne` mit `dontLocalize`-Parameter**

Replace the existing `private static string ResolveDisplayName(ObjectDataCD od)` method body with:

```csharp
        /// <summary>
        /// Resolve a name for an object. Two-pass-aware: pass dontLocalize=true to get the
        /// raw I2.Loc term (used as conflict-disambiguation note). Falls back to
        /// PascalCaseSplitter.Split(objectID.ToString()) when GetObjectName throws or
        /// returns empty. First exception per objectID is logged once via warnedIds.
        /// </summary>
        private static string ResolveOne(ObjectDataCD od, bool dontLocalize)
        {
            try
            {
                var fields = PlayerController.GetObjectName(
                    new ContainedObjectsBuffer { objectData = od },
                    dontLocalize);
                string text = fields.text;
                if (!string.IsNullOrEmpty(text))
                    return text.Replace("\n", " ");
            }
            catch (System.Exception ex)
            {
                if (warnedIds.Add(od.objectID))
                {
                    Debug.LogWarning(
                        $"[ItemChecklist] GetObjectName({od.objectID}, dontLocalize={dontLocalize}) "
                        + $"threw {ex.GetType().Name}: {ex.Message}");
                }
            }
            return PascalCaseSplitter.Split(od.objectID.ToString());
        }
```

- [ ] **Step 4: Build**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds. (The mod still bakes in Init() — Task 9 moves that out.)

- [ ] **Step 5: Commit**

```bash
git add unity/ItemChecklist/ItemCatalog.cs
git commit -m "feat(catalog): two-pass resolve + conflict-disambiguation notes + reentrance guard"
```

---

## Task 6: ItemCatalogWorldLoadHook (Phase 3)

**Files:**
- Create: `unity/ItemChecklist/ItemCatalogWorldLoadHook.cs`

**Spec-Reference:** Spec → „Hooks, Window-Rebind & Edge Cases → ItemCatalogWorldLoadHook"

**Precondition:** Diagnose-Doc from Task 3 must name the D2 anchor (`<AnchorClass>.<AnchorMethod>`).

- [ ] **Step 1: Hook-File schreiben mit dem D2-Anker**

Create `unity/ItemChecklist/ItemCatalogWorldLoadHook.cs`. Fill in the two placeholder values from `docs/research/iter-3-6-diagnose.md` (D2 winner):

```csharp
using System;
using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Postfix on the world-load anchor identified in Iter-3.6 diagnose D2.
    /// Triggers ItemCatalog.Bake() once the loaded world has its PugDatabase
    /// and Localization subsystems fully initialized. Postfix-pattern (not Prefix)
    /// because the anchor must have completed before GetObjectName can succeed.
    /// </summary>
    [HarmonyPatch(typeof(/* D2 anchor class — fill in from diagnose doc */),
                  nameof(/* D2 anchor method — fill in from diagnose doc */))]
    internal static class ItemCatalogWorldLoadHook
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (ItemChecklistMod.Catalog == null) return;
            try
            {
                ItemChecklistMod.Catalog.Bake();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemChecklist] World-load bake threw: {ex}");
            }
        }
    }
}
```

If the D2 anchor's containing class lives in a namespace that isn't already in the asmdef's references, the subagent may need to add a `using` for it. The asmdef already references `Pug.Other`, `0Harmony`, and `PugMod.SDK.Runtime`, which together cover the three D2 candidates.

- [ ] **Step 2: Build**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds. Hook is auto-discovered by the loader (per CK convention — no manual `Harmony.PatchAll()`).

- [ ] **Step 3: Commit**

```bash
git add unity/ItemChecklist/ItemCatalogWorldLoadHook.cs
git commit -m "feat(hooks): world-load harmony postfix triggers ItemCatalog.Bake()"
```

---

## Task 7: ItemChecklistWindow Singleton + RebindRows (Phase 4a)

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs`

**Spec-Reference:** Spec → „Hooks, Window-Rebind & Edge Cases → ItemChecklistWindow.Instance + RebindRows()"

- [ ] **Step 1: Singleton-Pattern + RebindRows hinzufügen**

In `unity/ItemChecklist/ui/ItemChecklistWindow.cs`:

1. Add a `public static ItemChecklistWindow Instance { get; private set; }` field near the top of the class.
2. In the existing `Awake` method, add `Instance = this;` at the start.
3. Add a new `OnDestroy` method that clears the Instance only when it's still this object (guards Hot-Reload):

```csharp
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
```

4. Add the public `RebindRows()` method:

```csharp
        /// <summary>
        /// Re-binds the visible rows from the current ItemCatalog. Called by
        /// ItemCatalogLocChangeHook after a synchronous re-bake. No-op when the
        /// window is not active — there is nothing to refresh.
        /// </summary>
        public void RebindRows()
        {
            if (!gameObject.activeSelf) return;
            SpawnRows();
        }
```

**Note:** `SpawnRows()` is the existing private method that populates rows from `ItemChecklistMod.Catalog`. Iter-3.5 calls `ResetScroll()` from `SpawnRows()` — that means a Loc-change re-bind resets scroll position. That is acceptable (Loc-change is rare; the user is mid-settings-change, not mid-scroll). If the subagent finds this jarring during Phase-5 testing, they can split `SpawnRows()` into a `SpawnRows(bool resetScroll = true)` overload and pass `false` from `RebindRows()` — but only if the user reports it as a problem.

- [ ] **Step 2: Build**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add unity/ItemChecklist/ui/ItemChecklistWindow.cs
git commit -m "feat(ui): ItemChecklistWindow static Instance + RebindRows() for live-loc-update"
```

---

## Task 8: ItemCatalogLocChangeHook (Phase 4b)

**Files:**
- Create: `unity/ItemChecklist/ItemCatalogLocChangeHook.cs`

**Spec-Reference:** Spec → „Hooks, Window-Rebind & Edge Cases → ItemCatalogLocChangeHook"

**Precondition:** Diagnose-Doc from Task 3 must name the D3 event symbol (`<namespace>.LocalizationManager.<EventName>`) and its sandbox status.

- [ ] **Step 1: Hook-File schreiben mit dem D3-Event-Symbol**

Create `unity/ItemChecklist/ItemCatalogLocChangeHook.cs`. Fill in the using-namespace and event-symbol from `docs/research/iter-3-6-diagnose.md` (D3):

```csharp
using System;
using ItemChecklist.UI;
using UnityEngine;
using /* D3 namespace, typically I2.Loc */;

namespace ItemChecklist
{
    /// <summary>
    /// Subscribes to the I2.Loc language-change event identified in Iter-3.6
    /// diagnose D3. On change, synchronously re-bakes the ItemCatalog and (if
    /// the window is currently active) re-binds its rows. Idle if Catalog is
    /// not yet initialized (e.g. language-change fires in the main menu before
    /// the first world is loaded).
    /// </summary>
    internal static class ItemCatalogLocChangeHook
    {
        private static bool subscribed;

        public static void Subscribe()
        {
            if (subscribed) return;
            // Symbol from D3 diagnose — fill in exact event name.
            LocalizationManager./* OnLocalizeEvent | OnLocalizeCallback | … */ += OnLocalize;
            subscribed = true;
        }

        private static void OnLocalize()
        {
            if (ItemChecklistMod.Catalog == null) return;
            try
            {
                ItemChecklistMod.Catalog.Bake();
                ItemChecklistWindow.Instance?.RebindRows();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemChecklist] Loc-change rebake threw: {ex}");
            }
        }
    }
}
```

**Sandbox-Fallback:** if D3 reported "BLOCKED — fallback plan needed", the subagent must NOT ship this file. Instead, document in the diagnose-doc that Iter-3.6 ships without live-loc-update; the user gets re-baked names on the next world-load. The Re-Bake-on-language-change goal becomes deferred to a follow-up iter. Skip Task 8 Steps 2-3 in that case and proceed to Task 9.

- [ ] **Step 2: Build**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds. (Subscribe is not yet called — Task 9 wires it from `Mod.Init()`.)

- [ ] **Step 3: Commit**

```bash
git add unity/ItemChecklist/ItemCatalogLocChangeHook.cs
git commit -m "feat(hooks): loc-change subscribes I2.Loc event and triggers synchronous rebake + window rebind"
```

---

## Task 9: ItemChecklistMod.Init() Refactor + Spike-Removal (Phase 5)

**Files:**
- Modify: `unity/ItemChecklist/ItemChecklistMod.cs`
- Delete: `unity/ItemChecklist/Iter36DiagnoseSpike.cs` (plus the corresponding `.meta`)

- [ ] **Step 1: Bake-Call aus Init() entfernen + LocChangeHook subscriben + Spike-Probe entfernen**

Replace the body of `ItemChecklistMod.Init()`:

```csharp
        public void Init()
        {
            Debug.Log("[ItemChecklist] Init");
            Catalog = new ItemCatalog();
            ItemCatalogLocChangeHook.Subscribe();
            // Bake() is now triggered by ItemCatalogWorldLoadHook (Phase 3).
        }
```

(If the diagnose D3 was BLOCKED and Task 8 was skipped, omit the `ItemCatalogLocChangeHook.Subscribe()` line and add a `// TODO(iter-3.7): wire loc-change rebake once sandbox allows` comment.)

- [ ] **Step 2: Spike-Files löschen**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
rm unity/ItemChecklist/Iter36DiagnoseSpike.cs
rm -f unity/ItemChecklist/Iter36DiagnoseSpike.cs.meta
```

- [ ] **Step 3: Build**

```bash
source .envrc
../../../utils/build.sh
```

Expected: Build succeeds. Install-script copies the cleaned-up mod into the fake-ID dev location.

- [ ] **Step 4: Commit**

```bash
git add unity/ItemChecklist/ItemChecklistMod.cs unity/ItemChecklist/Iter36DiagnoseSpike.cs unity/ItemChecklist/Iter36DiagnoseSpike.cs.meta
git commit -m "refactor(mod): remove Init() bake-call; subscribe loc-change hook; drop iter-3.6 diagnose spike"
```

---

## Task 10: In-Game Test-Phases (Spec Phases 2–7)

This task is a sequence of manual in-game tests. The subagent must STOP and ask the user to run each scenario, then inspect Player.log together.

**Player.log path:** `/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log`

- [ ] **Step 1: Phase 2 — Vanilla DisplayNames**

Truncate the log, launch CK, load a world, press F1, sample 10 items visually.

```bash
: > "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
```

Surface to user:
> Bitte CK starten, eine Welt laden, F1 drücken. Stichprobe 10 Items: sind die Namen lokalisiert (z.B. „Abandoned Camp Mattress") oder noch PascalCase („AbandonedCampMattress")?

Pass-Kriterium: 10/10 sample items show localized names.

- [ ] **Step 2: Phase 3 — Welt-Load-Hook-Timing**

After Phase 2, inspect the log:

```bash
grep -nE "World-load bake|ItemCatalog baked|Hotkey.*opening UI" "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
```

Pass-Kriterium: An `ItemCatalog baked: <N> items` line appears in the log **before** the first `Hotkey: ... opening UI` line.

- [ ] **Step 3: Phase 4 — Konflikt-Notes**

This phase requires a mod that produces a DisplayName collision (e.g. a second "Sword" or "Wood"). If no such mod is available, mark this phase as "synthetisch nicht testbar" in the closing addendum and proceed. If available:

> Bitte den Konflikt-Mod aktivieren, Welt laden, F1, prüfen ob das kollidierende Item Format „Name (Identifier)" trägt.

Pass-Kriterium: Konflikt-Items zeigen Note-Format.

- [ ] **Step 4: Phase 5 — Loc-Change Re-Bake**

If Task 8 was skipped (sandbox-blocked D3), skip this phase entirely.

Otherwise:
> Bitte CK starten, Welt laden, F1 (DE-Namen prüfen), zurück ins Hauptmenü, Settings → Sprache auf EN, zurück ins Spiel, F1 erneut.

Inspect log:

```bash
grep -nE "ItemCatalog baked|Loc-change rebake threw" "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log"
```

Pass-Kriterium: A second `ItemCatalog baked:` line appears after the language change; items in the F1-window now show EN names; no `Loc-change rebake threw` lines.

- [ ] **Step 5: Phase 6 — Regression Clipping (Iter-3.5c)**

> F1 öffnen, scrollen, prüfen ob Rows beim Verlassen des Window-Viewports geclippt werden (kein Bleed durch den Window-Rahmen). Visual 0-Tolerance vs. Iter-3.5c-Baseline.

Pass-Kriterium: keine sichtbaren Clipping-Regressionen.

- [ ] **Step 6: Phase 7 — Regression Pool-Leak (Iter-3)**

> F1 → ESC → F1 → ESC … 10× wiederholen. Prüfen dass keine Texte „verschwinden" und das Hauptmenü-Text-Rendering intakt bleibt.

Pass-Kriterium: keine verschwundenen Texte, kein Hauptmenü-Schaden.

- [ ] **Step 7: Phase-Pass-Marker committen (optional — pro Phase oder gebündelt)**

```bash
git commit --allow-empty -m "wip(test): phase-2..7 pass — iter-3.6 in-game verification clean"
```

(The `--allow-empty` makes this a marker-commit. Alternative: split into per-phase markers if any phase failed and required a fix-commit.)

---

## Task 11: Closing-Doku — Spike-5-Addendum oder neuer Research-Eintrag

**Files:**
- Modify: `docs/research/spike-5-uiscrollwindow-decompile.md` — anhängen eines Iter-3.6-Closing-Sections
- Modify (eventuell): `docs/research/iter-3-6-diagnose.md` — Status-Update auf "RESOLVED"

- [ ] **Step 1: Iter-3.6-Closing-Section an spike-5-Doc anhängen**

Append a section to `docs/research/spike-5-uiscrollwindow-decompile.md`:

```markdown
## Iter-3.6 Closing-Addendum (YYYY-MM-DD)

DisplayName-Resolution produktiv:
- Vanilla-Items zeigen lokalisierte Namen (Phase 2 PASS, sample 10/10)
- Welt-Load-Hook-Timing korrekt (Phase 3 PASS, log timestamp before first F1)
- Konflikt-Notes: <PASS / synthetisch nicht testbar>
- Loc-Change-Re-Bake: <PASS / sandbox-blocked, deferred to iter-3.7>
- Regression Iter-3.5c-Clipping: PASS
- Regression Iter-3-Pool-Leak: PASS

Anker-Entscheidungen (D1/D2/D3) siehe `docs/research/iter-3-6-diagnose.md`.

Branch `iter-3-6` ff-merged auf main per `<merge-base>..<final-sha>`.
```

- [ ] **Step 2: Commit**

```bash
git add docs/research/spike-5-uiscrollwindow-decompile.md
git commit -m "docs(spike-5): close iter-3.6 addendum — displayname-resolution produktiv"
```

---

## Task 12: Merge auf main (fast-forward, kein Squash)

**Important:** Per Sven's preference (and `feedback_frequent_wip_commits_for_bisect`): WIP-commits bleiben preserved, kein squash-merge.

- [ ] **Step 1: CWD aus dem Worktree raus, dann ff-merge**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git fetch . iter-3-6:iter-3-6  # noop if branch already exists locally
git merge --ff-only iter-3-6
git log --oneline -20
```

Expected: All iter-3.6 commits visible as individual entries on main; no merge-commit, no squash.

- [ ] **Step 2: Worktree-Preflight (per `feedback_worktree_remove_preflight`)**

Before removing the worktree:

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-6
git ls-files --others --exclude-standard
git status --short
```

Expected: clean / empty. If anything is listed: STOP and ask the user.

- [ ] **Step 3: Worktree entfernen**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git worktree remove .worktrees/iter-3-6
git branch -d iter-3-6
git worktree list
```

Expected: only the main worktree remains.

- [ ] **Step 4: Memory updaten — `project_item_checklist_ui_pivot_state`**

The memory needs the Iter-3.6 status updated. Read the current file, find the `## Pending` section, move the Iter-3.6 entry to `## Gelöst bisher`, and adjust the headline `description` field to reflect Iter-3.6 DONE.

```bash
# Path:
ls /Users/valgard/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md
```

Update content:
- Top of file `description:` field — add Iter-3.6 DONE marker
- `## Gelöst bisher` — add a bullet for Iter-3.6 with a one-line summary
- `## Pending` — remove the Iter-3.6 bullet
- Update the "Pending (geordnet nach Priorität)" intro line if it still lists Iter-3.6

No further commit needed (memory file edits are session-state, not git).

---

## Self-Review Notes

**Spec coverage check:**
- ✅ Goal "Vanilla-DisplayName-Fix" → Tasks 2–6 (diagnose + bake-timing + 2-pass)
- ✅ Goal "Mod-Items-Fallback" → Task 4 (PascalCaseSplitter) + Task 5 (ResolveOne fallback)
- ✅ Goal "Konflikt-Disambiguierung" → Task 5 (conflict-detection + note)
- ✅ Goal "Live-Sprachwechsel" → Task 7 (Singleton + RebindRows) + Task 8 (LocChangeHook)
- ✅ Goal "Diagnose-First" → Tasks 2–3
- ✅ Goal "Zero-Regression" → Task 10 Steps 5–6 (Phase 6+7 tests)
- ✅ Commit-Strategie "WIP-commits preserved" → Task 12 (ff-merge only)
- ✅ Edge Cases from Spec → all covered: Catalog==null guards (Tasks 6+8), Reentrance-Guard (Task 5), Singleton OnDestroy-guard (Task 7), warnedIds-cache (Task 5), Sandbox-Fallback (Task 8 Step 1 note + Task 9 Step 1 fallback)

**Type consistency check:**
- `Catalog` (property on `ItemChecklistMod`) — referenced consistently in Tasks 5, 6, 8, 9
- `Bake()` — referenced consistently in Tasks 5, 6, 8
- `Instance` (static on `ItemChecklistWindow`) — referenced consistently in Tasks 7, 8
- `RebindRows()` — referenced consistently in Tasks 7, 8
- `ResolveOne` / `dontLocalize` — referenced consistently in Task 5
- `warnedIds` (static HashSet) — defined and used only in Task 5

**Placeholder scan:** Only intentional placeholders remain (`<AnchorClass>`, `<AnchorMethod>`, `<EventName>`), each marked with a comment pointing to the diagnose-doc that fills them. No TODO/TBD elsewhere.
