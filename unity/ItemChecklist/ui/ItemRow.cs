using System.Collections.Generic;
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
        public PugText possessionText;    // Iter-20: right-aligned "owned" count column

        public const float RowHeight = 1.5f; // world units (~24px at 16 PPU)

        // Iter-22: the row's current identity, captured each Bind so the hover
        // virtuals (called by UIMouse on the selected row) describe THIS item.
        private int _objectId;
        private int _ckVariation;     // CK object variation (pets → 0, not skinIndex)
        private bool _nameKnown;      // tooltip allowed iff the row shows a real name
        private TooltipSlot _tooltip; // shared helper (one per window), injected by Content

        public void SetTooltipHelper(TooltipSlot helper) => _tooltip = helper;

        // Iter-22: a 3D collider on the row root (where this UIelement lives, so CK's
        // UIMouse GetComponent<UIelement>() resolves to it) lets UIMouse's
        // Physics.Raycast (UILayerMask) hover-select the row. Authored in the prefab,
        // always enabled — every row is hoverable (so the highlight shows on all rows);
        // the tooltip, not the collider, is discovery-gated (in the hover overrides).
        public BoxCollider hoverCollider;

        // Iter-22: hover highlight — a SpriteRenderer child fully authored in the prefab
        // (sprite + size + sorting + 9-slice + position, all Editor-owned). Code only
        // toggles its visibility on select/deselect; it never touches the sprite.
        public SpriteRenderer highlight;

        // Ancient Coin icon, resolved once from the game database and shared by
        // every row (the coin shown beside sell values).
        private static Sprite s_coinSprite;
        private static bool s_coinResolved;

        // Iter-20: the checkbox AND the "done" tick turn blue when the player owns ≥1.
        // The prefab tints both white, so owned==0 rows just reset to white (the pool
        // recycles, so the colour must be set each bind).
        private static readonly Color OwnedTint = new Color(0.35f, 0.65f, 1f);

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

        // Iter-16.1: name and detail visibility are DECOUPLED. For normal items both
        // equal "discovered". For pet skins they differ: `nameKnown` = the pet SPECIES
        // is discovered (CK var-0), `showDetails` = THIS skin is collected (mod ledger)
        // — so a known species' uncollected skin shows the name but the unknown icon + —.
        public void Bind(int objectId, Sprite iconSprite, string name, bool nameKnown,
            bool showDetails, Color rarityColor, Rarity rarity, int level, int sellValue,
            int possessionCount, bool isPetSkin, int skinIndex)
        {
            if (label != null) label.Render(nameKnown ? name : "???");

            if (icon != null)
            {
                if (showDetails)
                {
                    // Real icon. Pet skins recolor the base sprite via a gradient map;
                    // everything else is a plain sprite (keyword reset for the pool).
                    if (isPetSkin) PetSkinIcon.Apply(icon, objectId, skinIndex, iconSprite);
                    else { PetSkinIcon.Disable(icon); icon.sprite = iconSprite; icon.color = Color.white; }
                    icon.enabled = icon.sprite != null;
                }
                else
                {
                    // Undiscovered/uncollected: the "unknown object" sprite, rarity-tinted.
                    PetSkinIcon.Disable(icon);
                    icon.sprite = unknownIcon; icon.enabled = unknownIcon != null; icon.color = rarityColor;
                }

                // NATIVE scale; per-item iconOffset only for a real non-pet icon (pets +
                // the "?" placeholder stay centred). Reset each bind (the pool recycles).
                icon.transform.localScale = Vector3.one;
                if (showDetails && !isPetSkin)
                {
                    var info = PugDatabase.GetObjectInfo((ObjectID)objectId, 0);
                    icon.transform.localPosition = info != null ? info.iconOffset : Vector3.zero;
                }
                else
                    icon.transform.localPosition = Vector3.zero;
            }

            // Checkbox: empty box on every row; the requirement icon fills it only
            // when the item is discovered (the checklist "done" tick).
            // Iter-20: both the box and the "done" tick go blue when the player owns ≥1.
            bool owned = possessionCount >= 1;
            if (checkmark != null)
            {
                checkmark.enabled = true;
                checkmark.color = owned ? OwnedTint : Color.white;
            }
            if (checkFill != null)
            {
                checkFill.enabled = showDetails;
                if (showDetails)
                    checkFill.color = owned ? OwnedTint : Color.white;
            }

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
                levelText.Render(showDetails && level > 0 ? ItemChecklist.Loc.F("ItemChecklist-General/Level", level) : Dash);
            if (valueText != null)
                valueText.Render(showDetails && sellValue > 0 ? sellValue.ToString() : Dash);

            if (coinIcon != null)
            {
                bool showCoin = showDetails && sellValue > 0;
                if (showCoin && coinIcon.sprite == null)
                    coinIcon.sprite = CoinSprite();
                coinIcon.enabled = showCoin && coinIcon.sprite != null;
            }

            // Iter-20: possession count. Undiscovered = "—" (can't own an undiscovered
            // item — acquiring discovers it). Rendered plainly like the Lv/Value
            // columns (no live/remembered marker — the player only cares about the
            // count, not whether the source chunk is currently loaded).
            if (possessionText != null)
                possessionText.Render(showDetails ? possessionCount.ToString() : Dash);

            // Iter-22: remember identity for the hover virtuals. Pets sit at CK
            // variation 0 — the `skinIndex` param is a skin selector, not a CK variation.
            _objectId = objectId;
            _ckVariation = isPetSkin ? 0 : skinIndex;
            _nameKnown = nameKnown;

            // Iter-22: every row is hover-selectable (collider always enabled) so the
            // highlight appears on all rows. The TOOLTIP is gated on discovery in the
            // four hover overrides instead — ??? rows highlight but show no tooltip
            // (spoiler-safe, since the highlight reveals nothing). The highlight itself
            // is driven per-frame in LateUpdate, not here.
            if (hoverCollider != null) hoverCollider.enabled = true;
        }

        // Iter-22 — native CK tooltip. UIMouse selects this row (collider always on)
        // and calls these. Gate on _nameKnown so undiscovered rows show NO tooltip —
        // the highlight still appears (spoiler-safe, it reveals nothing).
        public override ContainedObjectsBuffer GetContainedObject()
        {
            if (!ItemChecklistContent.PointerInViewport()) return default;
            if (!_nameKnown || _tooltip == null) return default;
            _tooltip.SetObject((ObjectID)_objectId, _ckVariation);
            return _tooltip.GetSlotObjectPublic();
        }

        public override TextAndFormatFields GetHoverTitle()
        {
            if (!ItemChecklistContent.PointerInViewport()) return null;
            // Undiscovered: a minimal placeholder title only — no description/stats/icon
            // (those overrides stay gated), so the row hovers + shows "??? - not yet
            // discovered" without leaking the item. Resolve our term ourselves (CK's
            // tooltip localiser doesn't see mod terms) and pass it dontLocalize.
            if (!_nameKnown)
                return new TextAndFormatFields
                {
                    text = ItemChecklist.Loc.T("ItemChecklist-General/HoverUndiscovered"),
                    dontLocalize = true,
                };
            if (_tooltip == null) return null;
            _tooltip.SetObject((ObjectID)_objectId, _ckVariation);
            return _tooltip.TitleFor();
        }

        public override List<TextAndFormatFields> GetHoverDescription()
        {
            if (!ItemChecklistContent.PointerInViewport()) return null;
            // Undiscovered: a small hint line under the placeholder title (still no real
            // item info). Resolved via our term, passed dontLocalize.
            if (!_nameKnown)
                return new List<TextAndFormatFields>
                {
                    new TextAndFormatFields
                    {
                        text = ItemChecklist.Loc.T("ItemChecklist-General/HoverUndiscoveredDesc"),
                        dontLocalize = true,
                    },
                };
            if (_tooltip == null) return null;
            _tooltip.SetObject((ObjectID)_objectId, _ckVariation);
            return _tooltip.DescriptionFor();
        }

        public override List<TextAndFormatFields> GetHoverStats(bool previewReinforced)
        {
            if (!ItemChecklistContent.PointerInViewport()) return null;
            if (!_nameKnown || _tooltip == null) return null;
            _tooltip.SetObject((ObjectID)_objectId, _ckVariation);
            return _tooltip.StatsFor();
        }

        // Iter-22: drive the highlight per-frame from selection state — NOT from the
        // OnSelected/OnDeselected one-shots — so it stays correct in every case,
        // including when the cursor moves onto an open popup over an already-selected
        // row (whose full-width collider would otherwise leak the highlight behind the
        // popup). Shows for EVERY selected row (discovered or not); the tooltip is the
        // only discovery-gated part (in the hover overrides above).
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (highlight == null) return;
            bool show = Manager.ui.currentSelectedUIElement == this
                        && ItemChecklistContent.PointerInViewport();
            if (highlight.enabled != show) highlight.enabled = show;
        }
    }
}
