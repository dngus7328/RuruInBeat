#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LuluInBeat.MouseMode.ChartSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Read-only audit of ALL mouse chart assets. Does not move notes or change timings.
/// Uses the actual Mouse scene Inspector settings and the same normalized-to-UI
/// clamping/rounded wheel-path rules as MouseModeChartPlayer.
/// Results are screen-size scenarios, NOT evidence of human play-test success.
/// </summary>
public static class MouseChartBatchAudit
{
    private const string ChartDirectory = "Assets/MouseMode/ChartSystem/Charts";
    private const string ReferenceScene = "Assets/Scene/Mouse.unity";
    private const float GoodFallback = 0.13f;
    private const float Epsilon = 0.001f;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private struct Settings
    {
        public float diameter;
        public float lead;
        public float good;
        public float approachScale;
        public string scene;
    }

    private sealed class Entry
    {
        public MouseModeChartNote note;
        public double hit;
        public double end;
        public double spawn;
        public List<Vector2> path;
        public float pathLength;
    }

    private sealed class Counts
    {
        public int bodies;
        public int quickTransitions;
        public int invalidNotes;
        public int closePairs;
        public int missingAttacks;
        public int unknownAttacks;
        public int notesAfterAudio;
        public int attacksAfterAudio;
        public int stationaryCenterThreatPatterns;
        public int simulatedAdvancedPatterns;
        public readonly List<string> details = new List<string>();
    }

    [MenuItem("Tools/Ruru In Beat/Audit All Mouse Charts")]
    public static void AuditAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Chart Audit] Stop Play Mode before checking chart assets.");
            return;
        }

        Settings settings;
        try { settings = ReadSceneSettings(); }
        catch (Exception error)
        {
            Debug.LogError("[Chart Audit] Cannot read real Inspector settings: " + error.Message);
            return;
        }

        string[] ids = AssetDatabase.FindAssets("t:MouseModeChartAsset",
            new[] { ChartDirectory });
        Array.Sort(ids, StringComparer.Ordinal);

        var summary = new StringBuilder();
        var details = new StringBuilder();
        summary.AppendLine("asset,screen,note_diameter,lead_seconds,approach_start_scale,notes,wheels,attacks,body_overlap_pairs,close_pairs,quick_same_area_transitions,invalid_notes,notes_after_audio,attacks_after_audio,unknown_attacks,advanced_patterns_simulated,advanced_patterns_threatening_stationary_center");
        details.AppendLine("asset,screen,issue,first_id,second_id,time_seconds,min_distance,required_distance,comment");

        int charts = 0;
        int totalProblems = 0;
        foreach (string guid in ids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MouseModeChartAsset asset = AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(path);
            if (asset == null || asset.data == null) continue;

            charts++;
            foreach (Vector2 screen in new[] {
                new Vector2(900f, 450f),
                new Vector2(750f, 375f)
            })
            {
                Counts result = Evaluate(asset, path, settings, screen);
                totalProblems += result.bodies + result.invalidNotes;
                int wheelCount = asset.data.notes == null ? 0 :
                    asset.data.notes.Count(x => x != null && x.kind == MouseModeNoteKind.Wheel);
                int count = asset.data.notes == null ? 0 : asset.data.notes.Count;
                int attackCount = asset.data.attacks == null ? 0 : asset.data.attacks.Count;
                string label = ((int)screen.x) + "x" + ((int)screen.y);
                summary.AppendLine(string.Join(",", new [] {
                    Csv(path), Csv(label), Num(settings.diameter), Num(settings.lead),
                    Num(settings.approachScale), count.ToString(), wheelCount.ToString(),
                    attackCount.ToString(), result.bodies.ToString(),
                    result.closePairs.ToString(), result.quickTransitions.ToString(),
                    result.invalidNotes.ToString(),
                    result.notesAfterAudio.ToString(), result.attacksAfterAudio.ToString(),
                    result.unknownAttacks.ToString(),
                    result.simulatedAdvancedPatterns.ToString(),
                    result.stationaryCenterThreatPatterns.ToString()
                }));
                foreach (string line in result.details) details.AppendLine(line);
                Debug.Log("[Chart Audit] " + asset.name + " / " + label +
                    " / notes=" + count + " / visible body overlaps=" + result.bodies +
                    " / rapid same-area transitions=" + result.quickTransitions +
                    " / invalid=" + result.invalidNotes +
                    " / timed outside audio=" + result.notesAfterAudio +
                    " / unknown attacks=" + result.unknownAttacks);
            }
        }

        string root = Path.GetFullPath("Reports");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "MouseChartAuditSummary.csv"),
            summary.ToString(), new UTF8Encoding(true));
        File.WriteAllText(Path.Combine(root, "MouseChartAuditDetails.csv"),
            details.ToString(), new UTF8Encoding(true));
        Debug.Log("[Chart Audit] Complete. " + charts + " charts x 2 preview sizes; " +
            "possible body overlaps/invalid records=" + totalProblems +
            ". Read Reports/MouseChartAuditSummary.csv and MouseChartAuditDetails.csv. " +
            "These are static geometry checks, NOT verified Unity gameplay.");
    }

    private static Settings ReadSceneSettings()
    {
        Scene scene = SceneManager.GetSceneByPath(ReferenceScene);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(ReferenceScene, OpenSceneMode.Additive);
        try
        {
            MouseModeChartPlayer player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                player = root.GetComponentInChildren<MouseModeChartPlayer>(true);
                if (player != null) break;
            }
            if (player == null)
                throw new InvalidOperationException("MouseModeChartPlayer missing in " +
                    ReferenceScene);

            SerializedObject serialized = new SerializedObject(player);
            float noteSize = RequiredFloat(serialized, "noteSize");
            float scale = RequiredFloat(serialized, "noteVisualScale");
            Settings value = new Settings {
                diameter = Mathf.Max(1f, noteSize * scale),
                lead = Mathf.Clamp(RequiredFloat(serialized, "approachTravelSeconds"),
                    0.70f, 2.20f),
                approachScale = Mathf.Max(1f,
                    RequiredFloat(serialized, "approachStartScale")),
                good = RequiredFloat(serialized, "goodWindow"),
                scene = ReferenceScene
            };
            if (value.good <= 0f) value.good = GoodFallback;
            return value;
        }
        finally
        {
            if (openedHere && scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static float RequiredFloat(SerializedObject serialized, string key)
    {
        SerializedProperty value = serialized.FindProperty(key);
        if (value == null)
            throw new InvalidOperationException("Inspector property missing: " + key);
        return value.floatValue;
    }

    private static Counts Evaluate(MouseModeChartAsset asset, string file,
        Settings settings, Vector2 stage)
    {
        var result = new Counts();
        MouseModeChartData chart = asset.data;
        if (chart.notes == null) return result;
        double songLength = asset.song == null ? double.PositiveInfinity :
            asset.song.length;
        var entries = new List<Entry>(chart.notes.Count);
        var usedIds = new HashSet<int>();

        foreach (MouseModeChartNote note in chart.notes)
        {
            if (note == null) { result.invalidNotes++; continue; }
            bool invalid = note.id <= 0 || !usedIds.Add(note.id) ||
                !Finite(note.beat) || !Finite(note.position.x) ||
                !Finite(note.position.y) || note.position.x < 0f ||
                note.position.x > 1f || note.position.y < 0f ||
                note.position.y > 1f ||
                (note.kind == MouseModeNoteKind.Wheel &&
                 (!Finite(note.endBeat) || note.endBeat <= note.beat));
            if (note.path != null)
                foreach (Vector2 point in note.path)
                    if (!Finite(point.x) || !Finite(point.y) ||
                        point.x < 0f || point.x > 1f ||
                        point.y < 0f || point.y > 1f) invalid = true;
            if (invalid)
            {
                result.invalidNotes++;
                AddDetail(result, file, stage, "INVALID_NOTE", note.id, -1,
                    0d, 0f, 0f, "Invalid ID/coordinate/wheel duration");
                continue;
            }

            double hit = MouseModeChartTiming.BeatToSeconds(chart, note.beat);
            double end = MouseModeChartTiming.BeatToSeconds(chart,
                note.kind == MouseModeNoteKind.Wheel ? note.endBeat : note.beat);
            if (double.IsNaN(hit) || double.IsInfinity(hit) ||
                double.IsNaN(end) || double.IsInfinity(end))
            {
                result.invalidNotes++;
                continue;
            }
            if (hit < -.05d || end > songLength + .15d)
            {
                result.notesAfterAudio++;
                AddDetail(result, file, stage, "NOTE_OUTSIDE_AUDIO",
                    note.id, -1, hit, 0f, 0f, "Note timing outside playable audio duration");
            }
            var entry = new Entry {
                note = note, hit = hit, end = end, spawn = hit - settings.lead,
                path = CreateUiPath(note, stage, settings.diameter)
            };
            entry.pathLength = PathDistance(entry.path);
            entries.Add(entry);
        }
        entries.Sort((a, b) => a.hit.CompareTo(b.hit));

        for (int i = 0; i < entries.Count; i++)
        {
            Entry a = entries[i];
            if (i + 1 < entries.Count)
            {
                Entry b = entries[i + 1];
                double gap = b.hit - a.hit;
                float separation = Vector2.Distance(
                    NoteAt(a, a.hit), NoteAt(b, b.hit));
                if (gap >= 0d && gap <= 0.75d &&
                    separation < settings.diameter * 1.15f)
                {
                    result.quickTransitions++;
                    AddDetail(result, file, stage, "QUICK_SAME_AREA",
                        a.note.id, b.note.id, b.hit, separation,
                        settings.diameter * 1.15f,
                        "Consecutive hit positions too close to read comfortably; review by ear");
                }
            }

            for (int j = i + 1; j < entries.Count; j++)
            {
                Entry b = entries[j];
                if (b.spawn > a.end + settings.good) break;
                double from = Math.Max(a.spawn, b.spawn);
                double until = Math.Min(a.end + settings.good, b.end + settings.good);
                if (until < from) continue;
                float minDistance = float.MaxValue;
                double timeOfMin = from;
                // Wheel heads follow smoothed UI polylines; sample throughout
                // BOTH notes' actual visible intervals, including wheel movement.
                const int samples = 24;
                for (int sample = 0; sample <= samples; sample++)
                {
                    double t = from + (until - from) * sample / samples;
                    float distance = Vector2.Distance(NoteAt(a, t), NoteAt(b, t));
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        timeOfMin = t;
                    }
                }
                if (minDistance < settings.diameter)
                {
                    result.bodies++;
                    AddDetail(result, file, stage, "VISIBLE_BODY_OVERLAP",
                        a.note.id, b.note.id, timeOfMin,
                        minDistance, settings.diameter,
                        "Notes can occupy the same visible region before judgement");
                }
                else if (minDistance < settings.diameter * 1.30f)
                    result.closePairs++;
            }
        }

        CheckAttackData(chart, file, stage, songLength, result);
        return result;
    }

    private static void CheckAttackData(MouseModeChartData chart, string file,
        Vector2 stage, double songLength, Counts result)
    {
        if (chart.attacks == null || chart.attacks.Count == 0)
        {
            result.missingAttacks = 1;
            AddDetail(result, file, stage, "NO_ATTACKS", -1, -1,
                0d, 0f, 0f, "No bullet attack events in this chart");
            return;
        }
        foreach (MouseModeChartAttack attack in chart.attacks)
        {
            if (attack == null || !Finite(attack.beat) ||
                string.IsNullOrEmpty(attack.patternId))
            {
                result.unknownAttacks++;
                AddDetail(result, file, stage, "INVALID_ATTACK", -1, -1,
                    0d, 0f, 0f, "Attack event is null or missing a valid ID/beat");
                continue;
            }
            double seconds = MouseModeChartTiming.BeatToSeconds(chart, attack.beat);
            if (seconds < -.05d || seconds > songLength + .15d)
            {
                result.attacksAfterAudio++;
                AddDetail(result, file, stage, "ATTACK_OUTSIDE_AUDIO", -1, -1,
                    seconds, 0f, 0f, attack.patternId);
            }
            MouseModeBulletDirector.AttackSpec spec =
                MouseModeBulletDirector.ParseAttack(attack.patternId);
            if (spec.kind != MouseModeBulletDirector.AttackKind.Legacy)
                continue; // Laser, burst and rain require a full runtime simulation.
            string pattern = spec.legacyId;
            bool advanced = MouseModeBulletPatternLibrary.IsSupported(pattern);
            bool builtin = pattern == "basic" || pattern == "left" ||
                pattern == "right" || pattern == "split" ||
                pattern == "spread" || pattern == "fan" ||
                pattern == "rain" || pattern == "aim" ||
                pattern == "ex_fan" || pattern == "ex_weave" ||
                pattern == "ex_cross" || pattern == "ex_gate" ||
                pattern == "ex_sweep" || pattern == "ex_spiral" ||
                pattern == "ex_pincer" || pattern == "ex_aim" ||
                pattern == "ex_burst" ||
                Array.IndexOf(MouseModeBulletDirector.AdvancedPatternIds, pattern) >= 0;
            if (!advanced && !builtin)
            {
                result.unknownAttacks++;
                AddDetail(result, file, stage, "UNKNOWN_ATTACK", -1, -1,
                    seconds, 0f, 0f, attack.patternId);
                continue;
            }
            if (!advanced) continue;
            result.simulatedAdvancedPatterns++;
            bool hitsStationaryCenter = false;
            // This is an indicative trajectory probe, NOT actual Pino control,
            // bullet collision, invulnerability, or survivability verification.
            foreach (MouseModeBulletPatternLibrary.Pellet pellet in
                MouseModeBulletPatternLibrary.GenerateForChart(
                    attack.patternId, attack.beat, chart))
            {
                for (float elapsed = 0f;
                    elapsed <= pellet.lifeBeats; elapsed += .10f)
                {
                    Vector2 position = MouseModeBulletPatternLibrary.Position(
                        pellet, elapsed, stage.x, stage.y);
                    Vector2 center = new Vector2(-stage.x / 30f,
                        -stage.y * (100f / 450f));
                    if (Vector2.Distance(position, center) < 12f)
                    {
                        hitsStationaryCenter = true;
                        break;
                    }
                }
                if (hitsStationaryCenter) break;
            }
            if (hitsStationaryCenter)
                result.stationaryCenterThreatPatterns++;
            else
                AddDetail(result, file, stage, "STATIONARY_CENTER_UNTHREATENED",
                    -1, -1, seconds, 0f, 0f,
                    "Sampled advanced bullets do not cross stationary lower center: " +
                    pattern);
        }
    }

    private static List<Vector2> CreateUiPath(MouseModeChartNote note,
        Vector2 stage, float diameter)
    {
        var points = new List<Vector2>();
        if (note.kind == MouseModeNoteKind.Wheel &&
            note.path != null && note.path.Count > 0)
            foreach (Vector2 p in note.path)
                points.Add(ToUi(p, stage, diameter));
        if (points.Count == 0)
            points.Add(ToUi(note.position, stage, diameter));
        if (points.Count < 2) return points;

        // Same two-pass open Chaikin rounding as runtime.
        var unique = new List<Vector2> { points[0] };
        for (int i = 1; i < points.Count; i++)
            if (Vector2.Distance(unique[unique.Count - 1], points[i]) > .5f)
                unique.Add(points[i]);
        if (unique.Count < 3) return unique;
        for (int pass = 0; pass < 2; pass++)
        {
            var rounded = new List<Vector2> { unique[0] };
            for (int i = 0; i < unique.Count - 1; i++)
            {
                rounded.Add(Vector2.Lerp(unique[i], unique[i + 1], .25f));
                rounded.Add(Vector2.Lerp(unique[i], unique[i + 1], .75f));
            }
            rounded.Add(unique[unique.Count - 1]);
            unique = rounded;
        }
        return unique;
    }

    private static Vector2 ToUi(Vector2 value, Vector2 stage, float diameter)
    {
        float pad = diameter * .62f;
        return new Vector2(
            Mathf.Clamp((Mathf.Clamp01(value.x) - .5f) * stage.x,
                -stage.x * .5f + pad, stage.x * .5f - pad),
            Mathf.Clamp((.5f - Mathf.Clamp01(value.y)) * stage.y,
                -stage.y * .5f + pad, stage.y * .5f - pad));
    }

    private static float PathDistance(List<Vector2> path)
    {
        float sum = 0f;
        for (int i = 1; i < path.Count; i++)
            sum += Vector2.Distance(path[i - 1], path[i]);
        return sum;
    }

    private static Vector2 NoteAt(Entry note, double time)
    {
        List<Vector2> path = note.path;
        if (path.Count == 0) return Vector2.zero;
        if (note.note.kind != MouseModeNoteKind.Wheel ||
            path.Count == 1 || time <= note.hit)
            return path[0];

        float remaining = Mathf.Clamp01(
            (float)((time - note.hit) / Math.Max(Epsilon, note.end - note.hit))) *
            note.pathLength;
        for (int i = 1; i < path.Count; i++)
        {
            float length = Vector2.Distance(path[i - 1], path[i]);
            if (remaining <= length || i == path.Count - 1)
                return Vector2.Lerp(path[i - 1], path[i],
                    length > Epsilon ? Mathf.Clamp01(remaining / length) : 1f);
            remaining -= length;
        }
        return path[path.Count - 1];
    }

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string Csv(string value)
    {
        return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }

    private static string Num(double value)
    {
        return value.ToString("0.###", Invariant);
    }

    private static void AddDetail(Counts result, string file, Vector2 stage,
        string issue, int a, int b, double time, float distance, float required,
        string message)
    {
        result.details.Add(string.Join(",", new [] {
            Csv(file), Csv(((int)stage.x) + "x" + ((int)stage.y)),
            Csv(issue), a.ToString(), b.ToString(), Num(time),
            Num(distance), Num(required), Csv(message)
        }));
    }
}
#endif
