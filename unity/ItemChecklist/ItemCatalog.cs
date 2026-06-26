using System;
using System.Collections.Generic;
using System.Linq;
using PugMod;
using Unity.Mathematics;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Single immutable list of every item the game knows about, sorted
    /// alphabetically by display name. Built once per world load via
    /// <see cref="Bake"/>. Subsequent lookups are O(1) by id or by index.
    ///
    /// API surface verified against moorowl/ItemBrowser
    /// (ItemBrowserPackage/Scripts/Utilities/{ObjectUtility,ModUtility}.cs):
    ///   * <c>PugDatabase.objectsByType.Keys</c> enumerates every
    ///     <c>ObjectDataCD</c> the loaded database knows about.
    ///   * <c>PugDatabase.GetObjectInfo(objectID, variation)</c> returns the
    ///     <c>ObjectInfo</c> carrying <c>objectType</c>, <c>icon</c>,
    ///     <c>smallIcon</c>.
    ///   * <c>PlayerController.GetObjectName(ContainedObjectsBuffer, bool)</c>
    ///     is the official path to the localized display name; the bool is
    ///     <c>localize</c> (true = localized name, false = raw I2.Loc term).
    ///     Returns <c>TextAndFormatFields</c> with <c>.text</c> +
    ///     <c>.dontLocalize</c>. When <c>.dontLocalize</c> is true on the
    ///     localized call, ItemBrowser swaps in the unlocalized text instead
    ///     (player-names, ad-hoc items).
    ///   * <c>API.ModLoader.LoadedMods</c> exposes the mod-id → display-name
    ///     mapping for mod-origin tagging.
    /// </summary>
    public sealed class ItemCatalog
    {
        // Per-objectID one-time warning cache. Static so it survives re-bakes
        // (loc-change, world-reload) — the symptom is build-level, not world-level.
        // Not reset by Bake().
        private static readonly System.Collections.Generic.HashSet<ObjectID> warnedIds = new();

        // Re-entrance guard. Bake() must be safe against nested calls when
        // a Loc-change event fires during an in-flight bake. Single-threaded
        // assumption (Unity main thread for Harmony + I2.Loc events).
        private bool baking;

        public readonly struct Entry
        {
            public readonly int ObjectId;
            public readonly int Variation;
            public readonly string DisplayName;
            public readonly Sprite Icon;
            public readonly string ModOrigin;   // empty string = vanilla
            public readonly Rarity Rarity;      // CK ObjectInfo.rarity (Poor..Legendary)
            public readonly ObjectType ObjectType;  // CK ObjectInfo.objectType (Category sort key)
            public readonly int Level;          // CK ObjectInfo.level
            public readonly int SellValue;      // 0 = unsellable/legendary; >0 = computed sell value
            public readonly bool IsCraftable;   // ObjectInfo.requiredObjectsToCraft non-empty
            public readonly bool IsPetSkin;     // Iter-16.1: pet-skin row — Variation carries skinIndex, collected via PetCollection
            public readonly bool IsCattle;      // Iter-16.3: cattle species row (drives the Cattle category + collection routing)
            public readonly bool IsColourVariant; // Iter-17: a colour-variant slot (cattle colour OR paintable variant) — name is
                                                  // species-gated (shown once ANY form of the item is discovered), unlike per-variation rows

            public Entry(int objectId, int variation, string displayName, Sprite icon,
                string modOrigin, Rarity rarity, ObjectType objectType,
                int level, int sellValue, bool isCraftable, bool isPetSkin = false,
                bool isCattle = false, bool isColourVariant = false)
            {
                ObjectId = objectId;
                Variation = variation;
                DisplayName = displayName;
                Icon = icon;
                ModOrigin = modOrigin ?? string.Empty;
                Rarity = rarity;
                ObjectType = objectType;
                Level = level;
                SellValue = sellValue;
                IsCraftable = isCraftable;
                IsPetSkin = isPetSkin;
                IsCattle = isCattle;
                IsColourVariant = isColourVariant;
            }
        }

        private Entry[] entries = Array.Empty<Entry>();
        private readonly Dictionary<long, int> keyToIndex = new Dictionary<long, int>();

        public int Count => entries.Length;
        public Entry GetByIndex(int index) => entries[index];
        public bool TryGetIndex(int objectId, int variation, out int index) =>
            keyToIndex.TryGetValue(DiscoveredState.PackKey(objectId, variation), out index);

        public bool TryGetEntry(int objectId, int variation, out Entry entry)
        {
            if (keyToIndex.TryGetValue(DiscoveredState.PackKey(objectId, variation), out int idx))
            { entry = entries[idx]; return true; }
            entry = default;
            return false;
        }

        // Iter-16.1: pet-skin rows route possession/discovery through PetCollection
        // instead of CK's skin-blind DiscoveredState (OwnedCount uses this).
        public bool IsPetSkinEntry(int objectId, int variation)
            => TryGetEntry(objectId, variation, out var e) && e.IsPetSkin;

        // Iter-17: a cattle colour-slot row (drives per-colour possession routing).
        public bool IsCattleEntry(int objectId, int variation)
            => TryGetEntry(objectId, variation, out var e) && e.IsCattle;

        /// <summary>
        /// Build the catalog in three loops over <c>PugDatabase.objectsByType.Keys</c>:
        /// <list type="bullet">
        /// <item><b>Loop 1 — standard items.</b> Keeps the canonical
        /// (variation == 0) entries whose <c>ObjectType</c> is not one of the
        /// categorically-non-item discriminators (NonObtainable, Creature,
        /// Critter, PlayerType), with an Iter-7.1 icon-guard that keeps
        /// <c>NonUsable</c> raw materials but drops the icon-less internal engine
        /// entities filed under that type. Cooked-food (re-emitted by Loop 2) and
        /// pets (re-emitted by Loop 3) are skipped here.</item>
        /// <item><b>Loop 2 — cooked-food α-enumeration.</b> Cartesians the
        /// ingredient pairs to emit cooked-food permutations × 3 tier-variants
        /// (Base/Rare/Epic), keyed by the food variation.</item>
        /// <item><b>Loop 3 — per-skin pet entries (Iter-16.1).</b> One row per
        /// <c>(petObjectID, skinIndex)</c>, with <c>skinIndex</c> stored in the
        /// entry's <c>Variation</c> slot and <c>IsPetSkin</c> set; skin-unique
        /// names bypass the conflict pass.</item>
        /// </list>
        /// Each entry resolves its localized display name via two GetObjectName
        /// passes (localized + unlocalized); Loop 1 detects name conflicts and
        /// appends a disambiguation note. Finally sorts alphabetically and builds
        /// the id → index map. Safe to call multiple times — replaces the previous
        /// catalog atomically from a single-caller perspective.
        /// </summary>
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
                float perfT0 = UnityEngine.Time.realtimeSinceStartup;

                // PugDatabase.objectsByType is null until UpdateEntityMonos runs at
                // least once. Bake() called too early (before the world is ready)
                // hits this — fail soft so the consumer can retry.
                if (PugDatabase.objectsByType == null)
                {
                    Debug.LogWarning("[ItemChecklist] ItemCatalog.Bake called before PugDatabase ready — skipping");
                    return;
                }

                // Iter-16.3: identify the cattle subset of ObjectType.Creature and the
                // structural baby→adult fold (BreedStateCD.babyType), before Loop 1.
                CattleRegistry.Build();

                // Pre-resolve mod-id → display-name once so the per-entry loop
                // is a single Dictionary lookup.
                var modIdToName = new Dictionary<long, string>();
                foreach (var mod in API.ModLoader.LoadedMods)
                {
                    string name = !string.IsNullOrWhiteSpace(mod.Metadata.displayName)
                        ? mod.Metadata.displayName
                        : mod.Metadata.name;
                    modIdToName[mod.ModId] = name ?? string.Empty;
                }

                // First pass: collect localized + unlocalized names per accepted od.
                var localizedNames   = new Dictionary<long, string>();  // key = PackKey(objId, variation)
                var unlocalizedNames = new Dictionary<long, string>();
                var accepted         = new List<ObjectDataCD>();
                var iconCache        = new Dictionary<long, Sprite>();
                var rarityCache      = new Dictionary<long, Rarity>();
                var objectTypeCache  = new Dictionary<long, ObjectType>();
                var levelCache       = new Dictionary<long, int>();
                var sellValueCache   = new Dictionary<long, int>();
                var craftableCache   = new Dictionary<long, bool>();

                foreach (var od in PugDatabase.objectsByType.Keys)
                {
                    // Iter-17 Bucket 1 (curated, sweep-decided): emit DB-authored
                    // variation≠0 rows ONLY for player-paintable objects
                    // (PaintableObjectCD) — genuine cosmetic colour variants (decor) —
                    // dropping state variants (chest open/closed, seed growth, destructible
                    // damage), which carry no PaintableObjectCD. Sweep found ~395 non-0
                    // PlaceablePrefab keys, mixed cosmetic + state-junk; this keeps the
                    // cosmetics. Cooked food / pets / cattle are re-emitted by Loops 2/3/4
                    // and skipped by the existing guards below.
                    if (od.variation != 0)
                    {
                        if (!PugDatabase.HasComponent<PaintableObjectCD>(od)) continue;
                        var vinfo0 = PugDatabase.GetObjectInfo(od.objectID, od.variation);
                        if (vinfo0 == null || (vinfo0.smallIcon == null && vinfo0.icon == null)) continue; // icon-guard
                    }

                    // Iter-3.7: Cooked-Food family-items (IDs in [9500,9599]) are
                    // handled by the α-enumeration loop further down — skip them here
                    // so they don't appear as variation=0 placeholder entries.
                    if (od.objectID.IsCookedFood()) continue;

                    // Iter-16.1: pets are emitted per-skin by the pet loop below — skip the
                    // skinless variation-0 form here (same pattern as cooked food).
                    if (PugDatabase.HasComponent<PetCD>(od)) continue;

                    var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
                    if (info == null) continue;

                    // ObjectType has no single "Item" value — it discriminates the
                    // kind of thing (Helm, MiningPick, Ring, …). Items are
                    // everything that *isn't* a categorically-non-item type. This
                    // exclusion list mirrors ItemBrowser's ObjectUtility.IsNonObtainable
                    // (ObjectUtility.cs:393), which excludes NonObtainable / Creature /
                    // Critter / PlayerType — but **not** NonUsable. (Critter is relaxed
                    // back in just below for the catchable subset — see Iter-16.2.)
                    if (info.objectType == ObjectType.NonObtainable) continue;
                    // Iter-17: cattle (CattleCD) are emitted per-colour by Loop 4
                    // (discover-to-reveal), so skip the whole Creature type here — exactly
                    // as pets (Loop 3) and cooked food (Loop 2) are re-emitted. Non-cattle
                    // Creatures were never wanted (Iter-16.3 only ever admitted cattle from
                    // Creature; babies, being CattleCD/Creature, are skipped here too).
                    if (info.objectType == ObjectType.Creature) continue;
                    if (info.objectType == ObjectType.PlayerType)    continue;

                    // Iter-16.2: catchable critters are the EXCEPTION to the IB mirror
                    // above. CK's static DB holds 25 ObjectType.Critter item-forms, all
                    // with inventory icons and all obtainable → all discovery-tracked
                    // (SetObjectAsDiscovered has no Critter special-case): the 20 bug-net
                    // critters (ObjectIDs 9800–9819) PLUS the 5 Fireflies / Glowbugs
                    // (3500–3504, FireflyCD not CritterCD, but still carriable — verified
                    // in-game: present in player chests). The "~15, 9800–9819 only" figure
                    // a decompile probe produced was wrong on both count and range — count
                    // confirmed = 25 in-game. Keep them all, with the Iter-7.1 icon-guard
                    // dropping any icon-less stub defensively.
                    if (info.objectType == ObjectType.Critter
                        && info.smallIcon == null && info.icon == null) continue;

                    // Iter-7.1 catalog-completeness fix: ObjectType.NonUsable is NOT
                    // "garbage". CK assigns it to raw materials — ores, bars, raw wood,
                    // scrap, plain Wood, etc. The old blanket `NonUsable continue`
                    // silently dropped every ore/bar/raw-wood from the checklist. IB
                    // doesn't exclude NonUsable at all; we can't replicate its full
                    // IsNonObtainable here (it needs ECS/registry APIs the RoslynCSharp
                    // sandbox blocks), so instead we keep NonUsable items and drop only
                    // the handful of internal engine entities CK also files under
                    // NonUsable — territory spawners, TheCore, the DroppedItem entity,
                    // and boss-statue prefab stubs. Those are exactly the NonUsable
                    // entries with no icon (the 117 real materials all carry one; the 9
                    // internal entities have neither an icon nor a localized name) —
                    // verified in-game on game version 1.2.1.4.
                    if (info.objectType == ObjectType.NonUsable
                        && info.smallIcon == null && info.icon == null)
                        continue;

                    // Two-pass name resolution (ItemBrowser ObjectUtility.cs pattern).
                    // localize=true  → I2.Loc resolved display name (e.g. "Large Water Can")
                    // localize=false → raw I2.Loc term path (e.g. "Items/LargeWaterCan")
                    var (locText, locDontLocalize) = ResolveOne(od, localize: true);
                    var (rawText, _)               = ResolveOne(od, localize: false);

                    // ItemBrowser dontLocalize-output-flag fallback (ObjectUtility.cs:107-108):
                    // when the game signals this item can't be localized, swap to the raw term.
                    if (locDontLocalize && !string.IsNullOrEmpty(rawText))
                        locText = rawText;

                    // PascalCaseSplitter fallback when both passes failed to produce a name.
                    if (string.IsNullOrEmpty(locText))
                        locText = PascalCaseSplitter.Split(od.objectID.ToString());
                    if (string.IsNullOrEmpty(rawText))
                        rawText = PascalCaseSplitter.Split(od.objectID.ToString());

                    // Iter-17: a kept paintable colour variant shares the base item's name
                    // ("Calendar" ×3) — suffix it so the slots are distinguishable (else the
                    // conflict pass leaves identical "Calendar (Items/Calendar)" rows).
                    if (od.variation != 0)
                        locText = $"{locText} {Loc.F("ItemChecklist-General/ColorSuffix", od.variation)}";

                    long key = DiscoveredState.PackKey((int)od.objectID, od.variation);
                    localizedNames[key]   = locText;
                    unlocalizedNames[key] = rawText;
                    accepted.Add(od);
                    iconCache[key] = info.smallIcon != null ? info.smallIcon : info.icon;
                    rarityCache[key] = info.rarity;
                    objectTypeCache[key] = info.objectType;
                    levelCache[key]      = PugDatabase.TryGetComponent<LevelCD>(od, out var lvlCd) ? lvlCd.level : 0;
                    sellValueCache[key]  = ComputeSellValue(od, info);
                    craftableCache[key]  = info.requiredObjectsToCraft != null && info.requiredObjectsToCraft.Count > 0;
                }

                // ─── Loop 2: α-enumeration for Cooked-Food permutations ─────────
                // Pre-cache: ingredient → turnsIntoFood (Base-Tier-Family)
                //            family → (rareId, epicId) tier-versions
                // Note: tierMap is populated for ALL 45 Cooked-Food items
                // (incl. Rare/Epic), but only base-tier IDs are ever looked up
                // below — Rare/Epic items' tierMap entries are inert. Cost is
                // ~30 wasted dict insertions out of ~45 — not worth filtering.
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
                        localizedNames, unlocalizedNames, iconCache, rarityCache, objectTypeCache,
                        levelCache, sellValueCache, craftableCache, accepted);
                    if (tierMap.TryGetValue(baseFamily, out var tiers))
                    {
                        if (tiers.rare != ObjectID.None)
                            AddCookedEntry(
                                new ObjectDataCD { objectID = tiers.rare, variation = variation },
                                localizedNames, unlocalizedNames, iconCache, rarityCache, objectTypeCache,
                                levelCache, sellValueCache, craftableCache, accepted);
                        if (tiers.epic != ObjectID.None)
                            AddCookedEntry(
                                new ObjectDataCD { objectID = tiers.epic, variation = variation },
                                localizedNames, unlocalizedNames, iconCache, rarityCache, objectTypeCache,
                                levelCache, sellValueCache, craftableCache, accepted);
                    }
                }

                // ─── Loop 3: per-skin pet entries (Iter-16.1) ──────────────────
                // Pets always live at variation 0 in CK; the skin is separate aux
                // data (PetSkinCD.skinIndex). Emit one row per skin, keyed
                // (objectID, skinIndex) via the entry's Variation slot. Names are
                // unique per skin so they bypass the conflict pass above. Level/Value
                // are em-dashed for pets: LevelCD.level (7/10/16) is a prefab tier
                // field, NOT the pet's trainable per-instance gameplay level (1 in a
                // chest, 8 when equipped — verified in-game); a per-species row cannot
                // show a per-instance level. Value is just the rarity echo. Both → "—".
                var petEntries = new List<Entry>();
                foreach (var od in PugDatabase.objectsByType.Keys)
                {
                    if (od.variation != 0 || !PugDatabase.HasComponent<PetCD>(od)) continue;
                    var petInfo = PugDatabase.GetObjectInfo(od.objectID, 0);
                    if (petInfo == null) continue;

                    var (petLoc, petDont) = ResolveOne(od, localize: true);
                    var (petRaw, _)       = ResolveOne(od, localize: false);
                    if (petDont && !string.IsNullOrEmpty(petRaw)) petLoc = petRaw;
                    if (string.IsNullOrEmpty(petLoc))
                        petLoc = PascalCaseSplitter.Split(od.objectID.ToString());

                    var skinInfo = (Manager.ui != null && Manager.ui.petInfosTable != null)
                        ? Manager.ui.petInfosTable.GetPetSkinInfo(od.objectID) : null;
                    int skinCount = (skinInfo != null && skinInfo.skins != null && skinInfo.skins.Count > 0)
                        ? skinInfo.skins.Count
                        : (PugDatabase.TryGetComponent<PetCD>(od, out var pcd) ? Math.Max(1, pcd.maxSkins) : 1);

                    var petIcon = petInfo.smallIcon != null ? petInfo.smallIcon : petInfo.icon;
                    string petMod = ResolveModOrigin(od, modIdToName);
                    for (int skin = 0; skin < skinCount; skin++)
                    {
                        string skinName = $"{petLoc} {Loc.F("ItemChecklist-General/SkinSuffix", skin + 1)}";
                        petEntries.Add(new Entry(
                            (int)od.objectID, skin, skinName, petIcon, petMod,
                            petInfo.rarity, ObjectType.Pet,
                            level: 0, sellValue: 0, isCraftable: false, isPetSkin: true));
                    }
                }

                // ─── Loop 4: per-colour cattle entries (Iter-17, pet-style fixed slots) ──
                // Each cattle species has a FIXED colour palette (CattleRegistry.ColoursOf,
                // read from the authored PossibleChildVariation[] list — verified {0..4},
                // 5 colours/species, sandbox-safe). Emit one row per colour ALWAYS — exactly
                // like pet skins (Loop 3): the ItemChecklistContent name-gate
                // (IsDiscoveredAnyVariation for cattle) shows the species name on EVERY slot
                // once any colour is discovered, ??? otherwise; the per-colour collected tick
                // routes through IsDiscovered(id, colour). No placeholder, no discover-to-
                // reveal re-bake. Sweep-confirmed (D-icon): GetObjectInfo(id, v) falls back to
                // var0 for every colour (no per-colour icon), so the species icon + a
                // "(Colour v)" suffix disambiguate the slots. Level/Value em-dashed (16.3).
                var cattleEntries = new List<Entry>();
                var cattleDisc = DiscoveredState.Instance;
                foreach (var od in PugDatabase.objectsByType.Keys)
                {
                    if (od.variation != 0 || !PugDatabase.HasComponent<CattleCD>(od)) continue;
                    if (CattleRegistry.IsBaby((int)od.objectID)) continue;
                    var cInfo = PugDatabase.GetObjectInfo(od.objectID, 0);
                    if (cInfo == null) continue;

                    var (cLoc, cDont) = ResolveOne(od, localize: true);
                    var (cRaw, _)     = ResolveOne(od, localize: false);
                    if (cDont && !string.IsNullOrEmpty(cRaw)) cLoc = cRaw;
                    if (string.IsNullOrEmpty(cLoc)) cLoc = PascalCaseSplitter.Split(od.objectID.ToString());

                    var cIcon = cInfo.smallIcon != null ? cInfo.smallIcon : cInfo.icon;
                    string cMod = ResolveModOrigin(od, modIdToName);
                    int cid = (int)od.objectID;

                    // The species' authored colour set; fall back to {0} ∪ discovered if the
                    // PossibleChildVariation list is ever absent (defensive — all cattle have it).
                    var colours = new List<int>(CattleRegistry.ColoursOf(cid));
                    if (colours.Count == 0)
                    {
                        var seen = new HashSet<int> { 0 };
                        colours.Add(0);
                        if (cattleDisc != null)
                            foreach (var dv in cattleDisc.DiscoveredVariationsOf(cid))
                                if (seen.Add(dv)) colours.Add(dv);
                        colours.Sort();
                    }
                    foreach (var v in colours)
                    {
                        string colourName = $"{cLoc} {Loc.F("ItemChecklist-General/ColorSuffix", v)}";
                        cattleEntries.Add(new Entry(
                            cid, v, colourName, cIcon, cMod,
                            cInfo.rarity, ObjectType.Creature,
                            level: 0, sellValue: 0, isCraftable: false, isPetSkin: false,
                            isCattle: true, isColourVariant: true));
                    }
                }

                // Conflict detection: count occurrences of each localized name.
                var nameCount = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var locName in localizedNames.Values)
                    nameCount[locName] = nameCount.TryGetValue(locName, out var c) ? c + 1 : 1;

                // Second pass: build final entries, appending disambiguation note on conflict.
                var list = new List<Entry>(accepted.Count);
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
                    // Iter-17: a paintable item (base var0 + its colour variants) is a colour
                    // family → species-gated name reveal (all slots named once any form known).
                    list.Add(new Entry((int)od.objectID, od.variation, finalName, iconCache[key],
                        modOrigin, rarityCache[key], objectTypeCache[key],
                        levelCache[key], sellValueCache[key], craftableCache[key],
                        isPetSkin: false, isCattle: false,
                        isColourVariant: PugDatabase.HasComponent<PaintableObjectCD>(od)));
                }

                // Iter-16.1: pet-skin entries (unique names, no conflict pass needed).
                list.AddRange(petEntries);
                // Iter-17: cattle colour + placeholder entries (suffixed/unique names).
                list.AddRange(cattleEntries);

                entries = list
                    .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                keyToIndex.Clear();
                for (int i = 0; i < entries.Length; i++)
                    keyToIndex[DiscoveredState.PackKey(entries[i].ObjectId, entries[i].Variation)] = i;

                Debug.Log($"[ItemChecklist] ItemCatalog baked: {entries.Length} items");
                float perfTotalMs = (UnityEngine.Time.realtimeSinceStartup - perfT0) * 1000f;
                UnityEngine.Debug.Log(
                    $"[ItemChecklist] PERF bake-total={perfTotalMs:F0}ms catalog-size={entries.Length}");
            }
            finally
            {
                baking = false;
            }
        }

        /// <summary>
        /// Resolve a name for an object via <see cref="PlayerController.GetObjectName"/>.
        /// Pass <c>localize=true</c> for the I2.Loc-resolved display name; <c>false</c>
        /// for the raw term path (used as conflict-disambiguation note).
        /// Returns the text and the output struct's <c>dontLocalize</c> flag.
        /// Falls back to <c>(null, false)</c> when GetObjectName throws; the caller
        /// applies <see cref="PascalCaseSplitter.Split"/> as the final safety net.
        /// First exception per objectID is logged once via <c>warnedIds</c>.
        /// </summary>
        private static (string text, bool dontLocalize) ResolveOne(ObjectDataCD od, bool localize)
        {
            try
            {
                var fields = PlayerController.GetObjectName(
                    new ContainedObjectsBuffer { objectData = od },
                    localize);
                return (fields.text?.Replace("\n", " "), fields.dontLocalize);
            }
            catch (NullReferenceException ex)
            {
                if (warnedIds.Add(od.objectID))
                    Debug.LogWarning(
                        $"[ItemChecklist] GetObjectName({od.objectID}, localize={localize}) threw NullReferenceException: {ex.Message}");
            }
            catch (Exception ex)
            {
                if (warnedIds.Add(od.objectID))
                    Debug.LogWarning(
                        $"[ItemChecklist] GetObjectName({od.objectID}, localize={localize}) threw (non-NRE): {ex.Message}");
            }
            return (null, false);
        }

        /// <summary>
        /// Resolve the originating mod's display name for an object, or "" if
        /// the object is vanilla. Uses ItemBrowser's approach: scan
        /// <c>Manager.mod.ExtraAuthoring</c> (the runtime-registered
        /// authoring components added by mods) and match the entry by
        /// <c>ObjectAuthoring</c>. Vanilla items are not in that list, so they
        /// naturally fall through to the empty string.
        /// </summary>
        private static string ResolveModOrigin(ObjectDataCD od, Dictionary<long, string> modIdToName)
        {
            try
            {
                if (Manager.mod == null || Manager.mod.ExtraAuthoring == null)
                    return string.Empty;

                foreach (var authoring in Manager.mod.ExtraAuthoring)
                {
                    if (authoring == null) continue;
                    var go = authoring.gameObject;
                    if (go == null) continue;

                    if (!go.TryGetComponent<ObjectAuthoring>(out var objectAuthoring))
                        continue;

                    var entryObjectId = API.Authoring.GetObjectID(objectAuthoring.objectName);
                    if (entryObjectId != od.objectID) continue;
                    if (objectAuthoring.variation != od.variation) continue;

                    // Found a mod-side authoring for this object. We don't have a
                    // direct asset → mod-id link here (ItemBrowser uses a cached
                    // InstanceID map for that). Fall back to the first loaded
                    // non-game mod whose name matches a colon-prefix in
                    // objectName ("ModName:Object"), else return empty.
                    var internalName = objectAuthoring.objectName ?? string.Empty;
                    var colonIdx = internalName.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var sourceMod = Normalize(internalName.Substring(0, colonIdx));
                        foreach (var mod in API.ModLoader.LoadedMods)
                        {
                            if (Normalize(mod.Metadata.name) == sourceMod)
                                return modIdToName.TryGetValue(mod.ModId, out var displayName)
                                    ? displayName
                                    : mod.Metadata.name;
                        }
                    }

                    return string.Empty;
                }
            }
            catch
            {
                // Manager.mod / ExtraAuthoring may not be initialized in some
                // contexts — treat as vanilla.
            }

            return string.Empty;
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
        }

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
            Dictionary<long, Rarity> rarityCache,
            Dictionary<long, ObjectType> objectTypeCache,
            Dictionary<long, int> levelCache,
            Dictionary<long, int> sellValueCache,
            Dictionary<long, bool> craftableCache,
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
            rarityCache[key] = info.rarity;
            objectTypeCache[key] = info.objectType;
            levelCache[key]      = PugDatabase.TryGetComponent<LevelCD>(od, out var lvlCdCooked) ? lvlCdCooked.level : 0;
            sellValueCache[key]  = ComputeSellValue(od, info);
            craftableCache[key]  = info.requiredObjectsToCraft != null && info.requiredObjectsToCraft.Count > 0;
        }

        // Faithful port of ItemBrowser ObjectUtility.GetValue (sell mode) +
        // GetRaritySellValue. Returns 0 for unsellable items (None /
        // CantBeSoldAuthoring / Legendary) — the row renders 0 as "—".
        // sellValue < 0 is CK's "auto-compute" marker, NOT "unsellable":
        // derive from rarity + crafting ingredients (+ cooked-food recursion).
        private static int ComputeSellValue(ObjectDataCD od, ObjectInfo info)
        {
            if (info == null
                || PugDatabase.HasComponent<CantBeSoldAuthoring>(od)
                || info.rarity == Rarity.Legendary)
                return 0;

            int sellValue = info.sellValue;
            if (sellValue < 0)
            {
                sellValue = GetRaritySellValue(info.rarity);

                if (PugDatabase.HasComponent<CookedFoodAuthoring>(od))
                {
                    var primary = CookedFoodCD.GetPrimaryIngredientFromVariation(od.variation);
                    var secondary = CookedFoodCD.GetSecondaryIngredientFromVariation(od.variation);
                    sellValue = ComputeSellValue(primary) + ComputeSellValue(secondary);
                }
                else if (info.requiredObjectsToCraft != null)
                {
                    int extraSellFromIngredients = 0;
                    foreach (var craftingObject in info.requiredObjectsToCraft)
                    {
                        var ingredientInfo = PugDatabase.GetObjectInfo(craftingObject.objectID, 0);
                        if (ingredientInfo != null && ingredientInfo.sellValue != 0)
                            extraSellFromIngredients += GetRaritySellValue(ingredientInfo.rarity) * craftingObject.amount;
                    }
                    if (extraSellFromIngredients > 0)
                        sellValue = (int)math.round(math.max(1f, sellValue * 0.3f) + extraSellFromIngredients);
                }

                var randomization = Unity.Mathematics.Random.CreateFromIndex((uint)od.objectID).NextFloat(-0.1f, 0.1f);
                sellValue = math.max(1, sellValue + (int)math.round(sellValue * randomization));
            }
            return sellValue;
        }

        // Recursion entry for cooked-food ingredient values (mirrors IB's
        // GetValue(ObjectID, 0)): unsellable ingredients contribute 0.
        private static int ComputeSellValue(ObjectID id)
        {
            var od = new ObjectDataCD { objectID = id, variation = 0 };
            return ComputeSellValue(od, PugDatabase.GetObjectInfo(id, 0));
        }

        // from ItemBrowser ObjectUtility.GetRaritySellValue (InventoryUtility).
        private static int GetRaritySellValue(Rarity rarity) => 1 + math.max(0, (int)rarity) * 5;
    }
}
