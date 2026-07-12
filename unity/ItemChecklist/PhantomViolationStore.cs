using PugMod;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Iter-33 safety net (durable half). A cooked-food epic "phantom" (a row
    /// <see cref="ItemCatalog"/> suppressed as unreachable) that CK nonetheless discovers
    /// is a reachability-model violation. This persists each such violation via
    /// <c>API.ConfigFilesystem</c> (trusted; sandbox-safe — <c>System.IO</c> is banned), so
    /// it survives Player.log's per-launch rotation and a remote user can just send the file.
    ///
    /// <para>Global (not per-character): the reachability model is character-independent.
    /// Hand-rolled ASCII — no <c>Encoding</c>/<c>JsonUtility</c>, mirroring
    /// <see cref="Possession.PossessionStore"/>.</para>
    ///
    /// File: <c>mods/ItemChecklist/phantom-violations.txt</c>
    /// <list type="bullet">
    /// <item>Line 1: <c>#icl-phantom-violations v1</c></item>
    /// <item>Per violation: <c>objectId,variation,primaryIngredientId,secondaryIngredientId</c>
    /// — the decoded ingredients make the offending recipe self-explanatory.</item>
    /// </list>
    /// </summary>
    internal static class PhantomViolationStore
    {
        private const string Dir    = "ItemChecklist";
        private const string Path   = Dir + "/phantom-violations.txt";
        private const string Header = "#icl-phantom-violations v1";

        private static bool _loaded;
        private static readonly System.Collections.Generic.HashSet<long> _known =
            new System.Collections.Generic.HashSet<long>();

        /// <summary>
        /// Record a violating <c>(objectId, variation)</c> durably. Returns <c>true</c> iff it
        /// was newly recorded (not already on disk / seen this session) — so the caller logs
        /// exactly once per distinct violation. Cross-session dedup: the existing file is
        /// lazy-loaded once, so restarts neither duplicate lines nor re-log.
        /// </summary>
        public static bool Record(int objectId, int variation)
        {
            long key = DiscoveredState.PackKey(objectId, variation);
            try
            {
                if (!_loaded) { LoadKnown(); _loaded = true; }
                if (!_known.Add(key)) return false;

                int primary   = (int) CookedFoodCD.GetPrimaryIngredientFromVariation(variation);
                int secondary = (int) CookedFoodCD.GetSecondaryIngredientFromVariation(variation);
                string line = objectId + "," + variation + "," + primary + "," + secondary + "\n";

                if (!API.ConfigFilesystem.DirectoryExists(Dir)) API.ConfigFilesystem.CreateDirectory(Dir);
                string existing = ReadAll();
                string text = string.IsNullOrEmpty(existing) ? Header + "\n" + line : existing + line;
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++) bytes[i] = (byte) text[i];   // ASCII content only
                API.ConfigFilesystem.Write(Path, bytes);
                return true;
            }
            catch (System.Exception e)
            {
                // A failed write must not claim "newly recorded" — and must not leave `key`
                // marked in _known, or a retry this session would be silently suppressed
                // (the write self-heals on the next attempt; LoadKnown re-reads on restart).
                _known.Remove(key);
                Debug.LogWarning($"[ItemChecklist] phantom-violation persist failed: {e.Message}");
                return false;
            }
        }

        private static void LoadKnown()
        {
            try
            {
                string text = ReadAll();
                if (string.IsNullOrEmpty(text)) return;
                foreach (var raw in text.Split('\n'))
                {
                    var l = raw.Trim();
                    if (l.Length == 0 || l[0] == '#') continue;
                    var parts = l.Split(',');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int oid) && int.TryParse(parts[1], out int v))
                        _known.Add(DiscoveredState.PackKey(oid, v));
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[ItemChecklist] phantom-violation load failed: {e.Message}"); }
        }

        private static string ReadAll()
        {
            if (!API.ConfigFilesystem.FileExists(Path)) return null;
            var bytes = API.ConfigFilesystem.Read(Path);
            if (bytes == null) return null;
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) chars[i] = (char) bytes[i];
            return new string(chars);
        }
    }
}
