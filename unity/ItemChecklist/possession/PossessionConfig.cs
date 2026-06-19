using PugMod;

namespace ItemChecklist.Possession
{
    /// <summary>Tunable anchor radius (tiles) for which storage counts as "yours".
    /// Read once from API.Config (trusted, sandbox-safe). Decoupled from CK's ~10-15
    /// tile craft range so large bases are covered.</summary>
    internal static class PossessionConfig
    {
        private const float Default = 48f;
        private static float? _cached;

        public static float AnchorRadius
        {
            get
            {
                if (_cached.HasValue) return _cached.Value;
                float r = Default;
                try
                {
                    API.Config.Register("ItemChecklist", "Possession",
                        "Radius in tiles around a crafting station within which storage counts as owned.",
                        "anchorRadius", Default);
                    if (API.Config.TryGet("ItemChecklist", "Possession", "anchorRadius", out float v) && v > 0f)
                        r = v;
                }
                catch { /* config unavailable — keep default */ }
                _cached = r;
                return r;
            }
        }
    }
}
