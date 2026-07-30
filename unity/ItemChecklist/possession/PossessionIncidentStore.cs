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
    /// becomes a main-thread serialize spike). Iter-44: reaching the cap writes ONE
    /// <c>#full</c> marker line and then stops writing — silently degrading back to the
    /// per-launch log is exactly how Iter-42 stayed invisible for a month — and
    /// <see cref="Record"/> then returns <c>false</c>, where Iter-43 returned <c>true</c>
    /// while persisting nothing.</para>
    ///
    /// <para><strong>Iter-44: a present-but-unreadable file is never overwritten.</strong>
    /// The Iter-43 version read the file with a helper that returned <c>null</c> for BOTH
    /// "absent" and "read failed" — the very conflation <see cref="StoreLoadStatus"/> was
    /// introduced to end, one file deeper — and then wrote <c>Header + line</c>, replacing
    /// every accumulated incident with a single line. The trigger was CORRELATED with the
    /// fault being reported: a `ConfigFilesystem` fault produces an incident, whose write then
    /// destroys the evidence of all the earlier ones. <see cref="TryReadAll"/> now separates
    /// the two, and a failed read aborts the write (keeping the log warning, and leaving the
    /// dedup key unmarked so a later incident may still try).</para>
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

        /// <summary>Iter-44: the self-heal prune removing implausibly much. Iter-43 watched only
        /// the shrink path — while the largest ledger collapse ever MEASURED in this subsystem
        /// (Iter-41's <c>ledgerC</c> 402→0 as the player walked away) came through this one, where
        /// nothing but a default-off DIAG line existed.</summary>
        public const string Prune = "prune";

        /// <summary>Iter-44: a save was skipped because the store is read-only after a failed
        /// load. Reported once, because the symptom the player sees (a dropping counter, an
        /// uncollected pet skin) looks exactly like the data loss the read-only mode PREVENTED.</summary>
        public const string SaveSkipped = "save-skipped";

        private static bool _loaded;
        private static bool _capNoted;
        private static int _lines;
        private static readonly System.Collections.Generic.HashSet<string> _known = new System.Collections.Generic.HashSet<string>();

        /// <summary>Dedup granularity for a recurring QUANTITATIVE symptom: bucket by order of
        /// magnitude, so one benign small event cannot consume the slot a large one needs.
        /// Iter-43 keyed the shrink report on a flat <c>":session"</c>, so the first
        /// five-tile reorganisation of a session silenced a later four-hundred-tile collapse
        /// — the exact event the channel exists for.</summary>
        public static string MagnitudeBucket(int n)
        {
            if (n >= 200)
                return "200+";
            if (n >= 50)
                return "50+";
            if (n >= 10)
                return "10+";
            return "5+";
        }

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
                {
                    // Full. Say so IN the file (one '#' line, so LoadKnown skips it and it does
                    // not count toward the cap), then stop writing — and report that nothing was
                    // persisted, which the Iter-43 `return true` here did not.
                    if (!_capNoted)
                    {
                        _capNoted = true;
                        AppendLine("#full after " + MaxLines + " incidents - further ones reach Player.log only\n");
                    }
                    return false;
                }

                string line = kind + "|t=" + Time.realtimeSinceStartup.ToString("F1") + "|" + Sanitize(detail) + "\n";
                if (!AppendLine(line))
                {
                    // Could not persist. Un-mark so a later incident may retry this session; the
                    // warning above already went out, so the report is not lost, only undurable.
                    _known.Remove(dedupKey);
                    return false;
                }
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

        /// <summary>Append one already-newline-terminated line, preserving everything already in
        /// the file. Returns <c>false</c> when the file is present but unreadable — in that case
        /// nothing is written, because rewriting it from scratch would destroy the incident
        /// history at exactly the moment a filesystem fault is being reported.</summary>
        private static bool AppendLine(string line)
        {
            if (!API.ConfigFilesystem.DirectoryExists(Dir))
                API.ConfigFilesystem.CreateDirectory(Dir);
            if (!TryReadAll(out string existing))
            {
                Debug.LogWarning(
                    "[ItemChecklist] possession-incidents.txt exists but could not be read — NOT overwriting it. "
                        + "The incident above is in this log only (and Player.log rotates on the next launch)."
                );
                return false;
            }
            string text = string.IsNullOrEmpty(existing) ? Header + "\n" + line : existing + line;
            var bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
                bytes[i] = (byte)text[i]; // ASCII content only
            API.ConfigFilesystem.Write(Path, bytes);
            return true;
        }

        private static void LoadKnown()
        {
            try
            {
                if (!TryReadAll(out string text))
                {
                    // Present but unreadable. Not fatal here (the dedup set is only an anti-spam
                    // device), but it must be audible: it means this session cannot append either.
                    Debug.LogWarning("[ItemChecklist] possession-incident history could not be read — new incidents will not be appended.");
                    return;
                }
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

        /// <summary>Read the file, distinguishing "absent" from "unreadable" — the distinction
        /// the Iter-43 helper collapsed into a single <c>null</c>.</summary>
        /// <returns><c>true</c> with <paramref name="text"/> = the contents, or <c>null</c> when
        /// the file does not exist yet (a fresh write is then correct). <c>false</c> means the
        /// file IS there but could not be read — callers must not write.</returns>
        private static bool TryReadAll(out string text)
        {
            text = null;
            if (!API.ConfigFilesystem.FileExists(Path))
                return true; // genuinely absent
            var bytes = API.ConfigFilesystem.Read(Path);
            if (bytes == null)
                return false; // present, unreadable
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
                chars[i] = (char)bytes[i];
            text = new string(chars);
            return true;
        }
    }
}
