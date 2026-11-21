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
