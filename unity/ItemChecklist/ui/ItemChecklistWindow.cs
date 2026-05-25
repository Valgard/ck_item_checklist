using CoreLib.Submodule.UserInterface.Interface;
using UnityEngine;

namespace ItemChecklist.UI
{
    public class ItemChecklistWindow : UIelement, IModUI
    {
        // Editor-wired serialized fields
        public GameObject root;
        public SpriteRenderer background;
        public PugText title;

        // IModUI implementation
        public GameObject Root => root;
        public bool ShowWithPlayerInventory => false;
        public bool ShouldPlayerCraftingShow => false;

        protected void Awake()
        {
            HideUI();
        }

        public void ShowUI()
        {
            root.SetActive(true);
            ApplyTheme();
        }

        public void HideUI()
        {
            root.SetActive(false);
        }

        private void ApplyTheme()
        {
            // Vanilla CK CraftingUI theme as 9-slice background.
            // GetCraftingUITheme takes UIManager.CraftingUIThemeType enum
            // (verified from BookMod/Scripts/UI/BookUI.cs:71). Wood is an
            // educated guess from naming convention; if invalid, fallback
            // path loads our own atlas sprite.
            try
            {
                var theme = Manager.ui.GetCraftingUITheme(UIManager.CraftingUIThemeType.Wood);
                if (theme != null && background != null)
                    background.sprite = theme.background;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemChecklist] GetCraftingUITheme failed: {ex.Message} — falling back to atlas sprite");
                if (background != null && ItemChecklistMod.AssetBundle != null)
                    background.sprite = ItemChecklistMod.AssetBundle.LoadAsset<Sprite>("ui_panel");
            }

            if (title != null)
                title.Render("Item Checklist");
        }
    }
}
