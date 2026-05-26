# ItemChecklist UI Pivot — Iteration 3.5 Design (Scroll-Fix via Decompile-Spike)

**Date:** 2026-05-26
**Status:** Design approved (5 sections). Pending: spec write → spec self-review → user review → writing-plans.
**Branch:** `iter-3-5` (wird angelegt als Worktree `REPO_ROOT/.worktrees/iter-3-5/` aus `main` @ `c3fa944`)
**Prerequisite reading:**
- Iter-3 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md`
- Iter-2 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
- Spike-4 (UI architecture): `docs/research/spike-4-ui-architecture.md`

**Environment-Kontext:**
- CK Game-Version: `1.2.1.3-4986`
- CoreLib Runtime: `4.0.3` (binary identisch mit GitHub-Commit `3c99dc44`, von mod.io als "4.0.4"-Display gelabelt)
- CoreLib SDK Build-Time: `4.0.4` (Tag `31242dbe`)
- Iter-3-Mod-Stand: `pugText.Clear()`-Loop in ClearRows (commit `c3fa944` auf main)

## Context

Iter-2 PARTIAL hat 3 Bugs deferred. Iter-3 hat 1 davon gefixt (Pool-Leak via `pugText.Clear()`). Es bleiben offen:
- **Mouse-wheel scroll dead** (diese Iter)
- **DisplayName-Fallback ordering** (Iter-3.6, separat)

Iter-3 hat einen Scroll-Fix-Versuch via `scrollWindow.SetScrollValue(0f)` empirisch widerlegt: die Methode hat **destruktive Side-Effects** auf das Layout (Rows starteten am Window-Top, überlappten den Titel). Iter-3.5 ist die Spec-definierte Eskalation: Decompile-Spike auf UIScrollWindow + parallel Deep-Reference-Analyse auf ItemBrowser, daraus saubere Fix-Strategie ableiten.

## Goals

- Mouse-wheel scroll funktioniert innerhalb des Window-Viewports ohne Layout-Side-Effects
- `pugText.Clear()`-Fix aus Iter-3 bleibt wirksam (no regression)
- Iter-3.5 wird in einer Iter abgeschlossen — entweder als **SUCCESS** (Spike + Fix + Test + Commit + Merge) ODER als **SPIKE-ONLY** (Spike + Spec-Commit + Merge falls Spike "no-fix-possible" ergibt, dann Pivot zu Iter-3.5b mit eigenem Brainstorm)

## Non-Goals

- Filter-Bar, Search-Input (Iter-4+)
- Refactor von ItemRow/Window-Architektur
- Items-discovery-toggle-Click (Iter-4+)
- DisplayName-Fallback-Strategy (Iter-3.6, separat)

## Decisions Made During Brainstorming

| Question | Decision |
|---|---|
| Decompile-Spike-Tiefe | **A — Focused UIScrollWindow** (~100-200 LoC) plus parallele IB-Deep-Analyse pro [[reference-analysis-mandatory-when-provided]] Memory |
| Iter-Scope | **A — Spike + Fix kombiniert** in einer Iter |
| Workflow | **A — Linear** (Spike → Spec-Update → Fix → Test) |

## Workflow — 4 Phasen

1. **Spike** (Phase 1): Decompile UIScrollWindow (focused) + IB Deep-Analyse (EntriesList, EntriesListRenderer, BasicEntriesListRenderer, IB-Window-Prefab-YAML)
2. **Spec-Update** (Phase 2): Spike-Erkenntnisse als Was/Wie/Warum in `docs/research/spike-5-uiscrollwindow-decompile.md` dokumentieren; **konkrete Fix-Hypothesen** ranked am Ende. User-Decision-Gate via AskUserQuestion welche Hypothese implementiert wird.
3. **Fix-Implementation** (Phase 3): Code-Änderung (und/oder Prefab-Edit) basierend auf approved Hypothese; Build via `utils/build.sh`; Install verifizieren mit grep-on-install (per [[subagent-build-verify-install]] Memory)
4. **Test** (Phase 4): 5 Test-Phasen (siehe Testing-Sektion)

## Spike-Method

### Part A — UIScrollWindow Decompile

| Aspekt | Konkret |
|---|---|
| Target-DLL | `Pug.UnityExtensions.dll` (vermutlich; pre-flight verifizieren via `find` im SDK Library/PackageCache/ + CK-Game-Bottle DLL-Folder) |
| Decompile-Tool | **ILSpyCmd** (CLI, brew-installable). Fallback: dotPeek/dnSpyEx |
| Output-Format | Raw `UIScrollWindow.cs` Export (full class), dann focused grep-extract |
| Focused Methods | `SetScrollValue`, `UpdateScrollHeight`, `_scrollable` getter/setter, `Awake`/`OnEnable`/`Update`, plus direkte Helper-Methoden |
| Pre-Flight | `which ilspycmd \|\| brew install ilspycmd` als Plan-Step |

### Part B — IB Deep-Analyse

| File | Was analysieren | Output |
|---|---|---|
| `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/EntriesList.cs` | `SetEntries()` Full Call-Sequenz (Pre-Calls + 3-Call-Sequence + Post-Calls), `OnEnable`/`Awake`, IScrollable-Wiring | Was/Wie/Warum pro API-Call |
| `EntriesListRenderer.cs` | Renderer-Pattern Spawn/Cleanup Lifecycle, Pool-Mechanik | Was/Wie/Warum |
| `BasicEntriesListRenderer.cs` | ClearList-Pattern (teilweise schon analysiert in Iter-3) | Was/Wie/Warum |
| IB Window-Prefab YAML | Layout-Components, Parent-Hierarchy, scroll-relevante Components die wir vermissen | Component-Liste mit Mapping zu unseren Prefabs |

### Spike-Output-Spec (`docs/research/spike-5-uiscrollwindow-decompile.md`)

Pflicht-Sektionen:
1. UIScrollWindow Decompile-Findings (Was die Methoden intern tun)
2. IB Deep-Analyse (Was/Wie/Warum pro API-Call)
3. Cross-Reference: welche IB-Patterns matchen UIScrollWindow-Internal-Expectations
4. **Konkrete Fix-Hypothese(n)** abgeleitet aus 1+2+3, ranked nach Wahrscheinlichkeit + Risiko
5. **Reference Coverage**: explizit listen was analysiert vs. ausgelassen wurde (per `[[reference-analysis-mandatory-when-provided]]` Memory)

## Fix-Design

### Decision-Gate nach Spike (vor Implementation)

1. Spike-Spec listet 1-N **konkrete Fix-Hypothesen** ranked
2. Ich präsentiere die ranked Liste via AskUserQuestion → User wählt
3. Erst danach Implementation

### Fix-Format-Optionen die der Spike als Output haben kann

| Format | Beispiel | Implementation-Effort |
|---|---|---|
| Code-only-Fix | Zusätzlicher API-Call vor `SetScrollValue` (z.B. `UpdateLayout()`) | Klein (~1-5 LoC) |
| Prefab-Edit | Layout-Component am Window-Root oder Content fehlt | Mittel (Editor-side + .prefab YAML manual edit) |
| Code + Prefab | Beides | Größer |
| **No-fix possible** (worst case) | UIScrollWindow strukturell inkompatibel mit unserem Layout | Iter-3.5 abbrechen, pivot zu **Iter-3.5b** (DIY-scroll-Brainstorm). Iter-3.5-Spike-Output bleibt valid + nützlich |

### Acceptable-Fix-Kriterien

- `pugText.Clear()` aus Iter-3 darf NICHT regressen (Phase 3 Multi-Open-Test bleibt PASS)
- Layout (Liste startet unter Titel) darf NICHT broken werden (Phase 5 0-Tolerance)
- Scroll-funktioniert: mouse-wheel scrollt Content vertikal innerhalb des Viewports
- Side-Effects auf andere UI-Mods akzeptabel nur wenn dokumentiert + bewusst

## Testing — 5 Phasen

| # | Phase | Pass-Kriterium | Failure-Stop |
|---|---|---|---|
| 1 | Sandbox Compile | `Successfully compiled ItemChecklist safetyCheck=True`, kein CompileFailed Cascade | 3-Strike → re-brainstorm |
| 2 | Iter-2+3 Regression (First-Open) | Window + Wood-Theme + Title + Rows + Cursor + WASD-block intakt auf erstem F1 | 1-Strike → STOP, Fix revertieren |
| 3 | Multi-Open Text-Regression | `pugText.Clear()` Fix noch wirksam (Iter-3-Wins erhalten): F1→Escape→F1 ×3 + Disconnect-zu-Hauptmenü; alle Texte bleiben | 1-Strike → STOP |
| 4 | **Scroll-Fix Verification** (Iter-3.5-spezifisch) | Mouse-wheel scrollt Content vertikal, Bounds respected, Clipping OK | 1-Strike → Iter-3.5 fail-mode per Risk-Catalog |
| 5 | **Layout-Side-Effects-Check** (Iter-3.5-spezifisch) | Liste startet unter Titel (NICHT am Window-Top), Titel-Text wird NICHT überlagert | **0-Tolerance** → Fix MUSS revertiert werden (das war der Iter-3-SetScrollValue-Bug) |

**Test-Tool-Setup pro Phase:**
- Player.log + Player-prev.log truncate vor jedem Phase-Cycle
- Grep nach phase-spezifischen markers nach jedem Test
- Visual-Verification durch User (Screenshot wenn möglich)

**Sequenz:** Phasen MÜSSEN in Reihenfolge laufen — Phase 1 muss pass bevor Phase 2 startet, etc. Phase 5 (Layout) ist kritischer als Phase 4 (Scroll): Layout-Bug heißt Iter-3-Failure-Mode reproduziert → Fix komplett zurückrollen.

**Phase 2-vs-3 Lesson aus Iter-3:** Wenn Phase 2 problematic ist (z.B. discovered Items fehlen), kann das auch durch alphabetische Sortierung erklärt sein (= Iter-3.6 Issue, nicht Iter-3.5 Issue). Pass-Kriterium hier nur: Window + Title + Rows generell rendern, NICHT speziell discovered-Items-content.

## Risk + Failure-Modes

| # | Risk | Mitigation / Stop-Pattern |
|---|---|---|
| 1 | Spike findet keine klare Lösung | "no-fix-possible" Output → Pivot zu **Iter-3.5b** (DIY-scroll oder Layout-Restructure-Brainstorm). Spike-Output bleibt valid |
| 2 | Fix wirkt, bricht aber Phase 5 (Layout) | 0-Tolerance. Fix MUSS sofort revertiert. Das war exakt der Iter-3-SetScrollValue-Failure-Mode |
| 3 | Fix bricht `pugText.Clear()`-Pool-Pattern (Phase 3 regression) | 1-Strike fail → Fix revert. Iter-3-Win darf nicht regressen |
| 4 | ILSpyCmd nicht installiert / setup-friction | Pre-flight: `which ilspycmd \|\| brew install ilspycmd`. Falls Issues: dotPeek/dnSpyEx als documented Fallback |
| 5 | Prefab-Edit nötig → Editor-Roundtrip + Asset-Cache-Issues | "File → Save Project" im Editor + `git diff` verify + Subagent-build-verify-install (per [[subagent-build-verify-install]]). Plus Pre-Flight `.envrc` in Worktree kopieren (per [[worktree-remove-preflight-check]]) |
| 6 | Spike-Output zu vage (mehrere Hypothesen ohne klare Empfehlung) | Decision-Gate (siehe Fix-Design) → User entscheidet welche Hypothese implementiert wird |
| 7 | Worktree-Cleanup vergessen / unsafe | Per [[worktree-remove-preflight-check]] Memory: vor `git worktree remove .worktrees/iter-3-5` pre-flight check + bei untracked-Files fragen was passieren soll |

## Worktree-Setup (Pre-Flight)

```
1. Branch-Name: iter-3-5 (dot-vermeiden für tooling-compat)
2. Worktree-Path: REPO_ROOT/.worktrees/iter-3-5/
3. cp .envrc → .worktrees/iter-3-5/.envrc (per Iter-3-Lesson)
4. Verify: bridge-sprites in main-Repo Art/Bridge/ vorhanden (persistent seit 25.05.); nicht ins worktree kopieren weil gitignored
5. Plus: which ilspycmd (verify Decompile-Tool, install via brew falls fehlt)
```

## Lessons-driven Defaults

- WIP-Commit-Vorschläge nach jedem Spike-Sub-Phase (Decompile, IB-Analyse, Spec) per [[frequent-wip-commits-for-bisect]]
- Subagent-Build-Reports mit grep gegenchecken per [[subagent-build-verify-install]]
- Worktree pre-flight check vor remove per [[worktree-remove-preflight-check]]
- Reference-Analyse vollständig (Was/Wie/Warum) per [[reference-analysis-mandatory-when-provided]]

## References

- Iter-3 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md`
- Iter-3 Plan: `docs/superpowers/plans/2026-05-26-itemchecklist-ui-pivot-iter3.md`
- Iter-2 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
- Iter-1 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter1-design.md`
- Spike-4 (UI Architecture): `docs/research/spike-4-ui-architecture.md`
- IB Source (Reference): `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/`
- MapMarkersPlus Source (cross-mod simpler reference): `/tmp/ck-ui-research/MapMarkersPlus/Scripts/Common/UserInterface/MarkerList.cs`
- Memory: `[[reference-analysis-mandatory-when-provided]]`, `[[deep-spike-unfamiliar-internals]]`, `[[subagent-build-verify-install]]`, `[[worktree-remove-preflight-check]]`, `[[frequent-wip-commits-for-bisect]]`, `[[item-checklist-ui-pivot-state]]`, `[[corekeeper-ui-pattern]]`
