namespace ItemChecklist.UI
{
    public enum ItemCategory
    {
        Weapons, ArmorAccessories, Tools, Food, Placeables,
        Materials, Valuables, KeyItems, Instruments, Pets, Critters, Cattle, Other
    }

    public static class ItemCategories
    {
        // Display order = enum order. "Other" is the catch-all fallback.
        public static readonly ItemCategory[] All =
        {
            ItemCategory.Weapons, ItemCategory.ArmorAccessories, ItemCategory.Tools,
            ItemCategory.Food, ItemCategory.Placeables, ItemCategory.Materials,
            ItemCategory.Valuables, ItemCategory.KeyItems, ItemCategory.Instruments,
            ItemCategory.Pets, ItemCategory.Critters, ItemCategory.Cattle, ItemCategory.Other
        };

        // Iter-16.3: cattle are ObjectType.Creature (which falls to the default → Other),
        // so the Cattle bucket is keyed on the explicit Entry.IsCattle flag — the single
        // point that already knows (CattleCD), passed in by the caller. This stays correct
        // even if a future non-cattle Creature is ever admitted to the catalog.
        public static ItemCategory Of(ObjectType t, bool isCattle = false)
        {
            if (isCattle) return ItemCategory.Cattle;
            switch (t)
            {
                case ObjectType.MeleeWeapon:
                case ObjectType.RangeWeapon:
                case ObjectType.SummoningWeapon:
                case ObjectType.ThrowingWeapon:
                case ObjectType.BeamWeapon:        // numeric 610 (tool range) but a weapon
                    return ItemCategory.Weapons;

                case ObjectType.Helm:
                case ObjectType.BreastArmor:
                case ObjectType.PantsArmor:
                case ObjectType.Necklace:
                case ObjectType.Ring:
                case ObjectType.Offhand:
                case ObjectType.Bag:
                case ObjectType.Lantern:
                case ObjectType.Pouch:
                    return ItemCategory.ArmorAccessories;

                case ObjectType.Shovel:
                case ObjectType.Hoe:
                case ObjectType.CastingItem:
                case ObjectType.MiningPick:
                case ObjectType.PaintTool:
                case ObjectType.FishingRod:
                case ObjectType.BugNet:
                case ObjectType.Sledge:
                case ObjectType.RoofingTool:
                case ObjectType.DrillTool:
                case ObjectType.WaterCan:
                case ObjectType.Bucket:
                case ObjectType.Seeder:
                    return ItemCategory.Tools;

                case ObjectType.Eatable:
                    return ItemCategory.Food;

                case ObjectType.PlaceablePrefab:
                    return ItemCategory.Placeables;

                case ObjectType.NonUsable:               // raw materials (ores/bars/wood)
                case ObjectType.UniqueCraftingComponent:
                    return ItemCategory.Materials;

                case ObjectType.Valuable:
                    return ItemCategory.Valuables;

                case ObjectType.KeyItem:
                    return ItemCategory.KeyItems;

                case ObjectType.Instrument:
                    return ItemCategory.Instruments;

                case ObjectType.Pet:
                    return ItemCategory.Pets;

                case ObjectType.Critter:        // Iter-16.2: net-catchable critters + fireflies
                    return ItemCategory.Critters;

                default:
                    return ItemCategory.Other;
            }
        }
    }
}
