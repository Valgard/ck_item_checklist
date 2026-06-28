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
                bool diag = PossessionConfig.Diagnostics;
                float t0 = diag ? UnityEngine.Time.realtimeSinceStartup : 0f;
                string text = ledger.Serialize();
                float t1 = diag ? UnityEngine.Time.realtimeSinceStartup : 0f;
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++) bytes[i] = (byte)text[i];   // ASCII content only
                API.ConfigFilesystem.Write(PathFor(guid), bytes);
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
