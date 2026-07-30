using PugMod;
using UnityEngine;

namespace ItemChecklist.Possession
{
    /// <summary>
    /// Iter-43: durable, NOT-diagnostics-gated reporting for events that can cost the player
    /// remembered possession data. Modelled on <see cref="PhantomViolationStore"/> — same
    /// reason, one tier more serious: <c>Player.log</c> rotates on every launch, so a
    /// <c>Debug.LogWarning</c> is worthless to someone reporting "my owned counts collapsed"
    /// days later. This persists each incident via <c>API.ConfigFilesystem</c> (trusted;
    /// <c>System.IO</c> is banned) so the file can simply be sent.
    ///
    /// <para><strong>Why ungated:</strong> <see cref="ModConfig.Diagnostics"/> defaults to OFF,
    /// and a data-loss event that only reports when someone already suspected a problem and
    /// turned diagnostics on reports nothing when it matters. Iter-42 is the precedent — a
    /// month of silent deletion that needed an on-disk <c>.pugbackup</c> diff to see at all.
    /// The cost is bounded: writes happen only when something actually went wrong.</para>
    ///
    /// <para><strong>Deduped by caller-supplied key</strong> so a structural fault (a failed
    /// load for a character) records once, not once per autosave. Cross-session: the existing
    /// file is lazy-loaded once. Capped at <see cref="MaxLines"/> so a pathological loop cannot
    /// grow the file without bound (the Iter-28 lesson — an unbounded persisted structure
    /// becomes a main-thread serialize spike); once full, further incidents still log but are
    /// not written.</para>
    ///
    /// <para>Timestamps are <c>Time.realtimeSinceStartup</c> (session-relative seconds), NOT
    /// <c>System.DateTime</c> — wall-clock BCL surface is unproven against the Roslyn sandbox
    /// and is not worth a whole-mod compile failure for a log field. Session-relative is
    /// enough to order incidents within a launch, which is what matters here.</para>
    ///
    /// File: <c>mods/ItemChecklist/possession-incidents.txt</c>
    /// <list type="bullet">
    /// <item>Line 1: <c>#icl-possession-incidents v1</c></item>
    /// <item>Per incident: <c>&lt;kind&gt;|t=&lt;sessionSeconds&gt;|&lt;detail&gt;</c></item>
    /// </list>
    /// </summary>
    internal static class PossessionIncidentStore
    {
        private const string Dir = "ItemChecklist";
        private const string Path = Dir + "/possession-incidents.txt";
        private const string Header = "#icl-possession-incidents v1";
        private const int MaxLines = 200;

        // Incident kinds. Strings, not an enum: they are written to a file a human reads.
        public const string LoadFailed = "load-failed";
        public const string LedgerDiscarded = "ledger-discarded";
        public const string Shrink = "shrink";

        private static bool _loaded;
        private static int _lines;
        private static readonly System.Collections.Generic.HashSet<string> _known = new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// Record an incident durably and warn once. <paramref name="dedupKey"/> decides the
        /// granularity — pass something stable per fault (e.g. <c>kind + guid</c> for a failed
        /// load, <c>kind + tile</c> for a shrink) so a recurring symptom does not spam.
        /// Returns <c>true</c> iff newly recorded, so a caller can gate extra work on it.
        /// </summary>
        public static bool Record(string kind, string dedupKey, string detail, string humanMessage)
        {
            try
            {
                if (!_loaded)
                {
                    LoadKnown();
                    _loaded = true;
                }
                if (!_known.Add(dedupKey))
                    return false;

                // Always warn, even when the file is full or the write fails below — the log is
                // the fallback channel, and losing the line entirely is the worst outcome.
                Debug.LogWarning($"[ItemChecklist] possession incident [{kind}]: {humanMessage}");

                if (_lines >= MaxLines)
                    return true;

                string line = kind + "|t=" + Time.realtimeSinceStartup.ToString("F1") + "|" + Sanitize(detail) + "\n";
                if (!API.ConfigFilesystem.DirectoryExists(Dir))
                    API.ConfigFilesystem.CreateDirectory(Dir);
                string existing = ReadAll();
                string text = string.IsNullOrEmpty(existing) ? Header + "\n" + line : existing + line;
                var bytes = new byte[text.Length];
                for (int i = 0; i < text.Length; i++)
                    bytes[i] = (byte)text[i]; // ASCII content only
                API.ConfigFilesystem.Write(Path, bytes);
                _lines++;
                return true;
            }
            catch (System.Exception e)
            {
                // Un-mark so a retry this session is not silently suppressed (mirrors
                // PhantomViolationStore). The warning above already went out.
                _known.Remove(dedupKey);
                Debug.LogWarning($"[ItemChecklist] possession-incident persist failed: {e.Message}");
                return false;
            }
        }

        // Keep the record one line and its two field separators unambiguous.
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            var chars = new char[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\n' || c == '\r' || c == '|')
                    chars[i] = ' '; // would break the line / field split
                else if (c < 32 || c > 126)
                    chars[i] = '?'; // the file is hand-rolled ASCII
                else
                    chars[i] = c;
            }
            return new string(chars);
        }

        private static void LoadKnown()
        {
            try
            {
                string text = ReadAll();
                if (string.IsNullOrEmpty(text))
                    return;
                foreach (var raw in text.Split('\n'))
                {
                    var l = raw.Trim();
                    if (l.Length == 0 || l[0] == '#')
                        continue;
                    _lines++;
                    // The dedup key is not stored (details carry timestamps); a restart may
                    // therefore re-record a still-present structural fault ONCE, which is
                    // wanted — it dates the incident to the new session. The line cap is what
                    // bounds growth.
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] possession-incident load failed: {e.Message}");
            }
        }

        private static string ReadAll()
        {
            if (!API.ConfigFilesystem.FileExists(Path))
                return null;
            var bytes = API.ConfigFilesystem.Read(Path);
            if (bytes == null)
                return null;
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
                chars[i] = (char)bytes[i];
            return new string(chars);
        }
    }
}
