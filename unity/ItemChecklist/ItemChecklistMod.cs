using System.Linq;
using CoreLib;
using CoreLib.Submodule.ControlMapping;
using CoreLib.Submodule.UserInterface;
using ItemChecklist.Possession;
using ItemChecklist.UI;
using PugMod;
using Rewired;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Mod bootstrap. After the Harmony pivot, the heavy lifting is in
    /// the two patch classes (<see cref="SaveManagerDiscoveryHook"/> and
    /// <see cref="CharacterDataDiscoverySnapshot"/>) that mirror CK's
    /// native discovery system into <see cref="DiscoveredState"/>.
    ///
    /// <para>The only non-trivial work this class does is bridge the
    /// timing gap between CK's <c>OnAfterDeserialize</c> (which fires
    /// before <c>Manager.main.player</c> exists) and the active player's
    /// spawn. Each frame, if a player has spawned, look up the cached
    /// snapshot by <c>playerName</c> and apply it. See
    /// <see cref="CharacterDataDiscoverySnapshot"/> for the rationale
    /// behind the name-based cache key (more accurate alternatives all
    /// turned out sandbox-blocked).</para>
    /// </summary>
    public sealed class ItemChecklistMod : IMod
    {
        public static ItemCatalog Catalog { get; private set; }
        public static ItemChecklist.UI.ItemListViewModel ListView { get; internal set; }

        // AssetBundle reference — set in EarlyInit. UI code loads sprites
        // (window background, placeholder icon, etc.) via
        // AssetBundle.LoadAsset<Sprite>("Assets/ItemChecklist/Art/..."),
        // same pattern Item Browser uses.
        public static AssetBundle AssetBundle { get; private set; }
        public static LoadedMod ModInfo { get; private set; }

        // Always-on HUD counter prefab, captured in ModObjectLoaded and
        // instantiated lazily in Update once the UIManager exists. NOT routed
        // through CoreLib's modal RegisterModUI (which hides on
        // HideAllInventoryAndCraftingUI — the opposite of an always-on HUD).
        private static GameObject hudPrefab;

        // Iter-20: current possession snapshot, read by ItemChecklistContent.Rebind
        // per visible row. Refreshed on open + a throttled interval; the per-(x,z)
        // ledger is loaded/saved around character (GUID) activation.
        internal static PossessionView Possession { get; private set; } = PossessionView.Empty;

        // Iter-16.1: mod-owned per-skin "ever-owned" collection (CK does not track pet
        // skin discovery). Loaded/saved per character GUID alongside the possession
        // ledger; updated each scan; drives pet-skin rows' collected flag.
        internal static PetCollection Pets { get; private set; }

        // Iter-21: possession is spoiler-gated behind discovery. An undiscovered row
        // renders "???" and already em-dashes Level/Value (Iter-10 spoiler guard);
        // showing an owned count there is the same kind of spoiler — and produced the
        // incoherent "owned but never discovered" state for world-spawned placed
        // objects (e.g. a Core WayPoint the player never picked up, which the Iter-20
        // world scan counts). Gate on the SAME discovered flag the row uses for
        // ???-vs-name, so an undiscovered row can never show possession. Returns 0
        // (treated as "not owned" by the column, blue tint, and the possession filter)
        // when undiscovered or before the discovery snapshot has loaded.
        internal static int OwnedCount(int objectId, int variation)
        {
            // Iter-16.1: pet-skin rows route through PetCollection (collected) + the
            // per-skin live count — CK's DiscoveredState is blind to skins (variation
            // is force-zeroed for pets). For pets, `variation` carries the skinIndex.
            if (Catalog != null && Catalog.IsPetSkinEntry(objectId, variation))
                return Pets != null && Pets.IsCollected(objectId, variation)
                    ? Possession.CountSkin(objectId, variation)
                    : 0;

            var disc = DiscoveredState.Instance;
            // Iter-17: cattle colour slots get their OWN live owned count (per
            // (adultId, colour)), spoiler-gated on that colour being discovered — so each of
            // the 5 slots shows how many of THAT colour are penned/caged, not the species total.
            if (Catalog != null && Catalog.IsCattleEntry(objectId, variation))
                return disc != null && disc.IsDiscovered(objectId, variation)
                    ? Possession.CountColour(objectId, variation)
                    : 0;
            return disc != null && disc.IsDiscovered(objectId, variation)
                ? Possession.Count(objectId)
                : 0;
        }

        // Iter-16.4: the discovery twin of OwnedCount — the single "is this catalog
        // row complete / ticked?" predicate. Normal/critter rows: CK's DiscoveredState
        // (one row each). Pet-skin rows: PetCollection.IsCollected — CK force-zeroes pet
        // variation in SetObjectAsDiscovered, so DiscoveredState is blind to skins and
        // every collected skin would otherwise test as undiscovered. This is exactly the
        // ItemRow `showDetails` flag (the checkbox tick), routed through one place so the
        // Discovery filter, the in-view count, and the N/M numerator cannot drift from
        // the per-row tick (the Iter-21 chokepoint lesson). `variation` carries the
        // skinIndex for pet entries. Safe default (false) before the snapshot loads.
        internal static bool IsCollected(int objectId, int variation)
        {
            if (Catalog != null && Catalog.IsPetSkinEntry(objectId, variation))
                return Pets != null && Pets.IsCollected(objectId, variation);

            var disc = DiscoveredState.Instance;
            if (disc == null) return false;
            // Iter-17: cattle colour slots route through normal per-(id, colour) discovery
            // (the "collected" tick reflects the SPECIFIC colour caught). The species-name
            // gate (show all 5 slots named once any colour is found) lives in
            // ItemChecklistContent's nameKnown, NOT here — collected ≠ name-known for cattle.
            return disc.IsDiscovered(objectId, variation);
        }

        // Iter-16.4: the N/M numerator — how many catalog rows are collected. Replaces
        // DiscoveredState.Count, which counts each pet SPECIES once (variation force-zeroed)
        // while M = catalog.Count counts every skin row, making 100% unreachable. Counted
        // over the catalog through IsCollected so it stays consistent with the per-row tick
        // and the Discovery filter. O(catalog) but called only on discovery-change / bake /
        // collection-change refreshes, never per frame.
        internal static int CollectedCatalogCount()
        {
            var cat = Catalog;
            if (cat == null) return 0;
            int n = 0;
            for (int i = 0; i < cat.Count; i++)
            {
                var e = cat.GetByIndex(i);
                if (IsCollected(e.ObjectId, e.Variation)) n++;
            }
            return n;
        }

        // Iter-16.4: last collected-catalog tally pushed to the always-on HUD, so a newly
        // collected pet skin (which fires NO DiscoveredState.Changed — CK has no per-skin
        // discovery event) still updates the HUD counter live. The window self-heals on its
        // own (it rescans + RenderStatus on every open); only the always-on HUD needs this
        // nudge. -1 = "unknown" so the first scan always refreshes.
        private static int s_lastHudCollected = -1;

        // Iter-16.4: re-render the always-on HUD counter only when the collected tally
        // actually changed — PugText.Render rebuilds glyph SpriteRenderers, so the 3s
        // possession poll must not churn it every tick.
        private static void RefreshHudCounterIfChanged()
        {
            int collected = CollectedCatalogCount();
            if (collected == s_lastHudCollected) return;
            s_lastHudCollected = collected;
            ItemChecklist.UI.ItemChecklistHud.Instance?.Refresh();
        }

        private static PossessionLedger s_ledger;
        private static string s_ledgerGuid;
        private const float PossessionRefreshSeconds = 3f;
        private float _possessionTimer;
        // Prune grace: chunks stream in asynchronously after a world load/teleport, so
        // the self-heal prune must stay off until the world has been continuously
        // playable for this long (else it deletes not-yet-streamed real storage and
        // overwrites the persisted file). Reset whenever we leave the playable world.
        private const float PossessionPruneGraceSeconds = 8f;
        private float _possessionPlayableTime;

        // Rewired player captured via ControlMappingModule.rewiredStart
        // (Rewired is not ready at EarlyInit). Used to poll the bound
        // toggle action each frame.
        private static Player rewiredPlayer;

        private const string ToggleActionName = "ItemChecklist-ToggleChecklist";

        // Last character name we applied a snapshot for. Reset when
        // Manager.main.player goes back to null (main menu) so the next
        // char-load gets its own snapshot pushed.
        private string lastAppliedFor;

        public void EarlyInit()
        {
            Debug.Log("[ItemChecklist] EarlyInit");

            // Grab our AssetBundle handle (sprites for the UI live here).
            // Pattern from Item Browser's Main.EarlyInit.
            ModInfo = API.ModLoader.LoadedMods.FirstOrDefault(m => m.Handlers.Contains(this));
            if (ModInfo != null && ModInfo.AssetBundles != null && ModInfo.AssetBundles.Count > 0)
            {
                AssetBundle = ModInfo.AssetBundles[0];
                var names = AssetBundle.GetAllAssetNames();
                Debug.Log($"[ItemChecklist] AssetBundle loaded with {names.Length} assets:");
                foreach (var n in names) Debug.Log($"[ItemChecklist]   asset: {n}");
            }
            else
            {
                Debug.LogWarning("[ItemChecklist] AssetBundle not available — UI will fall back to default Unity sprites");
            }

            CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule));
            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));

            // Register the toggle keybind (default F1). CoreLib's
            // ControlMappingModule wires this into Rewired's UserData so the
            // user can rebind it through the game's input-settings UI.
            ControlMappingModule.AddKeyboardBind(
                keyBindName: ToggleActionName,
                defaultKeyCode: KeyboardKeyCode.F1,
                modifier: ModifierKey.None,
                modifier2: ModifierKey.None,
                modifier3: ModifierKey.None,
                categoryId: -1);     // -1 = default "Mods" category

            // Rewired isn't initialized at EarlyInit; subscribe to the
            // rewiredStart hook so we grab the player handle as soon as it
            // exists. Mirrors CoreLib's own CommandModule pattern.
            ControlMappingModule.rewiredStart += () =>
            {
                rewiredPlayer = ReInput.players.GetPlayer(0);
                Debug.Log("[ItemChecklist] Rewired player captured");
            };
        }

        public void Init()
        {
            Debug.Log("[ItemChecklist] Init");
            Catalog = new ItemCatalog();
            ItemCatalogLocChangeHook.Subscribe();
            // Iter-17: cattle colour slots are FIXED (all 5 per species baked up front),
            // so a newly-caught colour needs no re-bake — CK discovers cattle per
            // (id, variation) → SaveManagerDiscoveryHook → DiscoveredState.Changed, and the
            // existing window/HUD Changed subscriptions re-render the affected slots (and
            // flip the whole species to named on its first colour). No special path needed.
            // Bake() is triggered by ItemCatalogWorldLoadHook.
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj is GameObject go)
            {
                // Route loaded mod prefabs by name (whitelist, not else-register):
                //  - the checklist window is the only modal UI registered with CoreLib;
                //  - the HUD counter is non-modal, captured for our own lazy
                //    instantiation in Update;
                //  - everything else is a reusable building-block prefab
                //    (Dropdown.prefab and its Sort/Filter variants), nested
                //    into the window and never opened on its own — it must NOT be
                //    registered as a modal UI.
                if (go.name == "ItemChecklistWindow")
                    UserInterfaceModule.RegisterModUI(go);
                else if (go.name == "ItemChecklistHUD")
                    hudPrefab = go;
                else
                    // Building-block prefab (Dropdown.prefab + its variants):
                    // nested into the window, never opened on its own. Logged
                    // (not silently dropped) so the skip is traceable in Player.log.
                    Debug.Log($"[ItemChecklist] Skipping RegisterModUI for building-block prefab '{go.name}'");
            }
        }
        public void Shutdown()
        {
            // Backstop autosave on a clean quit (mods unload on "Beenden"), for any
            // quit path that did not route through a CK character write.
            SavePossessionLedger();
        }

        /// <summary>Persist the active character's possession ledger. Driven primarily
        /// by <see cref="SaveManagerWriteCharacterHook"/> (fires in lockstep with CK's
        /// own character save — autosave + "Save &amp; Quit"); also called on char-switch
        /// and Shutdown as backstops. No-op when no character is active.</summary>
        internal static void SavePossessionLedger()
        {
            if (s_ledger != null && !string.IsNullOrEmpty(s_ledgerGuid))
                PossessionStore.Save(s_ledgerGuid, s_ledger);
            if (Pets != null && Pets.Dirty && !string.IsNullOrEmpty(s_ledgerGuid))
                PetCollectionStore.Save(s_ledgerGuid, Pets);
        }

        public void Update()
        {
            // Iter-20: throttled possession refresh while playing. The prune is only
            // allowed once the world has been playable past the grace window (chunks
            // streamed); leaving the playable world re-arms it.
            if (s_ledger != null && WorldState.IsInPlayableWorld)
            {
                _possessionPlayableTime += Time.deltaTime;
                _possessionTimer -= Time.deltaTime;
                if (_possessionTimer <= 0f)
                {
                    _possessionTimer = PossessionRefreshSeconds;
                    bool allowPrune = _possessionPlayableTime > PossessionPruneGraceSeconds;
                    Possession = PossessionScanner.Scan(s_ledger, Pets, PossessionConfig.AnchorRadius, allowPrune);
                    // A scan may have newly collected a pet skin (no DiscoveredState.Changed
                    // fires for that — CK has no per-skin discovery event), so nudge the HUD.
                    RefreshHudCounterIfChanged();
                }
            }
            else
            {
                _possessionPlayableTime = 0f;   // left the playable world → re-arm grace
            }

            // Instantiate the always-on HUD counter once the UIManager and its
            // HUD hierarchy exist. Parent under chestInventoryUI's parent — the
            // in-game HUD root (where CK's HUD lives and CoreLib mounts modal
            // UIs). The instance's Awake sets ItemChecklistHud.Instance.
            if (hudPrefab != null
                && ItemChecklist.UI.ItemChecklistHud.Instance == null
                && Manager.ui != null
                && Manager.ui.chestInventoryUI != null)
            {
                Object.Instantiate(hudPrefab, Manager.ui.chestInventoryUI.transform.parent);
            }

            // Deferred language-change re-bake (set by ItemCatalogLocChangeHook;
            // run here, post-DoLocalizeAll, to avoid the mid-localize GetObjectName NRE).
            ItemCatalogLocChangeHook.ProcessPending();

            string activeGuid = SaveManagerActiveSelectHook.ActiveGuid;

            // Iter-20: load/save the possession ledger on character (GUID) change.
            // Independent of the discovery-snapshot cache below (that can miss).
            if (activeGuid != s_ledgerGuid)
            {
                SavePossessionLedger();   // backstop: persist the outgoing char on switch
                if (string.IsNullOrEmpty(activeGuid))
                {
                    s_ledger = null;
                    Pets = null;
                    Possession = PossessionView.Empty;
                }
                else
                {
                    s_ledger = PossessionStore.Load(activeGuid);
                    Pets = PetCollectionStore.Load(activeGuid);
                    _possessionPlayableTime = 0f;   // fresh load → withhold prune until grace
                    Possession = PossessionScanner.Scan(s_ledger, Pets, PossessionConfig.AnchorRadius, false);
                }
                s_ledgerGuid = activeGuid;
            }

            // No active character (main menu) — clear "applied for"
            // memory so the next char-select pushes a fresh snapshot.
            if (string.IsNullOrEmpty(activeGuid))
            {
                if (lastAppliedFor != null) lastAppliedFor = null;
            }
            else if (activeGuid != lastAppliedFor
                && CharacterDataDiscoverySnapshot.Cache.TryGetValue(activeGuid, out var ids))
            {
                DiscoveredState.Instance.Snapshot(ids);
                lastAppliedFor = activeGuid;
                Debug.Log($"[ItemChecklist] Snapshot applied: {ids.Length} ids for guid {activeGuid}");
            }

            // Hotkey poll — the rebindable Rewired action is the SOLE toggle
            // trigger (default F1, remappable via the game's input settings).
            // A raw Input.GetKeyDown(KeyCode.F1) used to OR in here as a
            // "diagnostic fallback", but it was never gated to diagnostic-only,
            // so it kept F1 hardcoded as an opener even after the player rebound
            // the key — the rebind appeared not to take (Iter-23). Dropped: the
            // Rewired path already covers the default F1 binding.
            if (rewiredPlayer != null && rewiredPlayer.GetButtonDown(ToggleActionName))
            {
                // Open-state is read from ACTUAL visibility (Root.activeSelf is
                // the canonical open/closed signal), not CoreLib's
                // currentInterface — which InventoryOpenAutoHidePatch can leave
                // transiently stale (dangling) after auto-hiding the window.
                var window = ItemChecklistWindow.Instance;
                bool checklistOpen = window != null && window.Root.activeSelf;

                if (checklistOpen)
                {
                    // Close via the exact E/ESC path: CoreLib's postfix on
                    // HideAllInventoryAndCraftingUI hides our window AND clears
                    // UserInterfaceModule.currentInterface. forceClose:false
                    // mirrors PlayerController.CloseAnyOpenInventory(). A bare
                    // HideUI() here would leave currentInterface dangling with
                    // no Vanilla menu to cover it, freezing the player in menu
                    // state (isAnyInventoryShowing stuck true).
                    Debug.Log("[ItemChecklist] Hotkey — closing UI");
                    Manager.ui.HideAllInventoryAndCraftingUI(forceClose: false);
                }
                // Guard: not actually playing in a world (world-load screen or
                // exit-to-menu fade — the Iter-15 loading-screen bug class, shared
                // with the HUD via WorldState), a Vanilla menu (pause/title), the
                // inventory/crafting UI, a focused text field, or chat is active —
                // don't open over it. isPlayerInventoryShowing closes the gap:
                // IsAnyMenuActive() covers only the menu system, never the
                // inventory/crafting UI. (The intro spawn-from-Core cutscene is
                // gated inside WorldState.IsInPlayableWorld via cutsceneIsPlaying
                // — Iter-15.)
                else if (!WorldState.IsInPlayableWorld
                    || Manager.menu.IsAnyMenuActive()
                    || Manager.ui.isPlayerInventoryShowing
                    || Manager.input.textInputIsActive
                    || ReferenceEquals(Manager.input.activeInputField, Manager.ui.chatWindow))
                {
                    Debug.Log("[ItemChecklist] Hotkey ignored (loading screen / other menu/input active)");
                }
                else
                {
                    Debug.Log("[ItemChecklist] Hotkey — opening UI");
                    // Iter-20: refresh possession + Recompute BEFORE opening, so the
                    // list (counts + active possession filter) is already current when
                    // the window appears. Doing the Recompute AFTER OpenModUI rebinds the
                    // rows a second time and races the search field's focus init (the
                    // field then ignores keystrokes until another widget is clicked).
                    if (s_ledger != null)
                        Possession = PossessionScanner.Scan(
                            s_ledger, Pets, PossessionConfig.AnchorRadius,
                            _possessionPlayableTime > PossessionPruneGraceSeconds);
                    ListView?.Refresh();
                    UserInterfaceModule.OpenModUI("ItemChecklist:Window");
                }
            }
        }
    }
}
