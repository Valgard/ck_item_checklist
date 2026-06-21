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
            if (string.IsNullOrEmpty(guid) || col == null) return;
            try
            {
                if (!API.ConfigFilesystem.DirectoryExists(Dir)) API.ConfigFilesystem.CreateDirectory(Dir);
                string text = col.Serialize();
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++) bytes[i] = (byte)text[i];   // ASCII content only
                API.ConfigFilesystem.Write(PathFor(guid), bytes);
                col.ClearDirty();
            }
            catch (System.Exception e) { Debug.LogWarning($"[ItemChecklist] pet-skin save failed: {e.Message}"); }
        }

        public static PetCollection Load(string guid)
        {
            var col = new PetCollection();
            if (string.IsNullOrEmpty(guid)) return col;
            try
            {
                string path = PathFor(guid);
                if (!API.ConfigFilesystem.FileExists(path)) return col;
                var bytes = API.ConfigFilesystem.Read(path);
                if (bytes == null) return col;
                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++) chars[i] = (char)bytes[i];
                col.LoadFrom(new string(chars));
            }
            catch (System.Exception e) { Debug.LogWarning($"[ItemChecklist] pet-skin load failed: {e.Message}"); }
            return col;
        }
    }
}
