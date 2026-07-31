// Iter-44: behavioural harness for the possession ledger. Run it with
//
//     dotnet run --project tests/possession-harness
//
// It prints one line per assertion and exits non-zero on any failure. It is NOT part of the mod
// build (see the .csproj for why it can exist at all) and NOT a gate — nothing runs it
// automatically. Run it after touching PossessionLedger.cs or PetCollection.cs, before the build.
//
// The rules it pins down are the ones whose in-game verification costs a base, a walk, a pen and a
// save cycle: what may shrink and when, the two-miss delay and its resets, the prune's premise, and
// the parse boundary's damage detection. Everything else about the mod (ECS, Harmony, UI, the
// sandbox) is still only testable in-game — see docs/conventions.md § Testing.
//
// If a real ledger file is present it is round-tripped too, which is the check that matters most:
// a false damage report would put a healthy character's store read-only. Point ICL_POSSESSION_DIR
// at a `mods/ItemChecklist` directory to use a different save; those checks are skipped when the
// directory is absent, so the harness stays green on a machine with no game install.
using System;
using System.Collections.Generic;
using System.IO;
using ItemChecklist.Possession;
using UnityEngine;

internal static class Harness
{
    private static int _pass,
        _fail;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok)
        {
            _pass++;
            Console.WriteLine("  PASS  " + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        }
        else
        {
            _fail++;
            Console.WriteLine("  FAIL  " + name + "  " + detail);
        }
    }

    // One anchor at tile (0,0); "covered" = within 8 tiles of it.
    private static bool CoveredNearOrigin(long key)
    {
        int x = PossessionLedger.KeyX(key),
            z = PossessionLedger.KeyZ(key);
        return x * x + z * z <= 64;
    }

    private static Dictionary<int, int> C(params int[] pairs)
    {
        var d = new Dictionary<int, int>();
        for (int i = 0; i < pairs.Length; i += 2)
            d[pairs[i]] = pairs[i + 1];
        return d;
    }

    private static Dictionary<long, int> A(params long[] pairs)
    {
        var d = new Dictionary<long, int>();
        for (int i = 0; i < pairs.Length; i += 2)
            d[pairs[i]] = (int)pairs[i + 1];
        return d;
    }

    // One scan: `contents`/`aux` are per-tile observations, `containers` the tiles where a
    // container entity was seen. Returns what was removed.
    private static TilePublishResult Scan(
        PossessionLedger led,
        Vector2 player,
        bool pastGrace,
        Dictionary<long, Dictionary<int, int>> contents = null,
        Dictionary<long, Dictionary<long, int>> aux = null,
        HashSet<long> containers = null,
        bool havePlayer = true,
        Dictionary<long, Dictionary<int, int>> placed = null
    ) =>
        led.ApplyScan(
            contents ?? new Dictionary<long, Dictionary<int, int>>(),
            placed ?? new Dictionary<long, Dictionary<int, int>>(),
            aux ?? new Dictionary<long, Dictionary<long, int>>(),
            containers ?? new HashSet<long>(),
            havePlayer,
            player,
            48f,
            CoveredNearOrigin,
            pastGrace,
            new HashSet<long>()
        );

    // A well-formed v4 file from data lines. The count line is MANDATORY under the v4 marker
    // (a file cut to its first line must not read as a clean empty ledger), so every hand-written
    // v4 fixture has to carry one — which is why this helper exists rather than string concatenation.
    private static string V4File(string body)
    {
        int n = 0;
        foreach (var l in body.Split('\n'))
            if (l.Trim().Length > 0)
                n++;
        return "#icl-ledger-v4\n#n=" + n + "\n" + body;
    }

    private static Dictionary<long, Dictionary<int, int>> Tile(long key, Dictionary<int, int> c) => new() { [key] = c };

    private static Dictionary<long, Dictionary<long, int>> TileA(long key, Dictionary<long, int> a) => new() { [key] = a };

    private static void Main()
    {
        const string M = "#icl-ledger-v4";
        const string V3 = "#icl-ledger-v3";
        long t00 = PossessionLedger.Key(0, 0);
        var at = new Vector2(0f, 0f); // player standing on the tile
        var far = new Vector2(95f, 0f); // the measured I4 distance
        var away = new Vector2(300f, 300f);

        // The real files are a bonus, not a requirement: without them the harness still checks
        // every rule below against synthetic data.
        string dir = Environment.GetEnvironmentVariable("ICL_POSSESSION_DIR");
        if (string.IsNullOrEmpty(dir))
            dir =
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                + "/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/"
                + "AppData/LocalLow/Pugstorm/Core Keeper/Steam/10510784/mods/ItemChecklist";
        Console.WriteLine("== real shipped files ==");
        string biggest = null;
        int biggestLen = -1;
        foreach (var f in Directory.Exists(dir) ? Directory.GetFiles(dir, "possession-*.txt") : new string[0])
        {
            string text = File.ReadAllText(f);
            bool supported = text.StartsWith(M) || text.StartsWith(V3);
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(text, out int skipped);
            if (supported)
            {
                Check("load " + Path.GetFileName(f), tiles > 0 && skipped == 0, "tiles=" + tiles + " skipped=" + skipped + " bytes=" + text.Length);
                // A v3 file MIGRATES, so its bytes legitimately change (v4 marker, count line,
                // fourth segment). What must hold is STABILITY: writing, re-reading and writing
                // again reproduces the same text, and no tile is lost on the way.
                string once = led.Serialize();
                var again = new PossessionLedger();
                int tiles2 = again.LoadFrom(once, out int skipped2);
                Check(
                    "write/read/write is stable and lossless",
                    again.Serialize() == once && tiles2 == tiles && skipped2 == 0,
                    "tiles=" + tiles + "->" + tiles2 + " skipped2=" + skipped2
                );
                Check("output is v4 with a declared count", once.StartsWith(M + "\n#n=" + tiles + "\n"), once.Split('\n')[1]);
            }
            else
            {
                Check("pre-v3 " + Path.GetFileName(f) + " is discarded, not damage", tiles == -1 && skipped == 0, "tiles=" + tiles);
            }
            if (supported && text.Length > biggestLen)
            {
                biggestLen = text.Length;
                biggest = f;
            }
        }

        string real;
        if (biggest != null)
            real = File.ReadAllText(biggest);
        else
        {
            // No game install here. Synthesize a file of the same SHAPE so the truncation and CRLF
            // checks below still mean something — they are about the parser, not about this save.
            Console.WriteLine("  SKIP  no real ledger found (set ICL_POSSESSION_DIR to include one); using a synthetic file");
            var synth = new List<string> { M };
            for (int i = 1; i <= 40; i++)
                synth.Add(i + "," + -i + "|" + (100 + i) + ":" + i + "|");
            real = string.Join("\n", synth);
        }
        var lines = real.Split('\n');
        Console.WriteLine("== damage detection (base file: " + lines.Length + " lines) ==");
        {
            string cut = real.Substring(0, real.Length - (lines[lines.Length - 1].Length / 2));
            var led = new PossessionLedger();
            led.LoadFrom(cut, out int skipped);
            Check("mid-line truncation IS detected", skipped > 0, "skipped=" + skipped);
        }
        {
            var keep = new List<string>();
            for (int i = 0; i < lines.Length / 2; i++)
                keep.Add(lines[i]);
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(string.Join("\n", keep), out int skipped);
            Check("ledger boundary truncation parses clean (KNOWN residual)", skipped == 0 && tiles == keep.Count - 1, "tiles=" + tiles);
        }
        {
            // CRLF: the shape that used to mark a HEALTHY player permanently read-only.
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(real.Replace("\n", "\r\n"), out int skipped);
            Check("CRLF file is NOT reported as damaged", skipped == 0 && tiles > 0, "tiles=" + tiles + " skipped=" + skipped);
        }
        {
            var led = new PossessionLedger();
            Check("v2 marker discards", led.LoadFrom("#icl-ledger-v2\n1,2|5610:10|", out int sk) == -1 && sk == 0);
            var led2 = new PossessionLedger();
            Check("v30 marker is NOT accepted as v3", led2.LoadFrom("#icl-ledger-v30\n1,2|5610:10|", out int _) == -1);
        }

        Console.WriteLine("== parse shapes ==");
        {
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(V4File("1,2|5610:10|\n3,4||123:1\n5,6|7:1|8:2"), out int skipped);
            Check("contents-only / aux-only / both parse clean", tiles == 3 && skipped == 0, "tiles=" + tiles + " skipped=" + skipped);
            Check("re-serialize keeps all three", led.Serialize().Split('\n').Length == 5, "marker + #n= + 3 tiles");
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("-7,-9|5610:3|"), out int sk);
            Check(
                "negative coords round-trip (still v3-shaped: unverified)",
                sk == 0 && led.Serialize() == M + "\n#n=1\n-7,-9|5610:3|",
                led.Serialize().Replace("\n", " / ")
            );
        }
        {
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(V4File("1,2|5610:0|"), out int sk);
            Check("id:0 rejected AND counted", sk == 1 && tiles == 0);
            var led2 = new PossessionLedger();
            led2.LoadFrom(V4File("1,2|5610:-5|"), out int sk2);
            Check("id:-5 rejected AND counted", sk2 == 1);
            var led3 = new PossessionLedger();
            led3.LoadFrom(V4File("1,2|5610:1,5610:2|"), out int sk3);
            Check("duplicate id within one line is damage", sk3 == 1);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("1,2|5610:1"), out int sk);
            Check("one '|' is damage", sk == 1);
            var led2 = new PossessionLedger();
            led2.LoadFrom(V4File("x,y|5610:1|"), out int sk2);
            Check("unparseable coords are damage", sk2 == 1);
            var led3 = new PossessionLedger();
            led3.LoadFrom(V4File("1,2||"), out int sk3);
            Check("an empty-but-well-formed line is damage", sk3 == 1);
        }
        {
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(V4File("1,2|5610:10|\n1,2||99:1"), out int sk);
            string s = led.Serialize();
            Check("duplicate tile line MERGES both dimensions", tiles == 1 && sk == 0 && s.Contains("5610:10") && s.Contains("99:1"), s.Replace("\n", " / "));
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("1,2|5610:1|\n"), out int sk);
            Check("trailing newline is not damage", sk == 0);
        }

        Console.WriteLine("== contents rules ==");
        {
            // Container observed => its buffer is authoritative => an absence lands at once.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            var r = Scan(led, at, true, Tile(t00, C(100, 2)), containers: new HashSet<long> { t00 });
            Check(
                "observed container: a lower count is applied at once",
                r.DroppedUnits == 3 && led.Serialize().Contains("100:2"),
                "dropped=" + r.DroppedUnits
            );
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            var r = Scan(led, at, true, Tile(t00, C(999, 1)), containers: new HashSet<long> { t00 });
            Check("observed container: an ABSENT id is dropped at once", r.DroppedUnits == 5 && !led.Serialize().Contains("100:"), "dropped=" + r.DroppedUnits);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            var r = Scan(led, at, false, Tile(t00, C(100, 2)), containers: new HashSet<long> { t00 });
            Check("the streaming grace blocks every removal", r.DroppedUnits == 0 && led.Serialize().Contains("100:5"));
        }
        {
            // The I4 case: seen from ~95 tiles, no container observed.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            var r = Scan(led, far, true, Tile(t00, C(110, 1)));
            Check("I4: far tile, no container -> contents kept", r.DroppedUnits == 0 && led.Serialize().Contains("100:5"));
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            var r = Scan(led, far, true, Tile(t00, C(100, 1)), containers: new HashSet<long> { t00 });
            Check("far tile WITH container observed -> shrinks (Iter-43 rule kept)", r.DroppedUnits == 4);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            Scan(led, away, true, Tile(t00, C(100, 9)));
            Check("a HIGHER observation wins without any permission", led.Serialize().Contains("100:9"));
        }

        Console.WriteLine("== 'one miss is not evidence' (the retired residual) ==");
        {
            // Co-located torch observed, chest missing, player at the tile: ONE scan must not cost
            // the chest its contents.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|110:1"), out int _);
            var r1 = Scan(led, at, true, Tile(t00, C(110, 1)));
            Check("miss #1: contents kept", r1.DroppedUnits == 0 && led.Serialize().Contains("100:5"), "dropped=" + r1.DroppedUnits);
            var r2 = Scan(led, at, true, Tile(t00, C(110, 1)));
            Check("miss #2: now it drops", r2.DroppedUnits == 5 && !led.Serialize().Contains("100:5"), "dropped=" + r2.DroppedUnits);
        }
        {
            // A confirmed observation in between must RESET the streak.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|110:1"), out int _);
            Scan(led, at, true, Tile(t00, C(110, 1))); // miss 1
            Scan(led, at, true, Tile(t00, C(100, 5, 110, 1)), containers: new HashSet<long> { t00 }); // confirmed
            var r = Scan(led, at, true, Tile(t00, C(110, 1))); // miss 1 again, not 2
            Check("a confirmed scan resets the miss streak", r.DroppedUnits == 0 && led.Serialize().Contains("100:5"));
        }
        {
            // Out of scope must also reset it: no information is not a miss.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|110:1"), out int _);
            Scan(led, at, true, Tile(t00, C(110, 1))); // miss 1
            Scan(led, away, true, Tile(t00, C(110, 1))); // out of scope
            var r = Scan(led, at, true, Tile(t00, C(110, 1)));
            Check("an out-of-scope scan resets the streak", r.DroppedUnits == 0 && led.Serialize().Contains("100:5"));
        }

        Console.WriteLine("== aux rules (the C-1 cases) ==");
        long red = 1300L << 32 | 2;
        {
            // C-1 A: the pen's LAST animal of a colour, player at the pen. Two scans, by design.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|8:1|") + red + ":1", out int _); // the station itself keeps the tile alive
            var r1 = Scan(led, at, true, Tile(t00, C(8, 1)));
            Check("C-1 A: miss #1 keeps the colour", r1.AuxKeysReduced == 0 && led.Serialize().Contains(red + ":1"));
            var r2 = Scan(led, at, true, Tile(t00, C(8, 1)));
            Check("C-1 A: miss #2 drops it", r2.AuxKeysReduced == 1 && !led.Serialize().Contains(red + ":1"), "reduced=" + r2.AuxKeysReduced);
        }
        {
            // C-1 B: 3 -> 1 of one colour is direct evidence and applies at once.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0||") + red + ":3", out int _);
            var r = Scan(led, at, true, aux: TileA(t00, A(red, 1)));
            Check("C-1 B: 3->1 applies immediately", r.AuxKeysReduced == 1 && led.Serialize().Contains(red + ":1"));
        }
        {
            // F15: a cow briefly outside AnchorRadius (or mid growth-churn) must not flicker.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|8:1|") + red + ":2", out int _);
            var r = Scan(led, at, true, Tile(t00, C(8, 1)));
            Check("F15: one unconfirmed scan does not touch the colour", r.AuxKeysReduced == 0 && led.Serialize().Contains(red + ":2"));
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0||") + red + ":3", out int _);
            var r = Scan(led, away, true);
            Check("away from base aux is KEPT (Iter-41)", r.AuxKeysReduced == 0 && led.Serialize().Contains(red + ":3"));
        }
        // (An earlier revision asserted the opposite here — that an observed container confirms its
        //  tile's aux absence at once. That was F3: the container's buffer is authoritative only for
        //  the pet-skin keys IT wrote, not for a pen's colours or a paint colour keyed to the same
        //  tile. The two-miss version of this case is in the section below.)

        Console.WriteLine("== empty entries / prune / ownership ==");
        {
            var led = new PossessionLedger();
            Scan(led, at, true, Tile(PossessionLedger.Key(4, 4), new Dictionary<int, int>()), TileA(PossessionLedger.Key(4, 4), new Dictionary<long, int>()));
            Check("a tile that produced nothing plants no entry", led.TileCount == 0);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:1|\n1,1|101:1|\n300,300|102:1|"), out int _);
            var live = Tile(PossessionLedger.Key(1, 1), C(101, 1));
            Scan(led, at, true, live); // stale miss #1 for (0,0)
            var r = Scan(led, at, true, live); // #2 → dropped
            string s = led.Serialize();
            Check(
                "prune drops stale in-range on the 2nd scan, keeps live + far",
                r.PrunedTiles == 1 && !s.Contains("100:1") && s.Contains("101:1") && s.Contains("102:1"),
                "pruned=" + r.PrunedTiles
            );
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:1|"), out int _);
            var r = Scan(led, at, false);
            Check("prune is a no-op during the grace", r.PrunedTiles == 0 && led.TileCount == 1);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:1|"), out int _);
            var r = Scan(led, at, true, havePlayer: false);
            Check("prune is a no-op with no player", r.PrunedTiles == 0 && led.TileCount == 1);
        }
        {
            // No anchor predicate: report and remove nothing, rather than silently disabling both
            // removal paths.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            var live = new HashSet<long>();
            var r = led.ApplyScan(
                Tile(t00, C(100, 1)),
                new Dictionary<long, Dictionary<int, int>>(),
                new Dictionary<long, Dictionary<long, int>>(),
                new HashSet<long> { t00 },
                true,
                at,
                48f,
                null,
                true,
                live
            );
            Check("a null anchor predicate removes nothing", r.DroppedUnits == 0 && r.PrunedTiles == 0 && led.Serialize().Contains("100:5"));
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|200:2|"), out int _);
            var observed = C(100, 1);
            Scan(led, away, true, Tile(t00, observed));
            Check("the caller's dict is neither mutated nor adopted", observed.Count == 1 && observed[100] == 1, "count=" + observed.Count);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|\n9,9|100:2|"), out int _);
            var live = new HashSet<long>();
            led.ApplyScan(
                Tile(t00, C(100, 5)),
                new Dictionary<long, Dictionary<int, int>>(),
                new Dictionary<long, Dictionary<long, int>>(),
                new HashSet<long> { t00 },
                true,
                at,
                48f,
                CoveredNearOrigin,
                true,
                live
            );
            led.SetCarried(C(100, 1));
            var view = led.BuildView(live);
            Check("BuildView sums carried + all tiles", view.Totals[100] == 8, "total=" + view.Totals[100]);
            Check("liveKeys is filled by ApplyScan", live.Contains(t00) && live.Count == 1, "live=" + live.Count);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|\n9,9|100:2|\n1,1|101:1|"), out int _);
            Check("reverse index", led.CountTilesHolding(100) == 2 && led.TilesHolding(100).Count == 2 && led.CountTilesHolding(999) == 0);
        }

        Console.WriteLine("== prune delay + miss adjacency + per-key independence ==");
        {
            // The single-chest tile: the chest misses one scan, so the tile is not observed at all
            // and only the PRUNE can reach it. It must survive the first miss.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            var r1 = Scan(led, at, true);
            Check("prune: miss #1 keeps the tile", r1.PrunedTiles == 0 && led.TileCount == 1, "pruned=" + r1.PrunedTiles);
            var r2 = Scan(led, at, true);
            Check("prune: miss #2 drops it", r2.PrunedTiles == 1 && led.TileCount == 0, "pruned=" + r2.PrunedTiles);
        }
        {
            // An observation between two stale scans must break the streak.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            Scan(led, at, true); // stale 1
            Scan(led, at, true, Tile(t00, C(100, 5)), containers: new HashSet<long> { t00 }); // observed
            var r = Scan(led, at, true); // stale 1 again
            Check("prune: an observation resets the stale streak", r.PrunedTiles == 0 && led.TileCount == 1);
        }
        {
            // Going out of scope between two stale scans must also break it: no information is not
            // evidence either.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|"), out int _);
            Scan(led, at, true); // stale 1
            Scan(led, away, true); // out of scope
            var r = Scan(led, at, true);
            Check("prune: an out-of-scope scan resets the streak", r.PrunedTiles == 0 && led.TileCount == 1);
        }
        {
            // Per-KEY misses: A misses, then A is back and B misses. B must NOT drop on its first
            // miss just because a neighbour missed before it.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5,200:7,300:1|"), out int _);
            var r1 = Scan(led, at, true, Tile(t00, C(200, 7, 300, 1))); // 100 absent
            Check("per-key: A's miss keeps A", r1.DroppedUnits == 0 && led.Serialize().Contains("100:5"));
            var r2 = Scan(led, at, true, Tile(t00, C(100, 5, 300, 1))); // 100 back, 200 absent
            Check("per-key: B does not spend A's grace", r2.DroppedUnits == 0 && led.Serialize().Contains("200:7"), "dropped=" + r2.DroppedUnits);
            var r3 = Scan(led, at, true, Tile(t00, C(100, 5, 300, 1))); // 200 absent again
            Check("per-key: B drops on ITS second miss", r3.DroppedUnits == 7 && !led.Serialize().Contains("200:7"), "dropped=" + r3.DroppedUnits);
        }
        {
            // Adjacency: a miss, a long gap out of scope, then one miss must not be "the second".
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:5|110:1"), out int _);
            Scan(led, at, true, Tile(t00, C(110, 1))); // miss 1
            for (int i = 0; i < 5; i++)
                Scan(led, away, true); // a trip away
            var r = Scan(led, at, true, Tile(t00, C(110, 1)));
            Check("adjacency: a stale miss does not survive a trip away", r.DroppedUnits == 0 && led.Serialize().Contains("100:5"));
        }
        {
            // F3: an observed CONTAINER must not confirm the absence of a DIFFERENT aux producer.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:1|") + red + ":1", out int _);
            var r = Scan(led, at, true, Tile(t00, C(100, 1)), containers: new HashSet<long> { t00 });
            Check("aux needs two misses even with a container observed", r.AuxKeysReduced == 0 && led.Serialize().Contains(red + ":1"));
            var r2 = Scan(led, at, true, Tile(t00, C(100, 1)), containers: new HashSet<long> { t00 });
            Check("...and drops on the second", r2.AuxKeysReduced == 1 && !led.Serialize().Contains(red + ":1"));
        }
        {
            // A zero-byte ledger file is damage, not an empty ledger.
            var led = new PossessionLedger();
            int tiles = led.LoadFrom("", out int sk);
            Check("empty ledger file is damage", tiles == 0 && sk == 1, "tiles=" + tiles + " skipped=" + sk);
        }

        Console.WriteLine("== Iter-45: provenance (stored vs placed) ==");
        {
            // The bug this exists for: a PLACED object must not read as "in a chest".
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|||110:1"), out int sk);
            Check("a placed-only tile parses", sk == 0 && led.TileCount == 1);
            Check("placed does NOT count as a CONTAINER tile", led.CountContainerTilesHolding(110) == 0);
            Check("but it IS locatable, so the arrow survives", led.CountTilesHolding(110) == 1 && led.TilesHolding(110).Count == 1);
            var view = led.BuildView(new HashSet<long>());
            Check("placed still counts as OWNED", view.Totals.TryGetValue(110, out var t) && t == 1, "total=" + t);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|110:2||110:1"), out int sk);
            Check("both provenances on one tile round-trip", sk == 0 && led.Serialize() == M + "\n#n=1\n0,0|110:2||110:1");
            Check("the reverse index counts the tile once", led.CountTilesHolding(110) == 1);
            var view = led.BuildView(new HashSet<long>());
            Check("totals sum both", view.Totals[110] == 3, "total=" + view.Totals[110]);
        }
        {
            // v3 MIGRATION: contents become stored (provenance assumed for one visit), and the
            // first observation of the tile replaces it with the real split.
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(V3 + "\n0,0|110:1|", out int sk);
            Check("a v3 file migrates instead of being discarded", tiles == 1 && sk == 0, "tiles=" + tiles + " skipped=" + sk);
            Check("v3 contents count as being in a container until observed", led.CountContainerTilesHolding(110) == 1);
            // NO `containers` argument here. An earlier revision passed one, which forces
            // `absenceIsConfirmed` — the single case where the correction looks atomic. Both
            // reviewers caught that the test was asserting a behaviour the shipped code did not
            // have. The migration must be exact on a PLACED-ONLY tile, which is the common shape.
            var r = Scan(led, at, true, placed: Tile(t00, C(110, 1)));
            var view = led.BuildView(new HashSet<long>());
            Check("migration does not double the count", view.Totals[110] == 1, "total=" + view.Totals[110]);
            Check("migration is not booked as a loss", r.DroppedUnits == 0 && r.ShrunkContentTiles == 0, "dropped=" + r.DroppedUnits);
            Check(
                "...and the tile is re-filed as PLACED",
                led.CountContainerTilesHolding(110) == 0 && led.Serialize().Contains("||110:1"),
                led.Serialize().Replace("\n", " / ")
            );
            Check("it stays trackable, so the arrow survives", led.CountTilesHolding(110) == 1);
        }
        {
            // A migrated tile that legitimately holds the id BOTH ways: v3 wrote the sum, so
            // subtracting the observed placed part must leave the chest's copy.
            var led = new PossessionLedger();
            led.LoadFrom(V3 + "\n0,0|110:3|", out int _);
            Scan(led, at, true, Tile(t00, C(110, 2)), placed: Tile(t00, C(110, 1)), containers: new HashSet<long> { t00 });
            var view = led.BuildView(new HashSet<long>());
            Check(
                "a mixed migrated tile splits exactly",
                view.Totals[110] == 3 && led.Serialize().Contains("110:2||110:1"),
                led.Serialize().Replace("\n", " / ")
            );
        }
        {
            // The permanent-doubling case the second reviewer found: observed from beyond
            // PruneRadius, so nothing may shrink. The subtraction is evidence-based rather than a
            // removal, so it must still apply.
            var led = new PossessionLedger();
            led.LoadFrom(V3 + "\n0,0|110:1|", out int _);
            for (int i = 0; i < 3; i++)
                Scan(led, new Vector2(60f, 0f), true, placed: Tile(t00, C(110, 1)));
            Check("no doubling when observed from beyond the shrink envelope", led.BuildView(new HashSet<long>()).Totals[110] == 1);
        }
        {
            // Provenance uncertainty must survive a save, or a tile not revisited before the first
            // save hardens into a split nobody verified.
            var led = new PossessionLedger();
            led.LoadFrom(V3 + "\n0,0|110:1|", out int _);
            string saved = led.Serialize();
            Check("an unobserved migrated tile is written v3-SHAPED", saved.EndsWith("0,0|110:1|"), saved.Replace("\n", " / "));
            var back = new PossessionLedger();
            back.LoadFrom(saved, out int sk);
            Scan(back, at, true, placed: Tile(t00, C(110, 1)));
            Check(
                "...so the correction still happens after a save",
                sk == 0 && back.CountContainerTilesHolding(110) == 0 && back.BuildView(new HashSet<long>()).Totals[110] == 1
            );
        }
        {
            // F3: under the v4 marker the count line is mandatory — a file cut to line 1 must not
            // load as a clean, writable, EMPTY ledger.
            var led = new PossessionLedger();
            int tiles = led.LoadFrom(M, out int sk);
            Check("v4 truncated to line 1 is damage", tiles == 0 && sk > 0, "tiles=" + tiles + " skipped=" + sk);
            var led2 = new PossessionLedger();
            led2.LoadFrom(V4File("#n=abc\n0,0|100:1||"), out int sk2);
            Check("a mangled count line is damage", sk2 > 0, "skipped=" + sk2);
            var led3 = new PossessionLedger();
            led3.LoadFrom(V3 + "\n0,0|100:1|", out int sk3);
            Check("a v3 file needs no count line", sk3 == 0);
        }
        {
            var led = new PossessionLedger();
            Check("a pre-v3 marker is still discarded", led.LoadFrom("#icl-ledger-v2\n1,2|5610:10|", out int _) == -1);
            var led2 = new PossessionLedger();
            Check("v40 is not accepted as v4", led2.LoadFrom("#icl-ledger-v40\n1,2|5610:10|", out int _) == -1);
        }
        {
            // The declared count: a clean cut between two lines is otherwise undetectable.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:1|\n1,1|101:1|\n2,2|102:1|"), out int _);
            string full = led.Serialize();
            var lines2 = full.Split('\n');
            var cut = new PossessionLedger();
            int sk2;
            cut.LoadFrom(string.Join("\n", lines2[0], lines2[1], lines2[2]), out sk2);
            Check("ledger boundary truncation IS detected via #n=", sk2 > 0, "skipped=" + sk2);
            var noCount = new PossessionLedger();
            noCount.LoadFrom(V4File("0,0|100:1|"), out int sk3);
            Check("a file without #n= is accepted unchecked", sk3 == 0);
        }
        {
            // Placed shrinks on the tile-scope premise only — a container's buffer says nothing
            // about the object standing beside it.
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|100:1||110:1"), out int _);
            var r = Scan(led, at, true, Tile(t00, C(100, 1)), containers: new HashSet<long> { t00 });
            Check("placed is NOT dropped on the first miss even with a container observed", r.DroppedUnits == 0 && led.Serialize().Contains("110:1"));
            var r2 = Scan(led, at, true, Tile(t00, C(100, 1)), containers: new HashSet<long> { t00 });
            Check("...and drops on the second", r2.DroppedUnits == 1 && !led.Serialize().Contains("110:1"), "dropped=" + r2.DroppedUnits);
        }
        {
            var led = new PossessionLedger();
            led.LoadFrom(V4File("0,0|||110:1"), out int _);
            var r = Scan(led, far, true, placed: Tile(t00, C(999, 1)));
            Check("placed is kept when the tile is out of scope", r.DroppedUnits == 0 && led.Serialize().Contains("110:1"));
        }

        Console.WriteLine("== pet collection (boundary truncation) ==");
        {
            var empty = new PetCollection();
            Check("empty petskins file is damage", empty.LoadFrom("") == 1);
            var legacy2 = new PetCollection();
            legacy2.LoadFrom("1200:0\n1201:1");
            Check("a headerless file is marked dirty so it gains the header", legacy2.Dirty);
        }
        {
            var col = new PetCollection();
            col.MarkCollected(1200, 0);
            col.MarkCollected(1200, 3);
            col.MarkCollected(1201, 1);
            string text = col.Serialize();
            var back = new PetCollection();
            int skipped = back.LoadFrom(text);
            Check("round-trip is clean", skipped == 0 && back.IsCollected(1200, 3) && back.IsCollected(1201, 1), "skipped=" + skipped);
            Check("header declares the count", text.StartsWith("#icl-petskins-v1 n=3"), text.Split('\n')[0]);

            // Cut exactly at a line boundary — undetectable without the declared count.
            var cutLines = text.Split('\n');
            var trunc = new PetCollection();
            int sk2 = trunc.LoadFrom(cutLines[0] + "\n" + cutLines[1]);
            Check("boundary truncation IS detected via n=", sk2 > 0, "skipped=" + sk2);

            // A pre-Iter-44 file (no header) still loads, unchecked.
            var legacy = new PetCollection();
            int sk3 = legacy.LoadFrom("1200:0\n1201:1");
            Check("headerless legacy file still loads", sk3 == 0 && legacy.IsCollected(1201, 1));

            var damaged = new PetCollection();
            Check("a malformed line is damage", damaged.LoadFrom("#icl-petskins-v1 n=1\nnonsense") > 0);
        }

        Console.WriteLine();
        Console.WriteLine((_fail == 0 ? "ALL GREEN" : "FAILURES PRESENT") + " -- pass=" + _pass + " fail=" + _fail);
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
