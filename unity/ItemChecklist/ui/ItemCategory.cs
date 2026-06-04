namespace ItemChecklist.UI
{
    public enum ItemCategory
    {
        Weapons, ArmorAccessories, Tools, Food, Placeables,
        Materials, Valuables, KeyItems, Instruments, Other
    }

    public static class ItemCategories
    {
        // Display order = enum order. "Other" is the catch-all fallback.
        public static readonly ItemCategory[] All =
        {
            ItemCategory.Weapons, ItemCategory.ArmorAccessories, ItemCategory.Tools,
            ItemCategory.Food, ItemCategory.Placeables, ItemCategory.Materials,
            ItemCategory.Valuables, ItemCategory.KeyItems, ItemCategory.Instruments,
            ItemCategory.Other
        };

        public static ItemCategory Of(ObjectType t)
        {
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

                default:
                    return ItemCategory.Other;
            }
        }
    }
}
