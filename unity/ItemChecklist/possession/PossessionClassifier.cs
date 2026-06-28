using System.Collections.Generic;

namespace ItemChecklist.Possession
{
    /// <summary>
    /// Pure classification of inventory entities into the possession buckets
    /// (spec §5). All signals are spike-verified against a real base.
    /// </summary>
    internal static class PossessionClassifier
    {
        // ObjectType.PlaceablePrefab — placed furniture/buildings (chests, pedestals,
        // mannequins, stations). NonUsable=0 (DroppedItem/TheCore), Creature=900 (NPCs).
        public const int PlaceablePrefab = 800;

        // Locked*Chest ObjectIDs: contents are unknown until opened, and opening swaps
        // the ObjectID to the Unlocked/normal variant (which then counts). No runtime
        // lock component exists, so this static set is the signal.
        public static readonly HashSet<int> LockedChestIds = new HashSet<int>
        {
            181, 182, 183,            // LockedPrince/Queen/KingChest (boss)
            210, 213, 216, 219, 222, 225, 950  // LockedCopper/Iron/Scarlet/Octarine/Galaxite/Solarite/ReluciteChest
        };

        public static bool IsLockedChest(int objectId) => LockedChestIds.Contains(objectId);

        // Boss-summon statues (LarvaBossStatue 4101 … ExcavationBossStatue 4109 — a
        // contiguous block). Placeable + in the catalog (discoverable), but typed
        // NonUsable(0) + not Mineable, so the generic furniture filter misses them.
        // Counted explicitly as owned placed objects (no contents — they are stations).
        public static bool IsBossStatue(int objectId) => objectId >= 4101 && objectId <= 4109;

        // Iter-28: world-spawned nature (bushes/grass/kelp/stalagmites/lilies/ruins/…) is
        // NOT a player possession. It must be excluded from the "count the placed object
        // itself" path (PossessionScanner path #1) so it never enters the per-(x,z) ledger
        // — that ledger had grown to ~5500 entries (≈90% wild nature), and re-serialising it
        // every autosave was a 12–37ms main-thread spike. Nature still counts when actually
        // STORED in a container or carried (path #2), which this predicate does NOT touch.
        //
        // No object-level signal separates wild nature from placed objects in CK — across
        // cat/stack/icon/craft/sell/tags/DontDropSelfCD/Diggable/Destructible, wild nature
        // collides with kept objects (Stalagmite ≡ CavelingFloorTile; GraveTree ≡ WayPoint ≡
        // Idol ≡ RuinsPiece). So this is a curated blacklist: nature TAGS catch the bulk
        // (incl. future tagged nature), a small ID list catches the tag-less stragglers.
        // Editable — add/remove freely as edge cases surface.
        //
        // Stable ObjectCategoryTag int values: Ruins=4, Greenery=5, Destructible=13,
        // CattleKelpFood=33.
        private static readonly HashSet<int> WorldNatureTagValues = new HashSet<int> { 4, 5, 13, 33 };

        // Wild nature with NO nature tag → matched explicitly by ObjectID.
        private static readonly HashSet<int> WorldNatureIds = new HashSet<int>
        {
            5610,  // Stalagmite
            5614,  // WaterLily
            5622,  // GraveTree
            5804,  // TallLandKelp
            5710,  // CavelingFloorTileDark (natural biome floor)
            5571,  // RuinsPiece2

            // Iter-30 diag dump: wild mineable ore/boulder deposits — placed near base
            // anchors but never owned, tag-less so caught only by ID. These were the
            // churn source defeating the autosave-skip (counted as placed objects, added
            // then PruneStaleNear-removed as the player roams). The diag saw Iron(2202)/
            // Gold(2203); the rest of the family is listed for cross-biome coverage.
            2200, 2201, 2202, 2203, 2204,  // Copper/Tin/Iron/Gold/Scarlet OreBoulder
            2205, 2206, 2207, 2209, 2218,  // Octarine/Galaxite/Solarite/Pandorium/Relucite OreBoulder
            5606,  // AmberBoulder
            5879,  // CrystalMeteorBoulder
            // NOTE: MeadowTree (15500) is deliberately NOT here. A tree within the base
            // anchor radius is plausibly placed décor (user wants it counted as owned),
            // and the anchor gate already excludes wild trees OUTSIDE the base from path
            // #1 — so leaving it un-blacklisted gives "kept in base, ignored outside" for
            // free. Ore boulders differ: never owned, even right next to base → blacklist.
        };

        public static bool IsWorldNature(int objectId, ObjectInfo info)
        {
            if (WorldNatureIds.Contains(objectId)) return true;
            if (info != null && info.tags != null)
                foreach (var t in info.tags)
                    if (WorldNatureTagValues.Contains((int)t)) return true;
            return false;
        }
    }
}
