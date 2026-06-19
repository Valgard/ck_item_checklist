using PugMod;
using UnityEngine;

namespace ItemChecklist.Possession
{
    /// <summary>Persists a ledger per character GUID via API.ConfigFilesystem
    /// (trusted; sandbox-safe). Hand-rolled ASCII bytes — no Encoding/JsonUtility.</summary>
    internal static class PossessionStore
    {
        private const string Dir = "ItemChecklist";

        private static string PathFor(string guid) => Dir + "/possession-" + guid + ".txt";

        public static void Save(string guid, PossessionLedger ledger)
        {
            if (string.IsNullOrEmpty(guid) || ledger == null) return;
            try
            {
                if (!API.ConfigFilesystem.DirectoryExists(Dir)) API.ConfigFilesystem.CreateDirectory(Dir);
                string text = ledger.Serialize();
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++) bytes[i] = (byte)text[i];   // ASCII content only
                API.ConfigFilesystem.Write(PathFor(guid), bytes);
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
