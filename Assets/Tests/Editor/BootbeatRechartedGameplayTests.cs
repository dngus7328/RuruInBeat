using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the authored BOOTBEAT demo chart and attack choreography.
/// Editor data checks complement (not replace) actual Play Mode balancing.
/// </summary>
public sealed class BootbeatRechartedGameplayTests
{
    private const string Path =
        "Assets/MouseMode/ChartSystem/Charts/BOOTBEAT_MOUSE_LV6.asset";

    private struct Cue
    {
        public int Id;
        public int Kind;
        public float Beat;
        public float EndBeat;
        public Vector2 Position;
    }

    private static SerializedProperty Data()
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(Path);
        Assert.IsNotNull(asset, "The selected BOOTBEAT LV6 chart is missing.");
        SerializedObject obj = new SerializedObject(asset);
        SerializedProperty data = obj.FindProperty("data");
        Assert.IsNotNull(data);
        return data;
    }

    // The EditMode test assembly may not reference Assembly-CSharp directly.
    // Calculate timestamps from the serialized tempo map instead.
    private static double BeatToSeconds(SerializedProperty data, float beat)
    {
        double bpm = Math.Max(1d, data.FindPropertyRelative("bpm").floatValue);
        double seconds = data.FindPropertyRelative("beatZeroSeconds").floatValue;
        SerializedProperty changes = data.FindPropertyRelative("tempoChanges");
        if (beat <= 0f || changes == null || changes.arraySize == 0)
            return seconds + beat * 60d / bpm;

        double previousBeat = 0d;
        for (int i = 0; i < changes.arraySize; i++)
        {
            SerializedProperty change = changes.GetArrayElementAtIndex(i);
            double changeBeat = change.FindPropertyRelative("beat").floatValue;
            double changeBpm = change.FindPropertyRelative("bpm").floatValue;
            if (changeBeat <= previousBeat || changeBpm <= 0d)
                continue;
            if (changeBeat >= beat)
                break;
            seconds += (changeBeat - previousBeat) * 60d / bpm;
            previousBeat = changeBeat;
            bpm = changeBpm;
        }
        return seconds + (beat - previousBeat) * 60d / bpm;
    }

    private static List<Cue> Notes()
    {
        SerializedProperty notes = Data().FindPropertyRelative("notes");
        Assert.IsNotNull(notes);
        var result = new List<Cue>();
        for (int i = 0; i < notes.arraySize; i++)
        {
            SerializedProperty item = notes.GetArrayElementAtIndex(i);
            result.Add(new Cue {
                Id = item.FindPropertyRelative("id").intValue,
                Kind = item.FindPropertyRelative("kind").enumValueIndex,
                Beat = item.FindPropertyRelative("beat").floatValue,
                EndBeat = item.FindPropertyRelative("endBeat").floatValue,
                Position = item.FindPropertyRelative("position").vector2Value
            });
        }
        return result;
    }

    [Test]
    public void DifficultyReflectsActualChartLoadNotTheSongTitle()
    {
        var chart = Data();
        Assert.That(chart.FindPropertyRelative("level").intValue,
            Is.InRange(3, 6),
            "Rate this chart from click and dodge difficulty, not its title.");
        var notes = Notes();
        Assert.That(notes.Count, Is.InRange(185, 210),
            "Rhythm has enough musical events to avoid empty phrases.");
        Assert.AreEqual(notes.Count,
            new HashSet<int>(notes.ConvertAll(x => x.Id)).Count,
            "Note IDs must stay unique.");
        Assert.AreEqual(4, notes.FindAll(x => x.Kind == 2).Count,
            "Introduce only four wheel-scroll phrases.");
        Assert.GreaterOrEqual(notes.FindAll(x =>
            x.Kind != 2 && Mathf.Abs(x.Beat * 2f -
            Mathf.Round(x.Beat * 2f)) > .16f).Count, 12,
            "Keep a little musical syncopation instead of all downbeats.");
        Assert.That(notes.FindAll(x => x.Beat < 16f).Count, Is.InRange(6, 9),
            "Introduce the musical pulse without leaving several beats silent.");

        var sorted = new List<Cue>(notes);
        sorted.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        for (int i = 1; i < sorted.Count; i++)
            Assert.Greater(sorted[i].Beat - sorted[i - 1].Beat, .45f,
                "No simultaneous notes or unreasonably compressed inputs.");
    }

    [Test]
    public void BeginnerSongHasLearnableRecurringMusicalPhrases()
    {
        var notes = Notes();
        // The early melodic pickup retains its recorded timing.
        // From the established groove onward, authored cues use quarter-beat
        // subdivisions and recurring 16-beat phrases rather than random transients.
        for (int i = 8; i < notes.Count; i++)
        {
            float snapped = Mathf.Round(notes[i].Beat * 4f) / 4f;
            Assert.That(Mathf.Abs(notes[i].Beat - snapped),
                Is.LessThan(.001f),
                "Out-of-pattern note after percussion entrance: " + notes[i].Id);
        }
        for (int phrase = 2; phrase <= 16; phrase++)
        {
            int phraseStart = phrase * 16;
            int count = notes.FindAll(x =>
                x.Beat >= phraseStart && x.Beat < phraseStart + 16).Count;
            Assert.That(count, Is.InRange(10, 12),
                "Keep varied accents and avoid hollow rhythmic phrases.");
        }
    }

    [Test]
    public void NoUnintendedDeadAirOrOneSidedMouseInput()
    {
        var notes = Notes();
        notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        int left = 0;
        int right = 0;
        foreach (Cue n in notes)
        {
            if (n.Kind == 0) left++;
            if (n.Kind == 1) right++;
        }
        Assert.LessOrEqual(Math.Abs(left - right), 15,
            "Alternate left/right rhythm patterns instead of overusing one button.");

        for (int i = 1; i < notes.Count; i++)
        {
            Cue prior = notes[i - 1];
            // A wheel note is actively played until its endBeat.
            float playableEnd = prior.Kind == 2 ? prior.EndBeat : prior.Beat;
            Assert.Less(notes[i].Beat - playableEnd, 3f,
                "Unexpected empty multi-beat gap after note " + prior.Id);
        }
    }

    [Test]
    public void EveryNoteUsesAnIntentionalDistinctScreenPosition()
    {
        var notes = Notes();
        var locations = new HashSet<Vector2>();
        foreach (var cue in notes)
            Assert.IsTrue(locations.Add(cue.Position),
                "A note reused an exact location from a previous note: " +
                cue.Id);
    }

    [Test]
    public void OnScreenNoteBodiesDoNotObscureEachOther()
    {
        var notes = Notes();
        SerializedProperty chart = Data();
        notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        for (int i = 0; i < notes.Count; i++)
        {
            Assert.That(notes[i].Position.x, Is.InRange(0f, 1f));
            Assert.That(notes[i].Position.y, Is.InRange(0f, 1f));
            for (int j = i + 1; j < notes.Count; j++)
            {
                float dt = (float)(BeatToSeconds(chart, notes[j].Beat) -
                    BeatToSeconds(chart, notes[i].Beat));
                if (dt > 1.75f) break;
                float dist = Vector2.Distance(
                    Vector2.Scale(notes[i].Position, new Vector2(900f, 450f)),
                    Vector2.Scale(notes[j].Position, new Vector2(900f, 450f)));
                Assert.GreaterOrEqual(dist, 90f,
                    "Overlapping cue bodies: IDs " + notes[i].Id + "/" +
                    notes[j].Id + ", " + dt.ToString("0.00") + "s apart.");
            }
        }
    }

    [Test]
    public void WheelScrollWindowsLeaveInputBreathingRoom()
    {
        var notes = Notes();
        foreach (var wheel in notes.FindAll(x => x.Kind == 2))
        {
            Assert.Greater(wheel.EndBeat, wheel.Beat + 1.7f);
            foreach (var tap in notes.FindAll(x => x.Kind != 2))
                Assert.IsFalse(tap.Beat > wheel.Beat - 1.19f &&
                    tap.Beat < wheel.EndBeat + 1.19f,
                    "Keep breathing room before and after a level-one wheel-scroll phrase.");
        }
    }

    [Test]
    public void BeginnerBarragesAreSpacedButDistinct()
    {
        SerializedProperty attacks = Data().FindPropertyRelative("attacks");
        Assert.IsNotNull(attacks);
        Assert.That(attacks.arraySize, Is.InRange(34, 36),
            "The tutorial still needs a sustained, engaging dodge rhythm.");
        var kinds = new HashSet<string>();
        float previous = -100f;
        for (int i = 0; i < attacks.arraySize; i++)
        {
            var e = attacks.GetArrayElementAtIndex(i);
            float beat = e.FindPropertyRelative("beat").floatValue;
            string id = e.FindPropertyRelative("patternId").stringValue;
            if (previous >= 0f)
            {
                Assert.Greater(beat, previous);
                Assert.That(beat - previous, Is.InRange(3.9f, 8.1f),
                    "Musical barrages must follow 4- or 8-beat phrase spacing.");
            }
            else Assert.GreaterOrEqual(beat, 18f,
                "Don't ambush a new player immediately on song start.");
            Assert.That(Mathf.Abs(beat / 4f - Mathf.Round(beat / 4f)),
                Is.LessThan(.001f),
                "Barrage accents must land on four-beat measure boundaries.");
            previous = beat;
            kinds.Add(id.Split('|')[0]);
        }
        foreach (string essential in new[] {
            "boot_pulse", "boot_answer", "boot_gate", "boot_sync" })
            Assert.IsTrue(kinds.Contains(essential),
                "Missing signature beginner barrage: " + essential);
    }

    [Test]
    public void EveryNewAttackMotifBuildsVisibleProjectiles()
    {
        Type library = Type.GetType(
            "MouseModeBulletPatternLibrary, Assembly-CSharp");
        Assert.IsNotNull(library);
        MethodInfo generate = library.GetMethod(
            "Generate", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(generate);
        foreach (string id in new[] {
            "boot_pulse", "boot_answer", "boot_gate", "boot_sync" })
        {
            IEnumerable shots = generate.Invoke(null,
                new object[] { id }) as IEnumerable;
            Assert.IsNotNull(shots);
            int count = 0;
            foreach (object ignored in shots) count++;
            Assert.That(count, Is.InRange(44, 46),
                id + " must create full multi-salvo dodge patterns.");
        }
    }

    [Test]
    public void BootbeatAttackPatternsDoNotLeaveCenterPinoPermanentlySafe()
    {
        // A static central Pino previously survived every pattern. Verify
        // the actual pellet trajectories cross the lower central play area.
        Type library = Type.GetType(
            "MouseModeBulletPatternLibrary, Assembly-CSharp");
        Assert.IsNotNull(library);
        MethodInfo generate = library.GetMethod("Generate",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo position = library.GetMethod("Position",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(generate);
        Assert.IsNotNull(position);
        foreach (string pattern in new[] {
            "boot_pulse", "boot_answer", "boot_gate", "boot_sync" })
        {
            IEnumerable pellets = generate.Invoke(null,
                new object[] { pattern }) as IEnumerable;
            Assert.IsNotNull(pellets);
            bool centerIsThreatened = false;
            foreach (object pellet in pellets)
            {
                Type pelletType = pellet.GetType();
                float lifetime = (float)pelletType.GetField("lifeBeats").GetValue(pellet);
                for (float t = 0f; t < lifetime; t += .10f)
                {
                    Vector2 at = (Vector2)position.Invoke(null,
                        new object[] { pellet, t, 900f, 450f });
                    // Representative stationary lower-center hitbox location.
                    if (Vector2.Distance(at, new Vector2(-30f, -100f)) < 12f)
                    {
                        centerIsThreatened = true;
                        break;
                    }
                }
                if (centerIsThreatened) break;
            }
            Assert.IsTrue(centerIsThreatened,
                pattern + " leaves the central stationary Pino untouchable.");
        }
    }

    [Test]
    public void NoteAndApproachCircleHonorInspectorScaling()
    {
        Type player = Type.GetType(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer, Assembly-CSharp");
        Assert.IsNotNull(player);
        MethodInfo sizeMethod = player.GetMethod("NoteSizeFromInspector",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo ringMethod = player.GetMethod("ApproachScaleAt",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(sizeMethod);
        Assert.IsNotNull(ringMethod);

        float size = (float)sizeMethod.Invoke(null, new object[] { 160f, 1.40f });
        float start = (float)ringMethod.Invoke(null,
            new object[] { 10d, 8.25d, 1.75f, 3.70f });
        float end = (float)ringMethod.Invoke(null,
            new object[] { 10d, 10d, 1.75f, 3.70f });

        Assert.That(size, Is.EqualTo(224f).Within(.001f),
            "The player deliberately selected a large note body.");
        Assert.That(start, Is.EqualTo(3.70f).Within(.001f),
            "The configured ring size must not be capped.");
        Assert.That(end, Is.EqualTo(1f).Within(.001f),
            "The ring must reach the note on the hit beat.");
    }

    [Test]
    public void PinoMovementStillKeepsTheIntentionalLowerField()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            "Assets/Scene/Mouse.unity");
        Assert.IsNotNull(scene);
        // This rework does NOT change the PinoMoveArea scene transform,
        // mouse button judgement or require cursor location for rhythm hits.
        Type player = Type.GetType(
            "LuluInBeat.MouseMode.ChartSystem.MouseModeChartPlayer, Assembly-CSharp");
        Assert.IsNotNull(player);
        Assert.IsNull(player.GetField("requireCursorNearNote",
            BindingFlags.Instance | BindingFlags.NonPublic),
            "The old position-independent rhythm input must stay intact.");
    }
}
