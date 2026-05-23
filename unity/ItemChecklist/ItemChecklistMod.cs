using CoreLib;
using CoreLib.Submodule.ControlMapping;
using PugMod;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Mod bootstrap. The Pugstorm mod loader instantiates this on game
    /// start and calls the IMod lifecycle methods. Later tasks fill
    /// Init() with subsystem startup and Update() with the hotkey poll
    /// and the InventoryPoller tick.
    /// </summary>
    public sealed class ItemChecklistMod : IMod
    {
        public void EarlyInit()
        {
            Debug.Log("[ItemChecklist] EarlyInit");
            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));
        }

        public void Init()
        {
            Debug.Log("[ItemChecklist] Init");
        }

        public void ModObjectLoaded(Object obj) { }

        public void Shutdown() { }

        public void Update() { }
    }
}
