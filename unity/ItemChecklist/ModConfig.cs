using ModSettingsMenu.Settings;

namespace ItemChecklist
{
    /// <summary>The mod's configuration adapter (named ModConfig for cross-mod uniformity — every
    /// sibling mod exposes a root-namespace <c>ModConfig</c>). Holds the possession-tracking
    /// settings, driven by the Mod Settings Menu framework
    /// (registered + bound once in <see cref="ItemChecklistMod.Init"/> via <see cref="Bind"/>).
    /// Both are read live each scan/save; the pre-Bind defaults apply only in the brief window
    /// before Init runs. This replaces the former CoreLib <c>API.Config</c> surface — the values
    /// now persist through the framework's own <c>ConfigFile</c> (<c>mods/ItemChecklist/config.cfg</c>).
    ///
    /// <para><b>AnchorRadius</b> (tiles): how far from a workbench-anchored base storage counts as
    /// "yours". Decoupled from CK's ~10-15 tile craft range so large bases are covered.</para>
    ///
    /// <para><b>Diagnostics</b>: when true, the possession scan logs per-scan timing + ledger size,
    /// the save logs serialize/write timing, and a one-time dump of the distinct counted placed
    /// objects (with tags + IsWorldNature verdict) is written to Player.log — to diagnose a recurring
    /// stutter or spot wild nature leaking past the blacklist in a new biome. Default false; zero
    /// overhead when false. Read once per scan/save into a local, so toggling it in-menu takes effect
    /// on the next scan.</para></summary>
    internal static class ModConfig
    {
        private const float DefaultRadius = 48f;

        /// <summary>Iter-36: what the HUD + window-footer counter shows.</summary>
        public enum CounterMode { Discovery, Possession }

        private static SettingHandle<float> _radiusHandle;
        private static SettingHandle<bool> _diagHandle;
        private static SettingHandle<CounterMode> _counterModeHandle;

        public static void Bind(SettingHandle<float> radius, SettingHandle<bool> diagnostics,
            SettingHandle<CounterMode> counterMode)
        {
            _radiusHandle = radius;
            _diagHandle = diagnostics;
            _counterModeHandle = counterMode;
        }

        public static float AnchorRadius => _radiusHandle != null ? _radiusHandle.Value : DefaultRadius;

        public static bool Diagnostics => _diagHandle != null ? _diagHandle.Value : false;

        // Iter-36: named `Mode` (NOT `CounterMode`) — a nested type + a same-named member is CS0102.
        public static CounterMode Mode => _counterModeHandle != null ? _counterModeHandle.Value : CounterMode.Discovery;
    }
}
