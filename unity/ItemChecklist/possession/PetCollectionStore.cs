using PugMod;
using UnityEngine;

namespace ItemChecklist.Possession
{
    /// <summary>Persists a PetCollection per character GUID via API.ConfigFilesystem
    /// (trusted; sandbox-safe). Hand-rolled ASCII bytes — no Encoding/JsonUtility.
    /// Mirrors PossessionStore; distinct file (petskins-&lt;guid&gt;.txt).</summary>
    internal static class PetCollectionStore
    {
        private const string Dir = "ItemChecklist";

        private static string PathFor(string guid) => Dir + "/petskins-" + guid + ".txt";

        public static void Save(string guid, PetCollection col)
        {
            if (string.IsNullOrEmpty(guid) || col == null)
                return;
            try
            {
                if (!API.ConfigFilesystem.DirectoryExists(Dir))
                    API.ConfigFilesystem.CreateDirectory(Dir);
                string text = col.Serialize();
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++)
                    bytes[i] = (byte)text[i]; // ASCII content only
                API.ConfigFilesystem.Write(PathFor(guid), bytes);
                col.ClearDirty();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] pet-skin save failed: {e.Message}");
            }
        }

        /// <summary>Read the collection for <paramref name="guid"/>, reporting how it ended.
        /// <para><strong>Iter-43 — this is the most damaging instance of the failed-load pattern
        /// in the mod, hence fixed first.</strong> The possession ledger self-rebuilds from the
        /// player's containers on the next base visit, so overwriting it with an empty one costs
        /// a walk home. This collection cannot: it is an EVER-OWNED set and CK tracks no per-skin
        /// discovery, so it has no second source. Writing an empty set over it means the only
        /// record that a skin was ever owned is gone, and re-earning it needs another randomly
        /// rolled egg hatch. A failed load must therefore never reach <see cref="Save"/>.</para>
        /// </summary>
        public static PetCollection Load(string guid, out StoreLoadStatus status)
        {
            status = StoreLoadStatus.NoFile;
            var col = new PetCollection();
            if (string.IsNullOrEmpty(guid))
                return col;
            string path = PathFor(guid);
            try
            {
                if (!API.ConfigFilesystem.FileExists(path))
                    return col; // genuinely new character
                var bytes = API.ConfigFilesystem.Read(path);
                if (bytes == null)
                {
                    status = StoreLoadStatus.Failed;
                    PossessionIncidentStore.Record(
                        PossessionIncidentStore.LoadFailed,
                        PossessionIncidentStore.LoadFailed + ":petskins:" + guid,
                        "petskins guid=" + guid + " reason=read-returned-null",
                        $"could not read the pet-skin collection for {guid} (read returned null). It will NOT "
                            + "be overwritten — collected skins are safe on disk, but newly collected ones "
                            + "cannot be saved this session."
                    );
                    return col;
                }
                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    chars[i] = (char)bytes[i];
                col.LoadFrom(new string(chars));
                status = StoreLoadStatus.Loaded;
            }
            catch (System.Exception e)
            {
                status = StoreLoadStatus.Failed;
                Debug.LogWarning($"[ItemChecklist] pet-skin load failed: {e.Message}");
                PossessionIncidentStore.Record(
                    PossessionIncidentStore.LoadFailed,
                    PossessionIncidentStore.LoadFailed + ":petskins:" + guid,
                    "petskins guid=" + guid + " reason=exception msg=" + e.Message,
                    $"could not read the pet-skin collection for {guid} ({e.Message}). It will NOT be "
                        + "overwritten — collected skins are safe on disk, but newly collected ones cannot "
                        + "be saved this session."
                );
            }
            return col;
        }
    }
}
