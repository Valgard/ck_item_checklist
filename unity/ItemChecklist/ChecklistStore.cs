using System;
using System.IO;
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
    /// </summary>
    public sealed class ChecklistStore
    {
        private const ushort Magic = 0xCC11;
        private const byte CurrentVersion = 1;
        private const float DebounceSeconds = 1.0f;

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

        private byte[] Encode()
        {
            int[] ownedIds = state.SnapshotOwned();
            int[] discoveredIds = state.SnapshotDiscovered();

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic);                       // u16
                w.Write(CurrentVersion);              // u8
                w.Write((byte) 0);                    // u8 reserved
                w.Write((uint) ownedIds.Length);
                foreach (var id in ownedIds) w.Write(id);
                w.Write((uint) discoveredIds.Length);
                foreach (var id in discoveredIds) w.Write(id);
                return ms.ToArray();
            }
        }

        private bool TryDecodeAndApply(byte[] blob)
        {
            if (blob == null || blob.Length < 4) return false;

            try
            {
                using (var ms = new MemoryStream(blob))
                using (var r = new BinaryReader(ms))
                {
                    ushort magic = r.ReadUInt16();
                    if (magic != Magic)
                    {
                        Debug.LogWarning($"[ItemChecklist] Store: magic mismatch 0x{magic:X4} (expected 0x{Magic:X4})");
                        return false;
                    }

                    byte version = r.ReadByte();
                    r.ReadByte(); // reserved
                    if (version != CurrentVersion)
                    {
                        Debug.LogWarning($"[ItemChecklist] Store: unknown version {version}");
                        return false;
                    }

                    uint ownedN = r.ReadUInt32();
                    var ownedIds = new int[ownedN];
                    for (int i = 0; i < ownedN; i++) ownedIds[i] = r.ReadInt32();

                    uint discN = r.ReadUInt32();
                    var discIds = new int[discN];
                    for (int i = 0; i < discN; i++) discIds[i] = r.ReadInt32();

                    state.LoadFromSnapshot(ownedIds, discIds);
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }
    }
}
