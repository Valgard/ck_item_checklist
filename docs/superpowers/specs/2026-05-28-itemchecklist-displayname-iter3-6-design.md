# ItemChecklist — Iteration 3.6 Design (DisplayName-Resolution)

**Date:** 2026-05-28
**Status:** Design approved (5 sections). Pending: spec self-review → user review → writing-plans.
**Branch:** `iter-3-6` (anzulegen, Worktree `REPO_ROOT/.worktrees/iter-3-6/` aus `main` @ `97afe68`)
**Prerequisite reading:**
- Iter-3.5c Spec (Clipping, schließt Iter-3 Bug-Quartett ab): `docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5c-design.md`
- Iter-3.5c Spike-5-Addendum: `docs/research/spike-5-uiscrollwindow-decompile.md`
- ItemCatalog aktueller Stand: `unity/ItemChecklist/ItemCatalog.cs`
- ItemBrowser-Vorlage: `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Utilities/ObjectUtility.cs` (Methode `BakeDisplayNamesAndNotes`)

**Environment-Kontext:**
- CK Game-Version: `1.2.1.4` (Iter-3.5c hat Patch-4 freigeschaltet)
- CoreLib Runtime: `4.0.3` (mod.io-display "4.0.4")
- CoreLib SDK Build-Time: `4.0.4`
- Iter-3.5c-Stand: produktiv auf main (`97afe68`), 4-Bug-Quartett abgeschlossen (Pool-Leak + Scroll + Clipping + Layout-Side-Effects)
- HEAD vor Iter-3.6-Start: `97afe68` (Iter-3.5c Spike-5-Close)

## Context

Vanilla-Items werden im Window mit ihren PascalCase-Identifiern angezeigt
(„AbandonedCampMattress", „AbandonedCampTent") statt mit lokalisierten
Display-Namen („Abandoned Camp Mattress", „Abandoned Camp Tent"). Code-Pfad:
`ItemCatalog.cs:139 ResolveDisplayName` ruft
`PlayerController.GetObjectName(...)`, läuft entweder in einen catch oder kriegt
einen leeren `text` zurück und fällt zurück auf `od.objectID.ToString()` (= der
Enum-Name in PascalCase).

Der ursprüngliche Iter-3.6-Scope war „mod-Items-Fallback", das Symptom betrifft
aber **alle** Items — Vanilla wie Mod. Der erweiterte Scope deckt die komplette
DisplayName-Resolution-Pipeline ab, inklusive Lifecycle-Timing
(wann/wo `Bake()` läuft), Konflikt-Disambiguierung (ItemBrowser-Pattern) und
Live-Update bei Sprachwechsel.

## Goals

- **Vanilla-DisplayName-Fix**: Items werden mit ihren lokalisierten Namen
  angezeigt (z.B. „Abandoned Camp Mattress")
- **Mod-Items-Fallback**: Mod-Items ohne I2.Loc-Eintrag fallen auf einen
  prettifizierten PascalCase-Splitter zurück („FasterTalents:SkillTome" →
  „Skill Tome") statt den rohen Identifier zu zeigen
- **Konflikt-Disambiguierung**: bei DisplayName-Kollision zwischen zwei Items
  wird der unlokalisierte Name als Note in Klammern angehängt
  (ItemBrowser-Pattern: „Sword (CopperSword)" vs. „Sword (IronSword)")
- **Live-Sprachwechsel**: ändert der User die Sprache mid-game, wird der
  Catalog **synchron** re-baked und ein offenes Window re-binded seine Rows
- **Diagnose-First**: drei explizit benannte Unbekannte (D1/D2/D3) werden in
  einer vorgeschalteten Spike-Phase empirisch beantwortet, bevor
  Implementation startet — vermeidet falsche Lösungs-Richtung
- **Zero-Regression**: alle bisherigen Iter-Wins (Pool-Leak-Fix, Scroll,
  Clipping, Layout-Side-Effects) bleiben intakt

## Non-Goals

- F1-Toggle UX (Iter-4)
- Listen-Sortierung über alphabetisch hinaus (Iter-5)
- Filter-Dropdown + Suche (Iter-6)
- Window-/Style-Polish (Iter-7)
- Per-Variation-Tracking (Iter-3.6 bleibt bei `variation == 0`-Filter wie
  heute; ItemBrowsers Skin-Variation-Handling ist Out-of-Scope)
- Unit-Test-Infrastruktur (es gibt keine; CK-Singletons lassen sich nicht
  isoliert testen — manuelle Phase-Tests bleiben Standard)

## Decisions Made During Brainstorming

| Frage | Entscheidung | Begründung |
|---|---|---|
| Diagnose-Status | **Vermutung, nicht verifiziert** → Diagnose-Phase 0 vorgeschaltet | Vermeidet `feedback_avoid_inverse_inference_fallacy`-Risiko |
| Scope-Umfang | **Voll**: Vanilla-Fix + Mod-Fallback + Disambig-Notes + Loc-Change-Re-Bake | User-Entscheidung; substantieller als Iter-3/3.5 aber in einem Iter machbar |
| Bake-Trigger | **Welt-Load-Harmony-Hook** (Anker via D2 festzulegen) | Catalog ist beim 1. F1 warm; Bake-Spike geht im Welt-Load unter |
| Fallback-Format | **PascalCase-Splitter** + Debug.LogWarning beim 1. Catch pro objectID | Casual-UX vor Diagnose-Sichtbarkeit; LogWarning behält Dev-Diagnose |
| Re-Bake bei Sprachwechsel | **Synchron** (nicht Stale-Flag) + Window-Rebind falls aktiv | Live-Konsistenz beim Wechsel; offenes Window darf nicht stale bleiben |
| Commit-Strategie | **WIP-Commits bleiben preserved**, kein Squash, fast-forward Merge | User-Präferenz; konsistent mit Iter-3.5c (`bde774a..f3b25aa` ff-merged) |

## Architecture-Übersicht

Drei Lifecycle-Phasen, drei Verantwortlichkeiten:

1. **Initial-Bake**: Welt-Load-Harmony-Hook ruft `Catalog.Bake()`; Catalog ist
   warm, bevor F1 zum ersten Mal gedrückt wird.
2. **Re-Bake**: Sprachwechsel-Event ruft **synchron** `Catalog.Bake()` und —
   falls das Window gerade aktiv ist — `ItemChecklistWindow.Instance.RebindRows()`.
   Wenn das Window inaktiv ist, ist der zweite Schritt no-op.
3. **Resolution-Time**: Innerhalb von `Bake()` zwei-pass `GetObjectName`
   (lokalisiert + unlokalisiert), Konflikt-Erkennung, PascalCase-Splitter als
   letzte Linie.

### Wesentliche Code-Changes

- `ItemChecklistMod.Init()`: ruft **nicht mehr** `Catalog.Bake()` — nur noch
  `Catalog = new ItemCatalog()` und subscribed den Loc-Change-Hook.
- `ItemCatalog.ResolveDisplayName()`: erweitert um zweiten unlokalisierten
  `GetObjectName`-Pass + Konflikt-Note-Logic + PascalCase-Splitter-Fallback.
- `ItemCatalog.Bake()`: Reentrance-Guard (single-Thread-Annahme dokumentiert),
  zwei Temp-Dicts für lokalisierte + unlokalisierte Namen, Konflikt-Pass.
- `ItemChecklistWindow`: neue public-Method `RebindRows()` + Singleton-Pattern
  via `Instance`-Property mit `OnDestroy`-Cleanup.
- Neue Klasse `ItemCatalogWorldLoadHook` (Harmony-Patch, ~40 LOC).
- Neue Klasse `ItemCatalogLocChangeHook` (Subscribe + synchroner Re-Bake +
  Rebind, ~30 LOC).
- Neue Utility `PascalCaseSplitter` (pure-Function, ~20 LOC, sandbox-safe).

### File-Layout

```
unity/ItemChecklist/
  ItemCatalog.cs                  [MODIFIED — Resolve-Logic erweitert + Reentrance-Guard]
  ItemChecklistMod.cs             [MODIFIED — Bake-Call entfernt aus Init(); Loc-Hook subscribe]
  ui/ItemChecklistWindow.cs       [MODIFIED — public RebindRows() + static Instance]
  ItemCatalogWorldLoadHook.cs     [NEW — Harmony-Patch, ~40 LOC]
  ItemCatalogLocChangeHook.cs     [NEW — Subscribe + synchroner Re-Bake + Rebind, ~30 LOC]
  PascalCaseSplitter.cs           [NEW — Pure utility, ~20 LOC]
```

## Phase 0 — Diagnose-Spike (vorgeschaltet)

Drei Unbekannte, deren konkrete Antwort den Code formt. Alle drei in einem
einzigen Debug-Build mit ~10 Logging-Statements beantwortbar.

| # | Frage | Diagnose-Methode |
|---|-------|-----------------|
| D1 | **Warum schlägt `GetObjectName` in `Init()` fehl?** Exception (welche?) oder leerer `text`? | `Debug.Log` mit `try/catch` um `GetObjectName`, `ex.GetType().Name` und `ex.Message` loggen. Plus: `Manager.main?.player == null`-Check loggen. |
| D2 | **Welcher Welt-Load-Anker ist korrekt?** PugDatabase ready-state vs. Localization ready-state vs. PlayerController ready-state. | Drei Harmony-Patches gleichzeitig setzen (z.B. `PugDatabase.UpdateEntityMonos`, `SaveManager.OnSaveLoaded`, `PlayerController.OnSpawn`). Jeder loggt seinen Eintritt + Test-Aufruf `GetObjectName(<known-vanilla-objectID>)`. Wer als erstes ein non-leeres Ergebnis liefert, gewinnt. |
| D3 | **Wie heißt das I2.Loc-Sprachwechsel-Event?** Vermutung: `LocalizationManager.OnLocalizeEvent` (statisch) oder `LocalizationManager.OnLocalizeCallback`. | `ilspycmd` auf die I2.Loc-DLL — `grep` nach `OnLocalize` und `LanguageChanged`. Plus Sandbox-Check (statische Subscription auf trusted-DLL-Events sollte funktionieren, aber `project_pugstorm_sandbox_rules` mahnt). |

**Ergebnis-Form:** Notiz-File `docs/research/iter-3-6-diagnose.md` mit:
- Die exakte Exception (oder bestätigt: leerer Text) von D1
- Die Anker-Wahl + Player.log-Zeitstempel-Sequenz von D2 (welcher Anker feuert
  **vor** dem ersten möglichen F1-Press)
- Das exakte Event-Symbol + Sandbox-Status von D3

Erst nach diesem Notiz-File wird die Implementation-Phase committed.

## Bake-Logic im Detail

### Algorithmus (`ItemCatalog.Bake()` neu)

```
1. Reentrance-Guard: if (baking) return; baking = true; try { … } finally { baking = false; }

2. Pre-Reqs: PugDatabase.objectsByType != null  (sonst log+return wie bisher)

3. Pre-Resolve modIdToName Map  (unverändert wie heute)

4. Zwei lokale Temp-Dicts:
   localizedNames  : Dictionary<ObjectDataCD, string>
   unlocalizedNames: Dictionary<ObjectDataCD, string>

5. Erster Pass über PugDatabase.objectsByType.Keys:
   - Skip variation != 0
   - Skip ObjectType in {NonUsable, NonObtainable, Creature, Critter, PlayerType}
   - localizedNames[od]   = ResolveOne(od, dontLocalize: false)
   - unlocalizedNames[od] = ResolveOne(od, dontLocalize: true)

6. Konflikt-Erkennung:
   - Build Dictionary<string, int> nameCount aus localizedNames.Values
   - Eine Name-String ist "Konflikt" ⇔ nameCount[name] > 1

7. Zweiter Pass: für jeden Eintrag im Temp-Dict:
   - finalName = localizedNames[od]
   - Wenn IsConflict(finalName) UND unlocalizedNames[od] ≠ finalName:
       finalName = $"{finalName} ({unlocalizedNames[od]})"
   - icon, modOrigin wie heute
   - list.Add(new Entry(...))

8. Sort + idToIndex wie heute (unverändert).
```

### `ResolveOne(od, dontLocalize)` (neue private Helper-Method)

```
try:
   fields = PlayerController.GetObjectName(
              new ContainedObjectsBuffer { objectData = od },
              dontLocalize)
   text = fields.text
   if (!string.IsNullOrEmpty(text)):
       return text.Replace("\n", " ")
catch (Exception ex):
   if (warnedIds.Add(od.objectID)):    // einmaliges Warning pro objectID
       Debug.LogWarning(
         $"[ItemChecklist] GetObjectName({od.objectID}, "
         + $"dontLocalize={dontLocalize}) threw {ex.GetType().Name}: {ex.Message}")

return PascalCaseSplitter.Split(od.objectID.ToString())
```

`warnedIds` ist ein `HashSet<ObjectID>` als private static im `ItemCatalog`.
Wird bei `Bake()` **nicht** zurückgesetzt — Re-Bakes pro Welt-Load sollen nicht
erneut warnen (Symptom ändert sich pro Build, nicht pro Welt).

### `PascalCaseSplitter.Split(string)` — Algorithmus

```
Input : "AbandonedCampMattress"  →  Output: "Abandoned Camp Mattress"
Input : "T1Sword"                →  Output: "T1 Sword"     (Number-Boundary)
Input : "IOPort"                 →  Output: "IO Port"      (Initialism-Boundary)
Input : ""                       →  Output: ""

Regel: Vor jeden Upper-Case-Char ein Space einfügen, wenn:
  (a) der vorherige Char Lower-Case ist     → "AbandonedC..."-Boundary
  (b) der vorherige Char Digit ist          → "T1S..."-Boundary
  (c) der vorherige Char Upper UND der nächste Char Lower ist → "IOP..."-Boundary
      (Initialism endet, neues Word beginnt)
Zusätzlich: Vor jede Digit-Run ein Space, wenn der vorherige Char Letter ist
            → "AbandonedT1..." → "Abandoned T1..."
```

Pure-Function, statisch, sandbox-safe (kein System.IO, kein Reflection).

### Was unverändert bleibt

- `ResolveModOrigin` und seine Aufruf-Site
- `OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)` — sortiert
  nach dem **finalen** DisplayName (inkl. eventueller Note); semantisch korrekt
- `idToIndex` Map und alle externen APIs (`Count`, `GetByIndex`, `TryGetIndex`)

## Hooks, Window-Rebind & Edge Cases

### `ItemCatalogWorldLoadHook` (NEW)

```
[HarmonyPatch(typeof(<AnchorClass>), nameof(<AnchorMethod>))]
internal static class ItemCatalogWorldLoadHook
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ItemChecklistMod.Catalog == null) return;  // EarlyInit-Race-Defense
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
```

`<AnchorClass>` und `<AnchorMethod>` werden in der Diagnose-Phase (D2)
festgenagelt. Postfix-Pattern statt Prefix, weil wir vom Anker das
*fertig-initialisierte* PugDatabase/Localization-Subsystem brauchen.

### `ItemCatalogLocChangeHook` (NEW)

```
internal static class ItemCatalogLocChangeHook
{
    private static bool subscribed;

    public static void Subscribe()
    {
        if (subscribed) return;
        LocalizationManager.OnLocalizeEvent += OnLocalize;  // exaktes Symbol via D3
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
```

Subscribe-Aufruf in `ItemChecklistMod.Init()` direkt nach `Catalog = new ItemCatalog()`.

### Reentrance-Guard in `ItemCatalog.Bake()`

```
private bool baking;

public void Bake()
{
    if (baking)
    {
        Debug.LogWarning("[ItemChecklist] Bake() re-entered — skipping nested call");
        return;
    }
    baking = true;
    try { /* … bestehende + neue Logic … */ }
    finally { baking = false; }
}
```

Nicht thread-safe (kein `lock`) — Annahme: alle Bake-Aufrufe laufen auf
Unity-Main-Thread (Harmony-Postfix + I2.Loc-Event sind beide Main-Thread).
Annahme als Doc-Comment am `Bake()` festhalten.

### `ItemChecklistWindow.Instance` + `RebindRows()` (NEW Public Surface)

```
public static ItemChecklistWindow Instance { get; private set; }

private void Awake() { Instance = this; /* … */ }
private void OnDestroy() { if (Instance == this) Instance = null; }

public void RebindRows()
{
    if (!gameObject.activeSelf) return;          // Window zu → no-op
    SpawnRows();                                  // re-binds aus aktuellem Catalog
}
```

Singleton-Pattern hier zulässig: CoreLib's `UserInterfaceModule` instanziiert
pro registriertem UI **ein** Window. `OnDestroy`-Guard schützt gegen
Hot-Reload-Edge-Cases.

### Edge Cases

| Edge Case | Behandlung |
|-----------|-----------|
| Loc-Change feuert vor Initial-Welt-Load (Sprache im Hauptmenü gewechselt) | `Catalog == null` Check im Hook → silent no-op. Initial-Bake nach Welt-Load nimmt die neue Sprache automatisch. |
| Welt-Load-Hook feuert mehrfach pro Welt-Load | `Bake()` ist re-entry-safe und idempotent (entries/idToIndex werden komplett ersetzt). |
| Window offen während Loc-Change (Settings-Menü schließt es nicht) | `RebindRows()` re-binded sichtbare Rows aus dem frisch gebakten Catalog. |
| GetObjectName wirft auf **jedem** Item (D1 sagt z.B. „PlayerController == null") | Erste ~1500 LogWarnings auf Unique-IDs limitiert via `warnedIds`; alle Items kriegen PascalCase-Splitter-Fallback. User sieht alles, kein Crash. |
| Mod-Item dessen `objectID.ToString()` ein numerisches Format ist (z.B. weil Enum-Mapping fehlt) | PascalCaseSplitter liefert die Zahl unverändert zurück — keine schöne UI, aber kein Crash. Diagnose-LogWarning sagt warum. |
| Sandbox blockiert `LocalizationManager.OnLocalizeEvent += …` (unwahrscheinlich, weil I2.Loc trusted ist) | D3 verifiziert vorab; wenn doch geblockt: Re-Bake fällt zurück auf „nächster Welt-Load nimmt neue Sprache mit". Spec wird in dem Fall korrigiert (Stale-Flag statt direkter Re-Bake), kein Hard-Block für Iter-3.6. |

## Testing-Strategie

Es gibt keine Unit-Test-Infrastruktur für CK-Mods — `PugDatabase`,
`PlayerController`, `LocalizationManager` sind alle Editor-/Runtime-Singletons,
die ein laufendes CK voraussetzen. Testing ist manuell + log-instrumentiert,
mit Player.log als Primary-Datenquelle. Einzige Ausnahme: `PascalCaseSplitter`
ist eine pure-Function — Inline-Verifikation im Code-Review reicht.

### Phase-Tests

| Phase | Test | Pass-Kriterium |
|-------|------|---------------|
| **0. Diagnose-Spike** | Spike-Build laden, ins Hauptmenü, Welt-Load, Player.log inspizieren | D1/D2/D3 alle drei beantwortet + dokumentiert in `docs/research/iter-3-6-diagnose.md` |
| **1. PascalCaseSplitter** | csc-eval Inline-Test mit 6 Cases (siehe Algorithmus oben) | Alle 6 Cases liefern erwarteten Output |
| **2. Bake-Logic-Erweiterung** | Welt mit 1.2.1.4 vanilla-Items laden, F1, sichtprüfen | Items werden mit lokalisierten Namen angezeigt; Stichprobe von 10 Items |
| **3. Welt-Load-Hook** | Player.log: Bake-Log-Zeile feuert nach Welt-Load und **vor** erstem F1-Press | Log-Timestamp-Reihenfolge bestätigt |
| **4. Konflikt-Notes** | Welt mit Konflikt-erzeugendem Mod laden (falls verfügbar) | Konflikt-Items zeigen `Name (Identifier)` Format |
| **5. Loc-Change Re-Bake** | Welt laden, F1 (Items in DE), Hauptmenü → Settings → Sprache auf EN, zurück, F1 erneut | Items zeigen jetzt EN-Namen; falls Window während Wechsel offen: Re-Bind verifiziert |
| **6. Regression Iter-3.5c (Clipping)** | F1 öffnen, scrollen, prüfen dass Mask + Sorting-Layers unverändert funktionieren | Visual: 0-Tolerance — keine sichtbaren Veränderungen zur Iter-3.5c-Baseline |
| **7. Regression Iter-3 (Pool-Leak)** | Multi-Open F1 → ESC → F1 → ESC … x10 | Keine „verschwindenden" Texte, keine Hauptmenü-Text-Regressionen |

### Test-Welt-Setup

- Bestehende Sandbox-Welt aus Iter-3.5c (oder neu, da Iter-3.6 nicht
  save-game-relevant ist)
- Vanilla-Mode mit nur ItemChecklist + CoreLib aktiviert für Test 2, 3, 5, 6, 7
- Für Test 4 zusätzlich ein Konflikt-erzeugender Mod (falls keiner existiert:
  Test wird optional und im Spec dokumentiert als „nice-to-have, nur via
  Synthese verifizierbar")

## Commit-Strategie

WIP-Commits nach jeder Phase. Diese bleiben **als-ist** auf dem Branch — sie
sind die Entwicklungsgeschichte und werden vor dem Merge **nicht** gesquashed.
Konsequenzen:

- Jede Phase ist als eigener Commit bisect-fähig — eine später entdeckte
  Regression lässt sich präzise einer Phase zuordnen.
- Commit-Nachrichten dürfen ihren WIP-Charakter behalten (z.B.
  `wip(spike): iter-3.6 diagnose harness — D1 result`); aussagekräftiger
  Subject + ggf. Body, aber kein Zwang zu fertigen `feat:`-Messages.
- Merge auf main passiert per fast-forward (kein squash, kein merge-commit),
  genauso wie `iter-3-5c` in `bde774a..f3b25aa` ff-merged wurde.
- Plan-Doku-Commit und Spec-Doku-Commit sind eigenständig — werden separat
  committed (typischerweise als Erstes auf dem neuen Branch).

### Beispielhafte Commit-Sequenz (illustrativ, nicht präskriptiv)

1. `docs(spec): iter-3.6 displayname-resolution design`
2. `docs(plan): iter-3.6 displayname-resolution implementation plan`
3. `wip(spike): iter-3.6 diagnose harness — try GetObjectName in Init`
4. `wip(spike): iter-3.6 diagnose — three candidate world-load anchors`
5. `wip(spike): iter-3.6 diagnose — I2.Loc OnLocalize symbol verified`
6. `docs(research): iter-3.6 diagnose results D1/D2/D3`
7. `feat(util): PascalCaseSplitter`
8. `feat(catalog): two-pass resolve + conflict-disambiguation`
9. `feat(hooks): world-load bake hook`
10. `feat(hooks): loc-change rebake hook + window rebind`
11. `refactor(mod): remove Init() bake call; subscribe loc-change`
12. `wip(test): phase-2 pass — vanilla items show localized names`
13. … weitere Phase-PASS-Marker je nach Bedarf
14. `docs(spike-5): close iter-3.6 addendum`

Diese Sequenz ist ein Vorschlag — der ausführende Subagent kann die
Granularität anpassen, solange jede signifikante Code-Phase einen eigenen
Commit kriegt.

## Cross-references

- [[project_item_checklist_ui_pivot_state]] — Iter-3.6 Pending-Eintrag mit
  ursprünglicher Fragenliste (a/b/c) — wird nach Implementation als DONE
  markiert
- [[project_pugstorm_sandbox_rules]] — Sandbox-Constraints für Harmony-Hooks
  und Static-Event-Subscriptions
- [[project_corekeeper_compile_fail_cascade]] — Hintergrund für Harmony-Patch-
  Discovery und Compile-Fail-Risiken bei neuen Patches
- [[feedback_native_first_then_harmony]] — Vorgehen: ItemBrowser-Pattern
  übernommen statt Custom-Resolution-Pipeline
- [[feedback_avoid_inverse_inference_fallacy]] — explizit referenziert im
  Diagnose-First-Goal; vermeidet Property-Inferenz-Fehler bei der
  Root-Cause-Bestimmung
- [[feedback_deep_spike_unfamiliar_internals]] — Phase 0 ist die explizite
  Anwendung dieser Regel
- [[feedback_frequent_wip_commits_for_bisect]] — Commit-Strategie folgt
  dieser Regel (Korrektur: keine Squash-Phase vor Merge)
