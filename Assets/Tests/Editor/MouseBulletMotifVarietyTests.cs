using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regression guards for rhythm-first barrage variety. This is a deterministic
/// geometry check; Play Mode and human mouse-play testing are still required.
/// </summary>
public sealed class MouseBulletMotifVarietyTests
{
    private static readonly string[] NewMotifs =
    {
        "mix_arc", "mix_cross", "mix_gate", "mix_sweep",
        "mix_echo", "mix_comet", "mix_ribbon"
    };

    private static Type PatternType
    {
        get
        {
            Type value = Type.GetType(
                "MouseModeBulletPatternLibrary, Assembly-CSharp");
            Assert.IsNotNull(value, "Shared bullet pattern library must compile.");
            return value;
        }
    }

    [Test]
    public void EveryNewMotifHasReadableDensityAndFullWidthCoverage()
    {
        MethodInfo generate = PatternType.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(generate);

        foreach (string id in NewMotifs)
        {
            IEnumerable shots = generate.Invoke(null,
                new object[] { id }) as IEnumerable;
            Assert.IsNotNull(shots, "Missing generated barrage: " + id);
            int count = 0;
            float left = float.MaxValue;
            float right = float.MinValue;
            foreach (object shot in shots)
            {
                Type type = shot.GetType();
                Vector2 origin = (Vector2)type.GetField("origin").GetValue(shot);
                float atBeat = (float)type.GetField("atBeat").GetValue(shot);
                float life = (float)type.GetField("lifeBeats").GetValue(shot);
                left = Mathf.Min(left, origin.x);
                right = Mathf.Max(right, origin.x);
                Assert.That(atBeat, Is.InRange(0f, 5.5f),
                    id + " should not continue firing into the next phrase.");
                Assert.That(life, Is.InRange(.5f, 12f),
                    id + " has an unreadable lifetime.");
                count++;
            }
            Assert.That(count, Is.InRange(9, 16),
                id + " must not use quantity as its only source of difficulty.");
            Assert.Greater(right - left, .36f,
                id + " piles bullets into one narrow corridor.");
        }
    }

    [Test]
    public void NewMotifsLeaveEscapeSpaceAtTheLowerRiskLine()
    {
        MethodInfo generate = PatternType.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo position = PatternType.GetMethod("Position",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(position);
        foreach (string id in NewMotifs)
        {
            IEnumerable source = generate.Invoke(null, new object[] { id })
                as IEnumerable;
            var shots = new List<object>();
            foreach (object item in source) shots.Add(item);

            // 900 x 450 is an analytical stage, not the actual Unity layout.
            // Check every quarter-beat that at least six horizontal slots
            // remain unblocked. This does not prove a moving dodge route.
            for (float now = 0f; now <= 14f; now += .25f)
            {
                var centers = new List<Vector2>();
                foreach (object shot in shots)
                {
                    float start = (float)shot.GetType().GetField("atBeat")
                        .GetValue(shot);
                    float life = (float)shot.GetType().GetField("lifeBeats")
                        .GetValue(shot);
                    if (now < start || now > start + life) continue;
                    centers.Add((Vector2)position.Invoke(null,
                        new object[] { shot, now - start, 900f, 450f }));
                }

                int safeSlots = 0;
                for (int x = -400; x <= 400; x += 40)
                {
                    Vector2 pino = new Vector2(x, -100f);
                    bool blocked = false;
                    foreach (Vector2 bullet in centers)
                        if (Vector2.Distance(pino, bullet) < 24f)
                        {
                            blocked = true;
                            break;
                        }
                    if (!blocked) safeSlots++;
                }
                Assert.GreaterOrEqual(safeSlots, 6,
                    id + " has no readable open space at beat " + now);
            }
        }
    }

    [TestCase("pop_twinfan", 12)]
    [TestCase("pop_echo", 18)]
    [TestCase("pop_pendulum", 18)]
    [TestCase("pop_ribbon", 16)]
    [TestCase("pop_gate", 20)]
    [TestCase("pop_pinwheel", 24)]
    [TestCase("pop_aim", 15)]
    [TestCase("pop_pulse", 20)]
    [TestCase("pop_seedflower", 15)]
    public void PopSignatureMotifsRemainDistinctWithoutBulletSpam(
        string pattern, int expectedCount)
    {
        MethodInfo generate = PatternType.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        IEnumerable pellets = generate.Invoke(null,
            new object[] { pattern }) as IEnumerable;
        Assert.IsNotNull(pellets);
        int count = 0;
        foreach (object pellet in pellets) count++;
        Assert.AreEqual(expectedCount, count,
            pattern + " should remain expressive without long crowded sprays.");
    }

    [Test]
    public void EncodedMotifSpreadAndDensitySurviveAttackParsing()
    {
        Type director = Type.GetType(
            "MouseModeBulletDirector, Assembly-CSharp");
        Assert.IsNotNull(director);
        MethodInfo parse = director.GetMethod("ParseAttack",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo encode = director.GetMethod("EncodeAttack",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(parse);
        Assert.IsNotNull(encode);
        string variant = "mix_arc|spread=1.25|density=0.70";
        object attack = parse.Invoke(null, new object[] { variant });
        string stored = (string)attack.GetType().GetField("legacyId")
            .GetValue(attack);
        Assert.AreEqual(variant, stored,
            "A chart's authored attack variation must reach game runtime.");
        Assert.AreEqual(variant,
            encode.Invoke(null, new[] { attack }),
            "Chart editor roundtrip must preserve artist-authored variations.");

        MethodInfo generate = PatternType.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        IEnumerable baseShots = generate.Invoke(null,
            new object[] { "mix_arc" }) as IEnumerable;
        IEnumerable variantShots = generate.Invoke(null,
            new object[] { variant }) as IEnumerable;
        int baseline = 0;
        int reduced = 0;
        float outerBase = 0f;
        float outerVariant = 0f;
        foreach (object bullet in baseShots)
        {
            baseline++;
            float x = ((Vector2)bullet.GetType().GetField("origin")
                .GetValue(bullet)).x;
            outerBase = Mathf.Max(outerBase, Mathf.Abs(x - .5f));
        }
        foreach (object bullet in variantShots)
        {
            reduced++;
            float x = ((Vector2)bullet.GetType().GetField("origin")
                .GetValue(bullet)).x;
            outerVariant = Mathf.Max(outerVariant, Mathf.Abs(x - .5f));
        }
        Assert.Less(reduced, baseline,
            "Encoded density did not reduce crowded shots.");
        Assert.Greater(outerVariant, outerBase,
            "Encoded spread did not widen the firing area.");
    }

    [Test]
    public void FullBandHitHasEightDistinctRhythmFirstBulletMotifs()
    {
        string[] ids = {
            "fh_beat", "fh_curtain", "fh_cross", "fh_gate",
            "fh_sweep", "fh_orbit", "fh_comet", "fh_finale"
        };
        MethodInfo generate = PatternType.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo position = PatternType.GetMethod("Position",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(generate);
        Assert.IsNotNull(position);
        foreach (string id in ids)
        {
            IEnumerable source = generate.Invoke(null, new object[] { id }) as IEnumerable;
            Assert.IsNotNull(source, "Missing Full-Band Hit attack: " + id);
            var shots = new List<object>();
            foreach (object shot in source) shots.Add(shot);
            Assert.That(shots.Count, Is.InRange(6, 16), id + " bullet count");

            // A Pino standing in one place should still have several viable
            // x positions on the lower danger line as the volleys pass.
            // Static gaps alone do NOT establish a viable continuous path.
            for (float now = 0f; now <= 15f; now += .25f)
            {
                int safeSlots = 0;
                for (int x = -400; x <= 400; x += 40)
                {
                    bool blocked = false;
                    foreach (object pellet in shots)
                    {
                        Type pelletType = pellet.GetType();
                        float start = (float)pelletType.GetField("atBeat").GetValue(pellet);
                        float life = (float)pelletType.GetField("lifeBeats").GetValue(pellet);
                        if (now < start || now > start + life) continue;
                        Vector2 point = (Vector2)position.Invoke(null,
                            new object[] { pellet, now - start, 900f, 450f });
                        if (Vector2.Distance(new Vector2(x, -99f), point) < 27f)
                        {
                            blocked = true;
                            break;
                        }
                    }
                    if (!blocked) safeSlots++;
                }
                Assert.GreaterOrEqual(safeSlots, 5,
                    id + " closes the whole escape line at beat " + now);
            }
        }
    }

    [Test]
    public void FullBandHitPatternsHaveContinuousReachableEscapeRoutes()
    {
        // The actual Pino moves with Mouse X delta, at capped speed. This
        // conservative horizontal slice models safe consecutive positions,
        // not just an unconnected snapshot with empty lanes.
        string[] ids = {
            "fh_beat", "fh_curtain", "fh_cross", "fh_gate",
            "fh_sweep", "fh_orbit", "fh_comet", "fh_finale",
            "fh_gate|mirror=1"
        };
        MethodInfo generate = PatternType.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo position = PatternType.GetMethod("Position",
            BindingFlags.Public | BindingFlags.Static);
        foreach (string id in ids)
        {
            IEnumerable source = generate.Invoke(null, new object[] { id }) as IEnumerable;
            var shots = new List<object>();
            foreach (object pellet in source) shots.Add(pellet);
            // 41 x positions, 20px apart on a 900x450 analytical stage.
            // One 20px move per quarter-beat is below the 900 UI px/s Pino
            // cap scaled to the actual 1792px-wide reference game area.
            bool[] reachable = new bool[41];
            reachable[20] = true; // Pino initially at centre
            for (float time = 0f; time <= 15f; time += .25f)
            {
                var bullets = new List<Vector2>();
                foreach (object shot in shots)
                {
                    Type t = shot.GetType();
                    float born = (float)t.GetField("atBeat").GetValue(shot);
                    float life = (float)t.GetField("lifeBeats").GetValue(shot);
                    if (time < born || time > born + life) continue;
                    bullets.Add((Vector2)position.Invoke(null,
                        new object[] { shot, time - born, 900f, 450f }));
                }
                var next = new bool[reachable.Length];
                bool canContinue = false;
                for (int i = 0; i < next.Length; i++)
                {
                    bool adjacent = reachable[i] ||
                        (i > 0 && reachable[i - 1]) ||
                        (i + 1 < next.Length && reachable[i + 1]);
                    if (!adjacent) continue;
                    Vector2 candidate = new Vector2(-400f + i * 20f, -99f);
                    bool safe = true;
                    foreach (Vector2 shot in bullets)
                        if (Vector2.Distance(candidate, shot) < 27f)
                        {
                            safe = false;
                            break;
                        }
                    if (!safe) continue;
                    next[i] = true;
                    canContinue = true;
                }
                Assert.IsTrue(canContinue,
                    id + " has no continuously reachable dodge corridor at " +
                    time + " beats.");
                reachable = next;
            }
        }
    }

    [Test]
    public void FullBandHitAttackMotifsCannotAllBeIgnoredAtCenter()
    {
        // Without a target, the previously-authored side fans could be
        // ignored by a Pino standing still at the exact starting position.
        // fh_curtain is intentionally a breather, so it may leave center safe.
        string[] pressureMotifs = {
            "fh_beat", "fh_cross", "fh_gate", "fh_sweep",
            "fh_orbit", "fh_comet", "fh_finale"
        };
        MethodInfo generate = PatternType.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo position = PatternType.GetMethod("Position",
            BindingFlags.Public | BindingFlags.Static);
        foreach (string id in pressureMotifs)
        {
            IEnumerable source = generate.Invoke(null, new object[] { id }) as IEnumerable;
            bool threatensCenter = false;
            foreach (object shot in source)
            {
                Type type = shot.GetType();
                float born = (float)type.GetField("atBeat").GetValue(shot);
                float life = (float)type.GetField("lifeBeats").GetValue(shot);
                for (float time = born; time <= born + life; time += .05f)
                {
                    Vector2 point = (Vector2)position.Invoke(null,
                        new object[] { shot, time - born, 900f, 450f });
                    if (point.sqrMagnitude <= 24f * 24f)
                    {
                        threatensCenter = true;
                        break;
                    }
                }
                if (threatensCenter) break;
            }
            Assert.IsTrue(threatensCenter,
                id + " can be ignored completely while standing in the middle.");
        }
    }

    private struct TimedFullBandPellet
    {
        public object pellet;
        public double launch;
        public double end;
        public float secondsPerBeat;
    }

    private static float DistanceToSegmentForAudit(Vector2 point,
        Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        if (delta.sqrMagnitude < .00001f) return Vector2.Distance(point, start);
        float progress = Mathf.Clamp01(Vector2.Dot(point - start, delta) /
            delta.sqrMagnitude);
        return Vector2.Distance(point, start + progress * delta);
    }

    [Test]
    public void FullBandHitWholeSongKeepsAReachableHorizontalDodgeRoute()
    {
        // Chart-level check rather than independent motif snapshots. Use the
        // real authored attack order, the BPM change map and the runtime's
        // 4.6-beat approach lead. Pino rhythm clicks are location-independent.
        // A single horizontal risk-line route is only a conservative model:
        // it cannot replace Play Mode collision, vertical dodges, player
        // reaction time, input stress or audio verification.
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(
            "Assets/MouseMode/ChartSystem/Charts/FullBandHit_MOUSE.asset");
        Assert.IsNotNull(asset);
        FieldInfo dataField = asset.GetType().GetField("data");
        Assert.IsNotNull(dataField);
        object chart = dataField.GetValue(asset);
        Assert.IsNotNull(chart);

        Type timingType = Type.GetType(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartTiming, Assembly-CSharp");
        Assert.IsNotNull(timingType);
        MethodInfo beatToSeconds = timingType.GetMethod("BeatToSeconds");
        MethodInfo secondsPerBeat = timingType.GetMethod("SecondsPerBeatAt");
        MethodInfo generate = PatternType.GetMethod("GenerateForChart",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo position = PatternType.GetMethod("Position",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(beatToSeconds);
        Assert.IsNotNull(secondsPerBeat);
        Assert.IsNotNull(generate);
        Assert.IsNotNull(position);

        IEnumerable attacks = dataField.FieldType.GetField("attacks")
            .GetValue(chart) as IEnumerable;
        Assert.IsNotNull(attacks);
        var timeline = new List<TimedFullBandPellet>();
        int eventCount = 0;
        foreach (object attack in attacks)
        {
            Type type = attack.GetType();
            float beat = (float)type.GetField("beat").GetValue(attack);
            string id = (string)type.GetField("patternId").GetValue(attack);
            Assert.IsTrue(id.StartsWith("fh_", StringComparison.Ordinal));
            float approachBeats = Mathf.Min(4.6f, Mathf.Max(0f, beat));
            float period = (float)(double)secondsPerBeat.Invoke(
                null, new object[] { chart, beat });
            IEnumerable bullets = generate.Invoke(
                null, new object[] { id, beat, chart }) as IEnumerable;
            Assert.IsNotNull(bullets);
            foreach (object pellet in bullets)
            {
                Type pelletType = pellet.GetType();
                float pelletBeat = (float)pelletType.GetField("atBeat")
                    .GetValue(pellet);
                float life = (float)pelletType.GetField("lifeBeats")
                    .GetValue(pellet);
                float launchBeat = beat + pelletBeat - approachBeats;
                double launch = (double)beatToSeconds.Invoke(
                    null, new object[] { chart, launchBeat });
                double end = (double)beatToSeconds.Invoke(
                    null, new object[] { chart, launchBeat + life });
                timeline.Add(new TimedFullBandPellet
                {
                    pellet = pellet, launch = launch, end = end,
                    secondsPerBeat = period
                });
            }
            eventCount++;
        }
        Assert.AreEqual(15, eventCount);
        Assert.Greater(timeline.Count, 90);

        // 900x450 normalized sample of the 1792px-wide reference stage.
        // maxMoveSpeed=900 UI units/s => about 452 analytical units/s.
        // At 0.10s per step, one 40px horizontal move is achievable.
        const float stageWidth = 900f;
        const float stageHeight = 450f;
        const float riskLineY = -99f;
        const float stepSeconds = .10f;
        const float conservativeHitRadius = 27f;
        bool[] reachable = new bool[21];
        reachable[10] = true;
        for (int frame = 0; frame <= 1310; frame++)
        {
            double now = frame * (double)stepSeconds;
            double previous = Math.Max(0d, now - stepSeconds);
            var current = new List<Vector2>();
            var former = new List<Vector2>();
            foreach (TimedFullBandPellet bullet in timeline)
            {
                if (now < bullet.launch || now > bullet.end) continue;
                float elapsed = Mathf.Max(0f, (float)
                    ((now - bullet.launch) / bullet.secondsPerBeat));
                Vector2 end = (Vector2)position.Invoke(null,
                    new object[] { bullet.pellet, elapsed, stageWidth, stageHeight });
                Vector2 start = end;
                if (previous >= bullet.launch)
                {
                    float previousElapsed = Mathf.Max(0f, (float)
                        ((previous - bullet.launch) / bullet.secondsPerBeat));
                    start = (Vector2)position.Invoke(null,
                        new object[] { bullet.pellet, previousElapsed,
                            stageWidth, stageHeight });
                }
                current.Add(end);
                former.Add(start);
            }

            bool[] next = new bool[reachable.Length];
            bool hasRoute = false;
            for (int i = 0; i < reachable.Length; i++)
            {
                bool canMoveHere = reachable[i] ||
                    (i > 0 && reachable[i - 1]) ||
                    (i + 1 < reachable.Length && reachable[i + 1]);
                if (!canMoveHere) continue;
                Vector2 pino = new Vector2(-400f + i * 40f, riskLineY);
                bool blocked = false;
                for (int k = 0; k < current.Count; k++)
                {
                    if (DistanceToSegmentForAudit(
                        pino, former[k], current[k]) < conservativeHitRadius)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) continue;
                next[i] = true;
                hasRoute = true;
            }
            Assert.IsTrue(hasRoute,
                "Full-Band Hit has no reachable horizontal route at song time " +
                now.ToString("0.00") + "s. Check overlapping authored barrages.");
            reachable = next;
        }
    }

    [Test]
    public void FullBandHitGateVisualCueUsesTheSameSafeLanesAsBullets()
    {
        // A gentle three-lane gate accompanies the wheel note. The later
        // challenge uses a narrower mirrored passage. Both must advertise
        // exactly the opening authored by the projectile library.
        MethodInfo open = PatternType.GetMethod("FullBandHitGateOpenLanes",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo start = PatternType.GetMethod("FullBandHitGateSafeStart",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(open);
        Assert.IsNotNull(start);
        Assert.AreEqual(3, open.Invoke(null,
            new object[] { "fh_gate|open=3|spacing=1.1" }));
        Assert.AreEqual(2, open.Invoke(null,
            new object[] { "fh_gate|mirror=1|spacing=1.05" }));
        Assert.AreEqual(2, start.Invoke(null, new object[] { 0, false, 3 }));
        Assert.AreEqual(2, start.Invoke(null, new object[] { 1, false, 3 }));
        Assert.AreEqual(3, start.Invoke(null, new object[] { 2, false, 3 }));
        Assert.AreEqual(4, start.Invoke(null, new object[] { 0, true, 2 }));
        Assert.AreEqual(3, start.Invoke(null, new object[] { 1, true, 2 }));
        Assert.AreEqual(2, start.Invoke(null, new object[] { 2, true, 2 }));

        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(
            "Assets/MouseMode/ChartSystem/Charts/FullBandHit_MOUSE.asset");
        SerializedProperty attacks = new SerializedObject(asset)
            .FindProperty("data.attacks");
        Assert.IsNotNull(attacks);
        Assert.AreEqual("fh_gate|open=3|spacing=1.1",
            attacks.GetArrayElementAtIndex(3)
                .FindPropertyRelative("patternId").stringValue);
        Assert.AreEqual("fh_gate|mirror=1|spacing=1.05",
            attacks.GetArrayElementAtIndex(11)
                .FindPropertyRelative("patternId").stringValue);
    }

    [Test]
    public void FullBandHitLiveChartKeepsFifteenAuthoredAttackCues()
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(
            "Assets/MouseMode/ChartSystem/Charts/FullBandHit_MOUSE.asset");
        Assert.IsNotNull(asset);
        SerializedProperty attacks = new SerializedObject(asset)
            .FindProperty("data.attacks");
        Assert.IsNotNull(attacks);
        Assert.AreEqual(15, attacks.arraySize);

        var ids = new HashSet<string>();
        for (int i = 0; i < attacks.arraySize; i++)
        {
            string pattern = attacks.GetArrayElementAtIndex(i)
                .FindPropertyRelative("patternId").stringValue;
            Assert.IsTrue(pattern.StartsWith("fh_", StringComparison.Ordinal),
                "Old unrelated bullet pattern in Full-Band Hit cue " + i);
            ids.Add(pattern.Split('|')[0]);
        }
        Assert.AreEqual(8, ids.Count);
    }

    [Test]
    public void OtherSongsGatePatternsMoveOnlyOneLanePerStep()
    {
        MethodInfo gap = PatternType.GetMethod("SharedGateSafeStart",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(gap);
        foreach (string id in new[] { "boot_gate", "mix_gate" })
        {
            int a = (int)gap.Invoke(null, new object[] { id, 0 });
            int b = (int)gap.Invoke(null, new object[] { id, 1 });
            int c = (int)gap.Invoke(null, new object[] { id, 2 });
            Assert.LessOrEqual(Mathf.Abs(a - b), 1, id);
            Assert.LessOrEqual(Mathf.Abs(b - c), 1, id);
            Assert.AreNotEqual(a, b,
                id + " should actually request a dodge movement.");
        }
    }

    [Test]
    public void AllNonLegacyMouseChartsNowHaveDistinctAttackChoices()
    {
        string[] chartNames =
        {
            "BOOTBEAT_MOUSE_LV6", "BOOTBEAT_MOUSE_LV11",
            "ArcadeGroove_MOUSE", "FullBandHit_MOUSE",
            "MabatakiNoSenritsu_MOUSE", "RhythmGameTheme_MOUSE",
            "popn_dash", "NewMouseChart 2"
        };
        foreach (string name in chartNames)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(
                "Assets/MouseMode/ChartSystem/Charts/" + name + ".asset");
            Assert.IsNotNull(asset, "Missing chart " + name);
            SerializedProperty data = new SerializedObject(asset)
                .FindProperty("data.attacks");
            Assert.IsNotNull(data, "Missing attack data " + name);

            var ids = new HashSet<string>();
            for (int i = 0; i < data.arraySize; i++)
            {
                string encoded = data.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("patternId").stringValue;
                ids.Add(encoded.Split('|')[0]);
            }
            Assert.GreaterOrEqual(ids.Count, 7,
                name + " must offer multiple ways to dodge, not repeat rain.");
        }
        // BOOTBEAT_EASY is an intentionally legacy rhythm-only chart.
        // Do not silently activate attacks or rewrite its note data.
    }
}
