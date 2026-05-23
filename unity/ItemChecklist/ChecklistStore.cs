using System;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Persists ChecklistState to Unity PlayerPrefs, keyed by
    /// (playerId, worldId). Subscribes to <see cref="ChecklistState.StateChanged"/>
    /// and auto-saves on a debounced timer so the initial container scan
    /// (which can fire hundreds of SetOwned calls back-to-back) does not
    /// produce a flush per call.
    ///
    /// Per Spike #3 (docs/research/spike-3-player-save-api.md): PugMod has
    /// no writeable Player-Save API; IConfigFilesystem is read-only.
    /// PlayerPrefs is the sandbox-safe persistence backend.
    ///
    /// Encoding is hand-rolled (no <c>System.IO</c>) because Pugstorm's
    /// RoslynCSharp sandbox rejects any reference to the <c>System.IO</c>
    /// namespace — even in-memory <c>MemoryStream</c>/<c>BinaryWriter</c>
    /// usage causes the loader to mark the assembly as "failed code
    /// security verification" at mod-load time, even though the Editor
    /// build compiles cleanly.
    /// </summary>
    public sealed class ChecklistStore
    {
        private const ushort Magic = 0xCC11;
        private const byte CurrentVersion = 1;
        private const float DebounceSeconds = 1.0f;

        // Sanity cap on per-set count fields decoded from a stored blob.
        // A corrupt blob with count=0xFFFFFFFF would otherwise try to
        // allocate uint.MaxValue * 4 bytes and OOM/throw. The cap is well
        // above any realistic Core Keeper item count (Phase-1 ~1500).
        private const uint MaxDecodedIds = 1_000_000;

        private readonly ChecklistState state;
        private readonly string playerId;
        private readonly string worldId;
        private float lastDirtyTime;
        private bool dirty;

        public ChecklistStore(ChecklistState state, string playerId, string worldId)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.playerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
            this.worldId = worldId ?? throw new ArgumentNullException(nameof(worldId));

            state.StateChanged += OnStateChanged;
        }

        /// <summary>
        /// Composite PlayerPrefs key. Plain ASCII, slash- and space-free,
        /// safe for every platform's underlying preference store.
        /// </summary>
        private string Key => $"ItemChecklist.{playerId}.{worldId}";

        /// <summary>
        /// Read the persisted blob (if any) and apply it to ChecklistState
        /// via LoadFromSnapshot. Call once after constructing the store
        /// (before the initial container scan).
        /// </summary>
        public void Load()
        {
            string base64 = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(base64))
            {
                Debug.Log($"[ItemChecklist] Store: no prior state for {Key}");
                return;
            }

            byte[] blob;
            try
            {
                blob = Convert.FromBase64String(base64);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"[ItemChecklist] Store: corrupt base64 in {Key}, ignoring: {e.Message}");
                return;
            }

            if (!TryDecodeAndApply(blob))
                Debug.LogWarning($"[ItemChecklist] Store: blob in {Key} failed to decode, ignoring");
            else
                Debug.Log($"[ItemChecklist] Store: loaded {state.OwnedCount} owned, {state.DiscoveredCount} discovered from {Key}");
        }

        /// <summary>
        /// Force an immediate save if there are pending changes. Call on
        /// world-end / Shutdown to make sure the last batch of edits
        /// reaches disk before the process exits.
        /// </summary>
        public void FlushIfDirty()
        {
            if (!dirty) return;
            byte[] blob = Encode();
            PlayerPrefs.SetString(Key, Convert.ToBase64String(blob));
            PlayerPrefs.Save();
            dirty = false;
            Debug.Log($"[ItemChecklist] Store: flushed {blob.Length} bytes to {Key}");
        }

        /// <summary>
        /// Call from the IMod Update loop. Triggers a debounced flush once
        /// no state mutations have arrived for DebounceSeconds seconds.
        /// </summary>
        public void Tick(float currentTime)
        {
            if (dirty && currentTime - lastDirtyTime >= DebounceSeconds)
                FlushIfDirty();
        }

        private void OnStateChanged()
        {
            dirty = true;
            lastDirtyTime = Time.unscaledTime;
        }

        // ===== Hand-rolled little-endian byte packing (no System.IO) =====
        //
        // Blob layout (spec §"Persistence format"):
        //   u16 magic = 0xCC11
        //   u8  version = 1
        //   u8  reserved
        //   u32 ownedN
        //   i32[ownedN]
        //   u32 discN
        //   i32[discN]

        private byte[] Encode()
        {
            int[] ownedIds = state.SnapshotOwned();
            int[] discoveredIds = state.SnapshotDiscovered();

            // 4-byte header + 4-byte count + 4*N ids, twice.
            int size = 4 + 4 + 4 * ownedIds.Length + 4 + 4 * discoveredIds.Length;
            byte[] buf = new byte[size];
            int p = 0;

            WriteU16LE(buf, ref p, Magic);
            buf[p++] = CurrentVersion;
            buf[p++] = 0;                                   // reserved

            WriteU32LE(buf, ref p, (uint) ownedIds.Length);
            for (int i = 0; i < ownedIds.Length; i++) WriteI32LE(buf, ref p, ownedIds[i]);

            WriteU32LE(buf, ref p, (uint) discoveredIds.Length);
            for (int i = 0; i < discoveredIds.Length; i++) WriteI32LE(buf, ref p, discoveredIds[i]);

            return buf;
        }

        private bool TryDecodeAndApply(byte[] blob)
        {
            if (blob == null || blob.Length < 4) return false;
            int p = 0;

            ushort magic = ReadU16LE(blob, ref p);
            if (magic != Magic)
            {
                Debug.LogWarning($"[ItemChecklist] Store: magic mismatch 0x{magic:X4} (expected 0x{Magic:X4})");
                return false;
            }

            byte version = blob[p++];
            p++; // reserved
            if (version != CurrentVersion)
            {
                Debug.LogWarning($"[ItemChecklist] Store: unknown version {version}");
                return false;
            }

            if (!TryReadIdArray(blob, ref p, "ownedN", out int[] ownedIds)) return false;
            if (!TryReadIdArray(blob, ref p, "discN", out int[] discIds)) return false;

            state.LoadFromSnapshot(ownedIds, discIds);
            return true;
        }

        private static bool TryReadIdArray(byte[] blob, ref int p, string fieldName, out int[] ids)
        {
            ids = null;
            if (blob.Length - p < 4)
            {
                Debug.LogWarning($"[ItemChecklist] Store: truncated blob before {fieldName}");
                return false;
            }
            uint n = ReadU32LE(blob, ref p);
            if (n > MaxDecodedIds)
            {
                Debug.LogWarning($"[ItemChecklist] Store: {fieldName}={n} exceeds sanity cap {MaxDecodedIds}");
                return false;
            }
            if (blob.Length - p < (long) n * 4)
            {
                Debug.LogWarning($"[ItemChecklist] Store: truncated blob in {fieldName} body ({n} ids declared)");
                return false;
            }
            ids = new int[n];
            for (int i = 0; i < n; i++) ids[i] = ReadI32LE(blob, ref p);
            return true;
        }

        private static void WriteU16LE(byte[] buf, ref int p, ushort v)
        {
            buf[p++] = (byte) (v & 0xFF);
            buf[p++] = (byte) ((v >> 8) & 0xFF);
        }

        private static void WriteU32LE(byte[] buf, ref int p, uint v)
        {
            buf[p++] = (byte) (v & 0xFF);
            buf[p++] = (byte) ((v >> 8) & 0xFF);
            buf[p++] = (byte) ((v >> 16) & 0xFF);
            buf[p++] = (byte) ((v >> 24) & 0xFF);
        }

        private static void WriteI32LE(byte[] buf, ref int p, int v) => WriteU32LE(buf, ref p, (uint) v);

        private static ushort ReadU16LE(byte[] buf, ref int p)
        {
            ushort v = (ushort) (buf[p] | (buf[p + 1] << 8));
            p += 2;
            return v;
        }

        private static uint ReadU32LE(byte[] buf, ref int p)
        {
            uint v = (uint) (buf[p] | (buf[p + 1] << 8) | (buf[p + 2] << 16) | (buf[p + 3] << 24));
            p += 4;
            return v;
        }

        private static int ReadI32LE(byte[] buf, ref int p) => (int) ReadU32LE(buf, ref p);
    }
}
