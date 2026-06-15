using PugMod;
using UnityEngine;

namespace ItemChecklist.UI
{
    public sealed class ItemRow : UIelement
    {
        // Editor-wired serialized fields (4-slot structure preserved from ItemRowView)
        public SpriteRenderer background;
        public SpriteRenderer icon;
        public PugText label;
        public Sprite unknownIcon;           // Iter-12: sprite shown in the icon slot for undiscovered items
        public SpriteRenderer checkmark;     // empty checkbox, shown on every row
        public SpriteRenderer checkFill;     // requirement icon inside the box, discovered only
        public SpriteRenderer rarityBorder;   // Iter-6: rarity frame, shown for Uncommon+
        public PugText levelText;    // Iter-10: right-aligned "Lv N" column ("—" if level 0 / undiscovered)
        public PugText valueText;    // Iter-10: right-aligned sell-value column ("—" if unsellable / undiscovered)
        public SpriteRenderer coinIcon;   // Iter-10: Ancient Coin glyph beside the value (shown only when a value is shown)

        public const float RowHeight = 1.5f; // world units (~24px at 16 PPU)

        // Ancient Coin icon, resolved once from the game database and shared by
        // every row (the coin shown beside sell values).
        private static Sprite s_coinSprite;
        private static bool s_coinResolved;

        private static Sprite CoinSprite()
        {
            if (!s_coinResolved)
            {
                s_coinResolved = true;
                var info = PugDatabase.GetObjectInfo(ObjectID.AncientCoin, 0);
                if (info != null)
                    s_coinSprite = info.smallIcon != null ? info.smallIcon : info.icon;
            }
            return s_coinSprite;
        }

        public void Bind(int objectId, Sprite iconSprite, string name, bool isDiscovered,
            Color rarityColor, Rarity rarity, int level, int sellValue)
        {
            if (isDiscovered)
            {
                // Real item icon: never tinted (reset, since the pool recycles rows).
                if (icon != null) { icon.sprite = iconSprite; icon.enabled = true; icon.color = Color.white; }
                if (label != null) label.Render(name);
            }
            else
            {
                // Undiscovered: show the "unknown object" sprite in the icon slot (Iter-12),
                // tinted by rarity to match the name tint + rarity border (Common/Poor = default).
                if (icon != null) { icon.sprite = unknownIcon; icon.enabled = unknownIcon != null; icon.color = rarityColor; }
                if (label != null) label.Render("???");
            }

            // Position + size the icon like CK/IB: NATIVE scale (the 1.25u slot fits the
            // detail icons), positioned by the game's per-item iconOffset — relative to the
            // slot, since Icon is a child of IconSlot (CK: icon.localPosition = iconOffset).
            // Reset both each bind (the viewport pool recycles rows).
            if (icon != null)
            {
                icon.transform.localScale = Vector3.one;
                // iconOffset only for the real item icon; the "?" placeholder stays centred.
                if (isDiscovered)
                {
                    var info = PugDatabase.GetObjectInfo((ObjectID)objectId, 0);
                    icon.transform.localPosition = info != null ? info.iconOffset : Vector3.zero;
                }
                else
                    icon.transform.localPosition = Vector3.zero;
            }

            // Checkbox: empty box on every row; the requirement icon fills it only
            // when the item is discovered (the checklist "done" tick).
            if (checkmark != null) checkmark.enabled = true;
            if (checkFill != null) checkFill.enabled = isDiscovered;

            // Iter-6 rarity colouring. Set the colour AFTER Render(): SetTempColor
            // writes the glyph SpriteRenderers that Render() rebuilds, so a colour
            // set before Render() would be discarded. keepColorOnStart:true makes the
            // tint survive PugText's renderOnStart re-render (first open after a fresh
            // row instantiate), which would otherwise reset glyphs to style.color and
            // leave the tint blank until the next RefreshVisible.
            if (label != null) label.SetTempColor(rarityColor, keepColorOnStart: true);
            if (rarityBorder != null)
            {
                rarityBorder.color = rarityColor;
                rarityBorder.enabled = rarity >= Rarity.Uncommon;   // Poor + Common: no border
            }

            // Iter-10: Level + Value columns. Undiscovered = "—"/"—" (no spoiler).
            // Level 0 and unsellable (sellValue < 0) render "—".
            const string Dash = "—";
            if (levelText != null)
                levelText.Render(isDiscovered && level > 0 ? ItemChecklist.Loc.F("ItemChecklist-General/Level", level) : Dash);
            if (valueText != null)
                valueText.Render(isDiscovered && sellValue > 0 ? sellValue.ToString() : Dash);

            if (coinIcon != null)
            {
                bool showCoin = isDiscovered && sellValue > 0;
                if (showCoin && coinIcon.sprite == null)
                    coinIcon.sprite = CoinSprite();
                coinIcon.enabled = showCoin && coinIcon.sprite != null;
            }
        }
    }
}
