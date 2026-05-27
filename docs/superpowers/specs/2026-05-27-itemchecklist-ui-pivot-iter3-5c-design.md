# ItemChecklist UI Pivot — Iteration 3.5c Design (Clipping via Prefab-Edits, IB-1:1)

**Date:** 2026-05-27
**Status:** Design approved (3 blocks). Pending: spec self-review → user review → writing-plans.
**Branch:** `iter-3-5c` (Worktree `REPO_ROOT/.worktrees/iter-3-5c/` aus `main` @ `41d0a2b`)
**Prerequisite reading:**
- Iter-3.5b ABORT-Spec (Findings + Lessons): `docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5b-design.md`
- Iter-3.5b Plan-Findings-Addendum: `docs/superpowers/plans/2026-05-27-itemchecklist-ui-pivot-iter3-5b.md`
- Iter-3.5 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-5-design.md`
- Iter-3 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md`
- Spike-5 (UIScrollWindow Decompile + IB Deep-Analyse + Addendum 2026-05-27): `docs/research/spike-5-uiscrollwindow-decompile.md`

**Environment-Kontext:**
- CK Game-Version: `1.2.1.3-4986`
- CoreLib Runtime: `4.0.3` (mod.io-display "4.0.4")
- CoreLib SDK Build-Time: `4.0.4`
- Iter-3.5 Mod-Stand: `ResetScroll()` + Prefab-Field-Wiring (commit `968eef7` auf main)
- Iter-3.5b Status: ABORTED (Pure-Runtime widerlegt; Doku committed; Worktree removed)
- HEAD vor Iter-3.5c-Start: `41d0a2b` (Layer-Name "UI" → "GUI" Korrektur)

## Context

Iter-3.5b's Pure-Runtime SpriteMask-Strategie wurde durch Pre-Flight widerlegt: ItemRow-Renderer leben in Layer `Default` (Orders 15-20), PugText-Sentinels in Layer `GUI` (Order 9999), und der von IB referenzierte "UI"-Layer existiert gar nicht — es ist `GUI` (uniqueID `1241602095`, verifiziert via `CoreKeeperModSDK/ProjectSettings/TagManager.asset`).

Iter-3.5c pivotiert zu **IB-1:1 mit Prefab-Edits**. Die Sortier-Konfiguration aller Row-Renderer wird via YAML-direct-edit auf IB's bewährte Werte umgestellt (`m_SortingLayerID: 1241602095`, Orders im `40..55`-Range, `m_MaskInteraction: 1`). Die ContentsMask wird im Unity Editor als statisches GameObject im Window-Prefab angelegt. Sprite-Quelle ist ein neues `Art/Bridge/mask_sprite.png` (1×1 weiß), das die bisherige kaputte `white_pixel.png` Text-Stub ersetzt.

## Goals

- **Clipping**: Rows ausserhalb des Window-Viewports unsichtbar (SpriteRenderer + PugText)
- **Pattern-Treue**: 1:1-Port von IB's `ContentsMask` + `EntriesDivider`-Sorting-Konvention
- **Zero-Code**: Iter-3.5c ist eine reine Prefab-+-Asset-Iteration; kein `.cs`-Code wird hinzugefügt oder geändert
- **Zero-Regression**: Iter-2/3/3.5-Wins (Pool-Leak-Fix, Scroll, ResetScroll) bleiben intakt

## Non-Goals

- Code-Änderungen an `ItemChecklistWindow.cs`, `ItemRow.cs`, `ItemChecklistContent.cs`, `ItemChecklistMod.cs`
- DisplayName-Fallback (Iter-3.6), F1-Toggle UX (Iter-4), 4.0.4-Future-Compat (Iter-3.7)
- Layout-/Visual-Redesign (Wood-Theme bleibt, Window-Position bleibt)

## Decisions Made During Brainstorming

| Question | Decision | Source |
|---|---|---|
| Sprite-Quelle | **Neues `Art/Bridge/mask_sprite.png` (1×1 weiß) + `white_pixel.png` entfernen** (war Text-Stub, unreferenziert) | Frage 1 + Codebase-Inspection |
| Workflow Prefab-Edits | **Hybrid: YAML-direct-edit für 5 bestehende Renderer/PugText-Felder + Unity-Editor nur für neues ContentsMask-GameObject im Window-Prefab** | Frage 2 |
| Mask-Layer | **`GUI`** (uniqueID `1241602095`) | Frage 3 + TagManager-Verifikation |
| Sorting-Orders | **Background=45, Icon=48, Label=49, Placeholder=49, Checkmark=50; Mask-Range 40..55** | Frage 4 + IB EntriesUnavailableHeader+EntriesDivider Verifikation |
| Code-Bedarf | **Zero-Code** — alle Sortier-Werte prefab-set, Sprite-Reference im Prefab, MaskInteraction im Prefab | Frage 5 |

## Architecture — Hierarchie nach Iter-3.5c

```
ItemChecklistWindow (root)
└── RowsContainer (UIScrollWindow)
    ├── ContentsMask         ← NEU im Prefab, Kind von RowsContainer, Geschwister von Content
    │   └── (Component: SpriteMask)
    │         m_Sprite                   = Reference auf Art/Bridge/mask_sprite.png
    │         m_IsCustomRangeActive      = 1
    │         m_FrontSortingLayerID      = 1241602095 (= "GUI")
    │         m_FrontSortingOrder        = 55
    │         m_BackSortingLayerID       = 1241602095
    │         m_BackSortingOrder         = 40
    │         m_MaskAlphaCutoff          = 0.2
    │         localPosition              = (windowLocalCenter.x, windowLocalCenter.y, 0)
    │         localScale                 = (windowWidth+0.5, windowHeight+0.5, 1)
    └── Content (scrollingContent, ItemChecklistContent : IScrollable)
        └── ItemRow_0…N
              (Background: Layer GUI Order 45, MaskInteraction 1)
              (Icon:       Layer GUI Order 48, MaskInteraction 1)
              (Label PugText.style:       sortingLayer 1241602095, orderInLayer 49)
              (Placeholder PugText.style: sortingLayer 1241602095, orderInLayer 49)
              (Checkmark:  Layer GUI Order 50, MaskInteraction 1)
```

**Render-Reihenfolge mit Iter-3.5c-Edits (top-rendered last):**
1. World/Player (Layer Default, low Order)
2. Window.Background (Layer Default Order 10) — Wood-Theme, **bleibt absichtlich auf Layer Default**
3. Mask-Region-Cutoff: alles in Layer GUI Order `40..55` wird in Mask-Geometrie geclippt
4. Row.Background (Layer GUI Order 45)
5. Row.Icon (Layer GUI Order 48)
6. Row.Label/Placeholder PugText (Layer GUI Order 49)
7. Row.Checkmark (Layer GUI Order 50)
8. Window.Title (Layer GUI Order 9999) — Sentinel-resolved, outside `40..55`-Range, mask-immune

**Wichtige Design-Eigenschaften:**

- **Window.Title vs Mask-Range:** Title sitzt im selben Layer (GUI) wie die Mask, aber Order 9999 ist weit außerhalb der Custom-Range. Mask ignoriert Renderer outside Range. Title wird nicht geclippt.
- **Wood-Theme-Background bleibt Layer Default:** Window.Background wird absichtlich NICHT auf GUI verlagert. Es rendert unter allem Layer-GUI-Content (Layer-Reihenfolge GUI > Default). Risk-Reduction: Mask kann es nicht versehentlich treffen, weil sie in einem anderen Layer wirkt.
- **mask_sprite.png Bundle-Inclusion-Chain:** ContentsMask.SpriteMask.m_Sprite → mask_sprite.png.meta GUID → asset bundle. Einzige Reference, die das Asset ins Bundle bringt.

## Components — File-Inventur

| File | Action | Was passiert |
|---|---|---|
| `unity/ItemChecklist/Art/Bridge/mask_sprite.png` | **NEU** | 1×1 weißes PNG, semantischer Name für Mask-Use-Case |
| `unity/ItemChecklist/Art/Bridge/mask_sprite.png.meta` | **NEU** | `textureType: 8`, `spriteMode: 1`, eigene GUID |
| `unity/ItemChecklist/Art/Bridge/white_pixel.png` | **DELETE** | Text-Stub, unreferenziert |
| `unity/ItemChecklist/Art/Bridge/white_pixel.png.meta` | **DELETE** | Orphan nach PNG-Delete |
| `unity/ItemChecklist/Prefabs/ItemRow.prefab` | **YAML-direct-edit** (5 Renderer-Blöcke) | Layer/Order/MaskInteraction für Background, Icon, Label, Placeholder, Checkmark |
| `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` | **Editor-Roundtrip** | Neu: ContentsMask GameObject + SpriteMask Component |
| `unity/ItemChecklist/ui/*.cs`, `unity/ItemChecklist/ItemChecklist.asmdef`, alle Code-Files | **UNVERÄNDERT** | Zero-Code-Iteration |

### ItemRow.prefab — 5 YAML-Edit-Blöcke

**Background SpriteRenderer** (current Z143-155):
```yaml
m_SortingLayerID: 1241602095   # was 0 (Default)
m_SortingLayer:   5             # was 0 (matches IB)
m_SortingOrder:   45            # was 15
m_MaskInteraction: 1            # was 0 (None → VisibleInsideMask)
```

**Icon SpriteRenderer** (current Z230-242):
```yaml
m_SortingLayerID: 1241602095
m_SortingLayer:   5
m_SortingOrder:   48
m_MaskInteraction: 1
```

**Label PugText.style** (current Z327-328):
```yaml
sortingLayer: 1241602095   # was -2147483648 (sentinel; now explicit)
orderInLayer: 49           # was 9999
```

**Placeholder PugText.style** (current Z416-417):
```yaml
sortingLayer: 1241602095
orderInLayer: 49
```

**Checkmark SpriteRenderer** (current Z495-507):
```yaml
m_SortingLayerID: 1241602095
m_SortingLayer:   5
m_SortingOrder:   50
m_MaskInteraction: 1
```

### ItemChecklistWindow.prefab — Editor-Roundtrip

Im Unity Editor:

1. RowsContainer-GameObject im Hierarchy selektieren
2. Right-click → Create Empty Child → Rename "ContentsMask"
3. Im Inspector: Tag-Layer auf `UI` (= 5) setzen für Konsistenz mit anderen UI-Elementen
4. Add Component → `SpriteMask`
5. SpriteMask-Settings:
   - `Sprite`: Drag `Art/Bridge/mask_sprite.png` ins Feld
   - `Mask Source`: Sprite (default)
   - `Custom Range`: ON
   - `Front Layer`: `GUI`, `Front Order`: 55
   - `Back Layer`: `GUI`, `Back Order`: 40
   - `Alpha Cutoff`: 0.2
6. Transform:
   - `Position`: matched zu `UIScrollWindow.windowLocalCenter` (aus Inspector ablesen)
   - `Scale`: matched zu `(windowWidth + 0.5, windowHeight + 0.5, 1)` mit ε-buffer
7. File → Save Project
8. `git diff` über Window-Prefab YAML: verifiziere nur erwartete Sections

### Asset-Creation (mask_sprite.png)

```bash
cd unity/ItemChecklist/Art/Bridge/
convert -size 1x1 xc:white mask_sprite.png        # ImageMagick: 1×1 weiß
file mask_sprite.png                              # Sanity: "PNG image data, 1 x 1"
python3 -c "from PIL import Image; im=Image.open('mask_sprite.png'); print(im.getpixel((0,0)))"  # Sanity: (255,255,255) o.ä.
```

`.meta` via Template aus `white_pixel.png.meta` mit neuer GUID (z.B. `uuidgen | tr -d - | tr A-Z a-z`).

## Testing — 6 Phasen

| # | Phase | Pass-Kriterium | Failure-Stop |
|---|---|---|---|
| **1** | **Asset-Bundle Inclusion** | Nach Build: SpriteMask m_Sprite-GUID im Window-Prefab-YAML im Bundle gefunden; Sprite-Asset-Hash im `MOD_INSTALL_PATH/.../Bundles/` enthalten | 1-Strike → STOP, Bundle-Konfiguration prüfen |
| **2** | **Iter-2/3/3.5 Regression** (First-Open) | F1 öffnet Window; Wood-Theme + Title + Rows + Cursor + WASD-Block + ResetScroll intakt | 1-Strike → STOP, Revert |
| **3** | **Multi-Open Pool** | F1×3 + Disconnect; alle Texte bleiben sichtbar | 1-Strike → STOP |
| **4** | **Scroll Regression** | Mouse-wheel scrollt; ResetScroll bei jedem F1 | 1-Strike → STOP |
| **5** | **Clipping Visual-Verification** (Iter-3.5c-Kern) | Rows die durch Scroll aus dem Wood-Theme-Rechteck herauslaufen, sind UNSICHTBAR — Icon, Label, Checkmark, Background alle clipped | **0-Tolerance** → revertieren |
| **6** | **Layout-Side-Effects** | Title NICHT geclippt; Wood-Theme-Background NICHT geclippt; Vanilla CK-UI (Inventory, Hotbar) unaffected | **0-Tolerance** → revertieren (das wäre "Window kaputt") |

**Sequenz-Regel:** Phasen 1-6 in Reihenfolge. Phase 5/6 sind kritischer als 2-4 (Clipping-Bug = User-sichtbarer Layout-Schaden).

**Test-Tool-Setup pro Phase:**
- Player.log + Player-prev.log truncate vor jedem Cycle
- Screenshot empfohlen für Phase 5+6 (mid-scroll states)
- `git diff unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` nach Editor-Save: nur erwartete Sections

## Risks + Failure-Modes

| # | Risk | Mitigation |
|---|---|---|
| **1** | `mask_sprite.png` landet NICHT im AssetBundle (Reference broken / Unity reimportet nicht) | Pre-Build-Check: `grep "<GUID>"` im Window-Prefab YAML muss exakt 1 match haben (im SpriteMask m_Sprite-Field). Post-Build: `grep` auf Sprite-Asset-Hash im Bundle. Falls miss: Editor-Roundtrip wiederholen, Sprite-Reference neu setzen |
| **2** | TagManager `uniqueID: 1241602095` für GUI hat sich zwischen CK-Builds geändert | Ad-hoc Pre-Build-Check: `grep 1241602095 CoreKeeperModSDK/ProjectSettings/TagManager.asset` muss noch matchen. Falls nicht: neuen Wert resolven, alle 5 YAML-Edits + Editor-Mask-Setting aktualisieren |
| 3 | Editor-Roundtrip ändert mehr als nur ContentsMask (Asset-Re-Import-Bug) | `git diff --stat` nach Save Project: erwartet nur Window-Prefab + ggf. mask_sprite.png.meta (durch Unity auto-update). Andere Files modifiziert → vor commit revertieren + recheck Editor-Workflow |
| 4 | Sorting-Order-Konflikte: Wood-Background versehentlich auch geclippt | 0-Tolerance Phase 6 + Architecture-Audit: Window.Background bleibt absichtlich auf Layer Default Order 10 (outside mask-range UND falscher Layer für Mask) |
| 5 | mask_sprite.png 1×1 ist zu klein für Mask-Geometrie | SpriteMask skaliert das Sprite via Transform.localScale auf Window-Bounds. 1×1 → ε-buffer-erweiterte Window-Bounds. Stretch ist OK weil Sprite uniform weiß |
| 6 | YAML-direct-edit bricht Prefab-YAML-Integrität (z.B. fileID-Konflikte) | 5 Edit-Operationen sind alle in-place Field-Replacements (kein strukturelles Ändern). Risiko niedrig. Falls Prefab beim Editor-Open rejected: revert last Edit + manuelles Inspect |
| 7 | Worktree-Setup-Hygiene | [[worktree-remove-preflight-check]] + `.envrc` copy + bridge-sprites bleiben gitignored im main-Repo |
| 8 | `mask_sprite.png` muss im main-Repo Art/Bridge/ landen (nicht nur Worktree), sonst kollabiert mit nächstem Worktree-Cycle | Asset-Creation **vor** Worktree-Setup im main-Repo Art/Bridge/ ausführen; Worktree-Setup nutzt utils/link.sh-symlinks die das Asset transitiv mitbringen |
| 9 | Iter-Budget-Overrun | Budget: 0.5 d Asset-Erstellung + 5 YAML-Edits, 0.5 d Editor-Roundtrip + Save, 0.5 d Tests Phasen 1-6. Bei Fail in Phase 5/6 → Iter-3.5d (alternative Strategy: Camera-Clip-Plane, uGUI-Pivot, oder Doppel-Mask) |

## Worktree-Setup (Pre-Flight)

```
1. Asset-Pre-Step: mask_sprite.png + .meta im main-Repo Art/Bridge/ erstellen
   (asset bleibt gitignored, persistent in main; symlink via link.sh in worktree)
2. Branch: iter-3-5c
3. Worktree: REPO_ROOT/.worktrees/iter-3-5c/  (aus main @ 41d0a2b)
4. cp .envrc → .worktrees/iter-3-5c/.envrc
5. Verify symlink von .worktrees/iter-3-5c/unity/ItemChecklist/Art/Bridge/mask_sprite.png → main-Repo  
   (via utils/link.sh; sonst per cp)
```

## Lessons-driven Defaults

- WIP-Commit-Vorschläge nach jedem Plan-Step (Asset-Create, jeder YAML-Edit-Block, Editor-Roundtrip, Phase-Tests) per [[frequent-wip-commits-for-bisect]]
- Subagent-Build-Reports mit grep gegenchecken per [[subagent-build-verify-install]]
- Worktree pre-flight check vor remove per [[worktree-remove-preflight-check]]
- **TagManager-/Asset-IDs gegen ProjectSettings prüfen**, nicht aus Mod-Refs inferieren per [[avoid-inverse-inference-fallacy]] — Iter-3.5b-Lesson, jetzt etabliert
- Audit-Grep für CK-UI-Prefab-Sorting MUSS PugText-Schlüssel mit erfassen: `m_SortingLayer|m_SortingOrder|m_Name:|sortingLayer:|orderInLayer:`

## References

- Iter-3.5b ABORT-Spec: `docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5b-design.md`
- Iter-3.5b Plan-Findings-Addendum: `docs/superpowers/plans/2026-05-27-itemchecklist-ui-pivot-iter3-5b.md`
- Iter-3.5 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-5-design.md`
- Iter-3 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md`
- Iter-2 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
- Iter-1 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter1-design.md`
- Spike-5 (UIScrollWindow Decompile + IB Deep-Analyse + Addendum 2026-05-27): `docs/research/spike-5-uiscrollwindow-decompile.md`
- Spike-4 (UI Architecture): `docs/research/spike-4-ui-architecture.md`
- IB Source (Reference): `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/`
  - `Prefabs/Browser/ItemBrowserUI.prefab` (Z3037-3123: ContentsMask + SpriteMask)
  - `Prefabs/Browser/EntriesDivider.prefab` (Z77-79: m_SortingLayerID 1241602095, Order 49)
  - `Prefabs/Browser/EntriesUnavailableHeader.prefab` (Z79-81: Background Order 45; Z244-245: Label PugText.style Order 48)
- CK Project-Settings: `CoreKeeperModSDK/ProjectSettings/TagManager.asset` (SortingLayer-Liste mit uniqueIDs)
- Pre-Flight-Daten: `/tmp/iter-3-5b-spike/` (UIScrollWindow.decompiled.cs, PugText.decompiled.cs, preflight-notes.txt)
- Memory: `[[item-checklist-ui-pivot-state]]`, `[[corekeeper-ui-pattern]]`, `[[pugstorm-modbuilder-sprite-meta]]`, `[[reference-analysis-mandatory-when-provided]]`, `[[avoid-inverse-inference-fallacy]]`, `[[worktree-remove-preflight-check]]`, `[[subagent-build-verify-install]]`, `[[frequent-wip-commits-for-bisect]]`, `[[deep-spike-unfamiliar-internals]]`
