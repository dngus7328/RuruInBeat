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
    private const float SecondsPerBeat = 60f / 134.27f;

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
    public void FirstSongIsActuallyLevelOneAndIntroducesInputsGradually()
    {
        var chart = Data();
        Assert.AreEqual(1, chart.FindPropertyRelative("level").intValue,
            "BOOTBEAT is the first song, not a level-six challenge.");
        var notes = Notes();
        Assert.That(notes.Count, Is.InRange(100, 140),
            "Level 1 must not be 245 frantic rhythm notes.");
        Assert.AreEqual(notes.Count,
            new HashSet<int>(notes.ConvertAll(x => x.Id)).Count,
            "Note IDs must stay unique.");
        Assert.AreEqual(4, notes.FindAll(x => x.Kind == 2).Count,
            "Introduce only four wheel-scroll phrases.");
        Assert.GreaterOrEqual(notes.FindAll(x =>
            x.Kind != 2 && Mathf.Abs(x.Beat * 2f -
            Mathf.Round(x.Beat * 2f)) > .16f).Count, 12,
            "Keep a little musical syncopation instead of all downbeats.");
        Assert.LessOrEqual(notes.FindAll(x => x.Beat < 16f).Count, 5,
            "Leave the opening phrase simple enough to learn the controls.");

        var sorted = new List<Cue>(notes);
        sorted.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        for (int i = 1; i < sorted.Count; i++)
            Assert.Greater(sorted[i].Beat - sorted[i - 1].Beat, .6f,
                "No dense rapid clicks or simultaneous input on level 1.");
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
        notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        for (int i = 0; i < notes.Count; i++)
        {
            Assert.That(notes[i].Position.x, Is.InRange(0f, 1f));
            Assert.That(notes[i].Position.y, Is.InRange(0f, 1f));
            for (int j = i + 1; j < notes.Count; j++)
            {
                float dt = (notes[j].Beat - notes[i].Beat) * SecondsPerBeat;
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
        Assert.That(attacks.arraySize, Is.InRange(9, 13));
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
                Assert.That(beat - previous, Is.InRange(16f, 27f),
                    "Level 1 should give breathing room between barrages.");
            }
            else Assert.GreaterOrEqual(beat, 24f,
                "Don't ambush a new player immediately on song start.");
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
            Assert.That(count, Is.InRange(8, 15),
                id + " produced too many or too few projectiles for level one.");
        }
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
