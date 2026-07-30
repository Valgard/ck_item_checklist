using PugMod;
using UnityEngine;

namespace ItemChecklist.Possession
{
    /// <summary>Iter-43: how a store's load ended. The distinction is load-bearing, not
    /// informational: <see cref="NoFile"/> and <see cref="Failed"/> both yield an EMPTY store,
    /// but only the first one legitimately means "nothing owned yet". Treating a failure as
    /// NoFile lets the next save write that empty store over an intact file — the data-loss
    /// path Iter-43 closes (see <c>ItemChecklistMod.s_ledgerReadOnly</c>).</summary>
    internal enum StoreLoadStatus
    {
        /// <summary>No file on disk (a new character) — an empty store is correct.</summary>
        NoFile,

        /// <summary>Read and parsed. The store may still legitimately be empty.</summary>
        Loaded,

        /// <summary>The file exists but could not be read or parsed. The in-memory store is
        /// empty or partial and MUST NOT be written back over the file.</summary>
        Failed,
    }

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
        private static readonly System.Collections.Generic.Dictionary<string, ulong> _lastSavedHash = new System.Collections.Generic.Dictionary<
            string,
            ulong
        >();

        private static ulong Fnv1a64(string s)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL; // FNV-1a 64 offset basis
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 1099511628211UL;
                } // ^ char, * FNV prime (wraps)
                return h;
            }
        }

        private static string PathFor(string guid) => Dir + "/possession-" + guid + ".txt";

        public static void Save(string guid, PossessionLedger ledger)
        {
            if (string.IsNullOrEmpty(guid) || ledger == null)
                return;
            try
            {
                bool diag = ModConfig.Diagnostics;
                float t0 = diag ? UnityEngine.Time.realtimeSinceStartup : 0f;
                string text = ledger.Serialize();
                float t1 = diag ? UnityEngine.Time.realtimeSinceStartup : 0f;
                ulong hash = Fnv1a64(text);

                // Unchanged since the last write → no disk I/O at all (also skips the
                // DirectoryExists probe). The first save per character always lands: the cache
                // is per-session, so a fresh launch has no entry for this guid and cannot
                // hash-match. Note what that means — the first save of a session ALWAYS
                // overwrites the file, whatever the in-memory ledger currently holds. That is
                // precisely why a FAILED load must never reach here: an empty ledger can never
                // hash-match a populated file, so the write would land and destroy it. The guard
                // lives at the call site (`ItemChecklistMod.s_ledgerReadOnly`, Iter-43), not in
                // the cache, because the cache is a perf device and cannot know load status.
                if (_lastSavedHash.TryGetValue(guid, out var prev) && prev == hash)
                {
                    if (diag)
                        Debug.Log(
                            $"[ItemChecklist] DIAG save SKIPPED unchanged serialize={(t1 - t0) * 1000f:F1}ms "
                                + $"bytes={text.Length} containers={ledger.Containers.Count}"
                        );
                    return;
                }

                if (!API.ConfigFilesystem.DirectoryExists(Dir))
                    API.ConfigFilesystem.CreateDirectory(Dir);
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++)
                    bytes[i] = (byte)text[i]; // ASCII content only
                API.ConfigFilesystem.Write(PathFor(guid), bytes);
                _lastSavedHash[guid] = hash;
                if (diag)
                    Debug.Log(
                        $"[ItemChecklist] DIAG save serialize={(t1 - t0) * 1000f:F1}ms "
                            + $"write={(UnityEngine.Time.realtimeSinceStartup - t1) * 1000f:F1}ms bytes={text.Length} containers={ledger.Containers.Count}"
                    );
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] possession save failed: {e.Message}");
            }
        }

        /// <summary>Read the ledger for <paramref name="guid"/>. <paramref name="status"/> tells
        /// the caller whether an empty result means "new character" or "could not read" — see
        /// <see cref="StoreLoadStatus"/>. Iter-43: previously every outcome returned a bare empty
        /// ledger indistinguishably, so a transient read fault (squarely in scope under Wine,
        /// where this project already ships six IL patches for lying/failing file APIs) let the
        /// next autosave persist an empty ledger over an intact one.</summary>
        public static PossessionLedger Load(string guid, out StoreLoadStatus status)
        {
            status = StoreLoadStatus.NoFile;
            var ledger = new PossessionLedger();
            if (string.IsNullOrEmpty(guid))
                return ledger;
            string path = PathFor(guid);
            try
            {
                if (!API.ConfigFilesystem.FileExists(path))
                    return ledger; // genuinely new character
                var bytes = API.ConfigFilesystem.Read(path);
                if (bytes == null)
                {
                    // NOT "no file": it exists but yielded nothing. Was silent before.
                    status = StoreLoadStatus.Failed;
                    PossessionIncidentStore.Record(
                        PossessionIncidentStore.LoadFailed,
                        PossessionIncidentStore.LoadFailed + ":ledger:" + guid,
                        "ledger guid=" + guid + " reason=read-returned-null",
                        $"could not read the possession ledger for {guid} (read returned null). Your owned "
                            + "counts are rebuilt from your containers at base; the file will NOT be overwritten."
                    );
                    return ledger;
                }
                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    chars[i] = (char)bytes[i];
                ledger.LoadFrom(new string(chars));
                status = StoreLoadStatus.Loaded;
            }
            catch (System.Exception e)
            {
                // Set Failed FIRST — LoadFrom clears both dicts before parsing, so a mid-parse
                // throw leaves a PARTIAL ledger that looks exactly like a complete one.
                status = StoreLoadStatus.Failed;
                Debug.LogWarning($"[ItemChecklist] possession load failed: {e.Message}");
                PossessionIncidentStore.Record(
                    PossessionIncidentStore.LoadFailed,
                    PossessionIncidentStore.LoadFailed + ":ledger:" + guid,
                    "ledger guid=" + guid + " reason=exception msg=" + e.Message,
                    $"could not read the possession ledger for {guid} ({e.Message}). Your owned counts are "
                        + "rebuilt from your containers at base; the file will NOT be overwritten."
                );
            }
            return ledger;
        }
    }
}
