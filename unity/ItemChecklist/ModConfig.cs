using ModSettingsMenu.Settings;

namespace ItemChecklist
{
    /// <summary>The mod's configuration adapter (named ModConfig for cross-mod uniformity — every
    /// sibling mod exposes a root-namespace <c>ModConfig</c>). Holds the possession-tracking
    /// settings, driven by the Mod Settings Menu framework
    /// (registered + bound once in <see cref="ItemChecklistMod.Init"/> via <see cref="Bind"/>).
    /// They are read live each scan/save; the pre-Bind defaults apply only in the brief window
    /// before Init runs. This replaces the former CoreLib <c>API.Config</c> surface — the values
    /// now persist through the framework's own <c>ConfigFile</c> (<c>mods/ItemChecklist/config.cfg</c>).
    ///
    /// <para><b>AnchorRadius</b> (tiles): how far from a workbench-anchored base storage counts as
    /// "yours". Decoupled from CK's ~10-15 tile craft range so large bases are covered.</para>
    ///
    /// <para><b>ScanIntervalSeconds</b> (seconds): how often the possession scan runs — the player
    /// trades update freshness against per-scan overhead. A discrete Choice of int presets (1..30 s,
    /// default 3 = the old hardcoded cadence). Read live at each timer reset in
    /// <see cref="ItemChecklistMod.Update"/>, so an in-menu change applies from the next cycle.</para>
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
        private const int DefaultScanInterval = 3;

        /// <summary>Iter-36: what the HUD + window-footer counter shows.</summary>
        public enum CounterMode { Discovery, Possession }

        private static SettingHandle<bool> _enabledHandle;
        private static SettingHandle<float> _radiusHandle;
        private static SettingHandle<bool> _diagHandle;
        private static SettingHandle<CounterMode> _counterModeHandle;
        private static SettingHandle<int> _scanIntervalHandle;

        public static void Bind(SettingHandle<bool> enabled, SettingHandle<float> radius,
            SettingHandle<bool> diagnostics, SettingHandle<CounterMode> counterMode,
            SettingHandle<int> scanInterval)
        {
            _enabledHandle = enabled;
            _radiusHandle = radius;
            _diagHandle = diagnostics;
            _counterModeHandle = counterMode;
            _scanIntervalHandle = scanInterval;
        }

        /// <summary>Master switch (default true). When false the mod is fully inert:
        /// no possession scan, the window won't open, the HUD is hidden.</summary>
        public static bool Enabled => _enabledHandle != null ? _enabledHandle.Value : true;

        public static float AnchorRadius => _radiusHandle != null ? _radiusHandle.Value : DefaultRadius;

        public static bool Diagnostics => _diagHandle != null ? _diagHandle.Value : false;

        // Iter-36: named `Mode` (NOT `CounterMode`) — a nested type + a same-named member is CS0102.
        public static CounterMode Mode => _counterModeHandle != null ? _counterModeHandle.Value : CounterMode.Discovery;

        // Iter-38: possession-scan cadence (seconds), a discrete Choice of int presets. Returns a
        // float so Update reads it straight into the timer; falls back to the default pre-Bind.
        public static float ScanIntervalSeconds => _scanIntervalHandle != null ? _scanIntervalHandle.Value : DefaultScanInterval;
    }
}
