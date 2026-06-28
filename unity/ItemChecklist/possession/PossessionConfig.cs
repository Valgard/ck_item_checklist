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

        private static bool? _diagCached;

        /// <summary>When true, the possession scan logs per-scan timing + ledger size, the
        /// save logs serialize/write timing, and a one-time dump of the distinct counted
        /// placed objects (with tags + IsWorldNature verdict) is written to Player.log — to
        /// diagnose a recurring stutter or spot wild nature leaking past the blacklist in a
        /// new biome. Default false; read once at startup, so set it in the config and
        /// relaunch to toggle. Zero overhead when false.</summary>
        public static bool Diagnostics
        {
            get
            {
                if (_diagCached.HasValue) return _diagCached.Value;
                bool d = false;
                try
                {
                    API.Config.Register("ItemChecklist", "Possession",
                        "Log possession-scan perf + a one-time counted-object dump to Player.log (stutter diagnosis / world-nature blacklist completion).",
                        "diagnostics", false);
                    if (API.Config.TryGet("ItemChecklist", "Possession", "diagnostics", out bool v)) d = v;
                }
                catch { /* config unavailable — keep default */ }
                _diagCached = d;
                return d;
            }
        }
    }
}
