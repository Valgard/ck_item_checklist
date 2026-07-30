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
                            $"[ItemChecklist] DIAG save SKIPPED unchanged serialize={(t1 - t0) * 1000f:F1}ms " + $"bytes={text.Length} tiles={ledger.TileCount}"
                        );
                    return;
                }

                if (!API.ConfigFilesystem.DirectoryExists(Dir))
                    API.ConfigFilesystem.CreateDirectory(Dir);
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++)
                    bytes[i] = (byte)text[i]; // ASCII content only
                API.ConfigFilesystem.Write(PathFor(guid), bytes);

                // Iter-44: VERIFY by reading back, because "Write did not throw" does not mean
                // "the write landed". CK's StandaloneFilesystem.Write ends in
                // `catch (IOException) { Debug.LogError(...) }` with NO rethrow (verified in the
                // decompile), and its inner File.Replace/File.Move retry loop gives up after ten
                // attempts with only a LogError. So the entire IOException class — disk full,
                // sharing violation, and the Wine file-API faults this project ships six IL patches
                // for — is invisible to the caller. Two consequences, and the second is the worse
                // one: the failure went unreported, AND the hash below cached "the disk holds this"
                // for content that was never written, so every later save with unchanged content
                // hash-matched and was SKIPPED. One poisoned cache entry could suppress saving for
                // the rest of the session. The hash is therefore only cached after a verified
                // read-back, which costs one extra read on the writes that actually happen (the
                // skip still elides the rest).
                if (!Verify(PathFor(guid), hash))
                {
                    WriteFailed = true;
                    _lastSavedHash.Remove(guid); // never let an unverified write suppress the next one
                    Debug.LogWarning("[ItemChecklist] possession save did not land (write reported no error but the file does not match).");
                    PossessionIncidentStore.Record(
                        PossessionIncidentStore.SaveFailed,
                        PossessionIncidentStore.SaveFailed + ":ledger:" + guid,
                        "ledger guid=" + guid + " reason=verify-failed bytes=" + text.Length,
                        $"the possession ledger for {guid} could not be written — the game's file layer reported no error, "
                            + "but reading the file back does not match what was saved (a full disk or a locked file will do "
                            + "this). Owned counts changed this session will be missing after a restart."
                    );
                    return;
                }
                _lastSavedHash[guid] = hash;
                WriteFailed = false;
                if (diag)
                    Debug.Log(
                        $"[ItemChecklist] DIAG save serialize={(t1 - t0) * 1000f:F1}ms "
                            + $"write={(UnityEngine.Time.realtimeSinceStartup - t1) * 1000f:F1}ms bytes={text.Length} tiles={ledger.TileCount}"
                    );
            }
            catch (System.Exception e)
            {
                // Iter-44: a failed WRITE is the mirror image of Iter-43's failed load, and it was
                // still a bare log line — no status, no durable record, and the footer's
                // not-saving marker stayed off. The player then sees this session's
                // changes gone on the next launch: indistinguishable from the data loss the whole
                // read-only mechanism exists to prevent. Player.log rotates, so it must be durable.
                WriteFailed = true;
                Debug.LogWarning($"[ItemChecklist] possession save failed: {e.Message}");
                PossessionIncidentStore.Record(
                    PossessionIncidentStore.SaveFailed,
                    PossessionIncidentStore.SaveFailed + ":ledger:" + guid,
                    "ledger guid=" + guid + " reason=exception msg=" + e.Message,
                    $"could not WRITE the possession ledger for {guid} ({e.Message}). Owned counts changed this session "
                        + "will be missing after a restart. The file on disk still holds the last successful save."
                );
            }
        }

        /// <summary>Iter-44: the last write attempt did not land. Distinct from a failed LOAD (which
        /// makes the store read-only on purpose): here the mod is trying to save and cannot, so the
        /// data at risk is this session's, not the file's. Reset per character in
        /// <see cref="Load"/>, so one character's fault cannot mark another as not-saving.</summary>
        internal static bool WriteFailed { get; private set; }

        /// <summary>Read a just-written file back and compare content hashes. Any failure — a read
        /// that throws, a short file, different bytes — counts as "did not land", because the point
        /// is to distrust a silent success (see <see cref="Save"/>).</summary>
        private static bool Verify(string path, ulong expected)
        {
            try
            {
                var bytes = API.ConfigFilesystem.Read(path);
                if (bytes == null)
                    return false;
                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    chars[i] = (char)bytes[i];
                return Fnv1a64(new string(chars)) == expected;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] possession save verify failed: {e.Message}");
                return false;
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
            // Per character: a write fault on the previous character must not leave this one
            // showing "not saving" for a session it could not clear (the clear path runs only on a
            // successful write, and a character with nothing to save never reaches it).
            WriteFailed = false;
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
                string text = new string(chars);
                int tiles = ledger.LoadFrom(text, out int skipped);
                // A discard is NOT a failure: it is the intended version migration, and the store
                // must stay writable so the base can repopulate it. But it is also what a corrupt
                // file looks like, so report it — that is the whole point (Iter-43). The first
                // line goes into the record: `#icl-ledger-v2` reads as a migration, anything else
                // as damage.
                if (tiles < 0)
                {
                    int nl = text.IndexOf('\n');
                    string firstLine = (nl < 0 ? text : text.Substring(0, nl)).Trim();
                    PossessionIncidentStore.Record(
                        PossessionIncidentStore.LedgerDiscarded,
                        PossessionIncidentStore.LedgerDiscarded + ":" + guid + ":" + firstLine,
                        "ledger guid=" + guid + " bytes=" + bytes.Length + " firstLine=" + firstLine,
                        $"the possession ledger for {guid} was DISCARDED — expected '{PossessionLedger.VersionMarker}', got "
                            + $"'{firstLine}' ({bytes.Length} bytes). Owned counts rebuild from your containers on the "
                            + "next base visit. If this was not a mod update, the file was corrupt."
                    );
                }
                else if (skipped > 0)
                {
                    // Iter-44 (review C-3): the parser cannot THROW on damaged input — it skips
                    // what it cannot read — so a file truncated mid-write parsed into a SUBSET
                    // and the Iter-43 status flag reported `Loaded`. The store then stayed
                    // writable and the next autosave persisted the subset over the intact file;
                    // the one after that took the `.pugbackup` too. Treat any unaccepted data
                    // line as damage: read-only for this character, and the base repopulates the
                    // in-memory ledger without ever writing the partial state back.
                    status = StoreLoadStatus.Failed;
                    PossessionIncidentStore.Record(
                        PossessionIncidentStore.LoadFailed,
                        PossessionIncidentStore.LoadFailed + ":ledger:" + guid,
                        "ledger guid=" + guid + " reason=damaged bytes=" + bytes.Length + " tiles=" + tiles + " skippedLines=" + skipped + " readOnly=yes",
                        $"the possession ledger for {guid} is DAMAGED — {skipped} line(s) could not be read, so only "
                            + $"{tiles} of its tiles loaded. It will NOT be overwritten; owned counts rebuild from your "
                            + "containers at base. Keep a copy of the file if you want it looked at."
                    );
                    return ledger;
                }
                status = StoreLoadStatus.Loaded;
            }
            catch (System.Exception e)
            {
                // Set Failed FIRST — LoadFrom clears `_tiles` before parsing, so a mid-parse
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
