using System.Collections.Generic;
using PugMod;
using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Iter-22: one shared, hidden SlotUIBase whose only job is to turn an
    /// arbitrary (objectID, ckVariation) into CK-native tooltip data (title /
    /// description / stats). Code-instantiated (see ItemChecklistContent), so
    /// Awake is overridden to skip SlotUIBase's animator init — a bare instance
    /// has no serialized Animator and base.Awake would NRE on `animator.enabled`.
    /// The four hover virtuals are computed by SlotUIBase from GetSlotObject().
    /// </summary>
    public sealed class TooltipSlot : SlotUIBase
    {
        private ContainedObjectsBuffer _obj;

        public void SetObject(ObjectID id, int variation)
        {
            _obj = new ContainedObjectsBuffer
            {
                objectData = new ObjectDataCD { objectID = id, variation = variation, amount = 1 }
            };
        }

        // SlotUIBase computes title/desc/stats from its slot object.
        protected override ContainedObjectsBuffer GetSlotObject() => _obj;

        // Public passthrough — GetSlotObject is protected; the row needs the buffer
        // for its GetContainedObject() override.
        public ContainedObjectsBuffer GetSlotObjectPublic() => GetSlotObject();

        // Skip base.Awake() — it does `animator.enabled = ...` and a bare instance
        // has no serialized Animator. This helper never animates.
        protected override void Awake() { }

        // Public delegators so the row (and the Task-1 spike) can pull each part.
        public TextAndFormatFields TitleFor() => GetHoverTitle();
        public List<TextAndFormatFields> DescriptionFor() => GetHoverDescription();
        public List<TextAndFormatFields> StatsFor() => GetHoverStats(false);
    }
}
