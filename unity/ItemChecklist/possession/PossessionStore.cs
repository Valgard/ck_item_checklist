using PugMod;
using UnityEngine;

namespace ItemChecklist.Possession
{
    /// <summary>Persists a ledger per character GUID via API.ConfigFilesystem
    /// (trusted; sandbox-safe). Hand-rolled ASCII bytes — no Encoding/JsonUtility.</summary>
    internal static class PossessionStore
    {
        private const string Dir = "ItemChecklist";

        // 64-bit content hash of the text last written to disk, per character GUID.
        // The WriteCharacter hook fires on EVERY autosave, but base storage rarely
        // changes between two autosaves — so we elide the disk write (5–13ms of Wine
        // I/O, the real cost) when the freshly serialized ledger hashes to the same
        // value. Serialize() (~1ms) stays the cheap change-signal; we keep only the
        // hash, not a duplicate of the ~9KB text. FNV-1a/64 (not 32-bit GetHashCode):
        // a hash collision means a needed save is skipped = data loss, and 1/2^64 per
        // save is negligible where 1/2^32 would not be.
        private static readonly System.Collections.Generic.Dictionary<string, ulong> _lastSavedHash =
            new System.Collections.Generic.Dictionary<string, ulong>();

        private static ulong Fnv1a64(string s)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;                                      // FNV-1a 64 offset basis
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 1099511628211UL; } // ^ char, * FNV prime (wraps)
                return h;
            }
        }

        private static string PathFor(string guid) => Dir + "/possession-" + guid + ".txt";

        public static void Save(string guid, PossessionLedger ledger)
        {
            if (string.IsNullOrEmpty(guid) || ledger == null) return;
            try
            {
                bool diag = PossessionConfig.Diagnostics;
                float t0 = diag ? UnityEngine.Time.realtimeSinceStartup : 0f;
                string text = ledger.Serialize();
                float t1 = diag ? UnityEngine.Time.realtimeSinceStartup : 0f;
                ulong hash = Fnv1a64(text);

                // Unchanged since the last write → no disk I/O at all (also skips the
                // DirectoryExists probe). The first save per character always lands
                // (no _lastSavedHash entry), persisting the Iter-28 cleaned ledger.
                if (_lastSavedHash.TryGetValue(guid, out var prev) && prev == hash)
                {
                    if (diag)
                        Debug.Log($"[ItemChecklist] DIAG save SKIPPED unchanged serialize={(t1 - t0) * 1000f:F1}ms " +
                            $"bytes={text.Length} containers={ledger.Containers.Count}");
                    return;
                }

                if (!API.ConfigFilesystem.DirectoryExists(Dir)) API.ConfigFilesystem.CreateDirectory(Dir);
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++) bytes[i] = (byte)text[i];   // ASCII content only
                API.ConfigFilesystem.Write(PathFor(guid), bytes);
                _lastSavedHash[guid] = hash;
                if (diag)
                    Debug.Log($"[ItemChecklist] DIAG save serialize={(t1 - t0) * 1000f:F1}ms " +
                        $"write={(UnityEngine.Time.realtimeSinceStartup - t1) * 1000f:F1}ms bytes={text.Length} containers={ledger.Containers.Count}");
            }
            catch (System.Exception e) { Debug.LogWarning($"[ItemChecklist] possession save failed: {e.Message}"); }
        }

        public static PossessionLedger Load(string guid)
        {
            var ledger = new PossessionLedger();
            if (string.IsNullOrEmpty(guid)) return ledger;
            try
            {
                string path = PathFor(guid);
                if (!API.ConfigFilesystem.FileExists(path)) return ledger;
                var bytes = API.ConfigFilesystem.Read(path);
                if (bytes == null) return ledger;
                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++) chars[i] = (char)bytes[i];
                ledger.LoadFrom(new string(chars));
            }
            catch (System.Exception e) { Debug.LogWarning($"[ItemChecklist] possession load failed: {e.Message}"); }
            return ledger;
        }
    }
}
