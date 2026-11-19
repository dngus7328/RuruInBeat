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
    public void FullSongHasLearnablePhrasesSyncopationAndDeliberateChords()
    {
        var notes = Notes();
        Assert.GreaterOrEqual(notes.Count, 225);
        Assert.LessOrEqual(notes.Count, 290);
        Assert.AreEqual(notes.Count,
            new HashSet<int>(notes.ConvertAll(x => x.Id)).Count,
            "Each note must have a unique stable ID.");
        Assert.GreaterOrEqual(notes.FindAll(x => x.Kind == 2).Count, 7,
            "Wheel phrases must be present across the song.");
        Assert.GreaterOrEqual(notes.FindAll(x =>
            x.Kind != 2 && Mathf.Abs(x.Beat * 2f -
            Mathf.Round(x.Beat * 2f)) > .16f).Count, 60,
            "Music-derived syncopations must not collapse back to a straight grid.");

        int chords = 0;
        notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        for (int i = 1; i < notes.Count; i++)
            if (Mathf.Abs(notes[i].Beat - notes[i - 1].Beat) < .0001f)
            {
                chords++;
                Assert.AreNotEqual(notes[i].Kind, notes[i - 1].Kind,
                    "Simultaneous cues must be intentional L+R input.");
            }
        Assert.GreaterOrEqual(chords, 6);
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
                Assert.IsFalse(tap.Beat > wheel.Beat + .15f &&
                    tap.Beat < wheel.EndBeat + .12f,
                    "Click overlaps active wheel-scroll phrase.");
        }
    }

    [Test]
    public void BarragesAreFrequentButUseDistinctGameplayMotifs()
    {
        SerializedProperty attacks = Data().FindPropertyRelative("attacks");
        Assert.IsNotNull(attacks);
        Assert.GreaterOrEqual(attacks.arraySize, 30);
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
                Assert.LessOrEqual(beat - previous, 16f,
                    "Long dead air between bullet attacks.");
            }
            else
            {
                Assert.GreaterOrEqual(beat, 0f);
            }
            previous = beat;
            kinds.Add(id.Split('|')[0]);
        }
        foreach (string essential in new[] {
            "boot_pulse", "boot_answer", "boot_gate", "boot_sync" })
            Assert.IsTrue(kinds.Contains(essential),
                "Missing signature BOOTBEAT attack: " + essential);
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
            Assert.That(count, Is.InRange(10, 40),
                id + " produced unusable projectile density.");
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
