#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

public static class MouseAllChartsAudit
{
    // Check actual chart assets against the same 900 x 450 virtual note area.
    // This is a static safety test, not a substitute for testing Pino and real bullets.
    private const float VirtualWidth = 900f;
    private const float VirtualHeight = 450f;
    private const float DisplayLeadSeconds = 1.75f;
    private const float MinimumSpacingPixels = 137f;
    private const float CursorSpeedWarningPixelsPerSecond = 1200f;

    private static readonly string[] ChartIds =
    {
        "BOOTBEAT_MOUSE_LV6",
        "BOOTBEAT_MOUSE_LV11",
        "popn_dash",
        "FullBandHit_MOUSE",
        "RhythmGameTheme_MOUSE",
        "ArcadeGroove_MOUSE",
        "MabatakiNoSenritsu_MOUSE"
    };

    [MenuItem("Tools/Lulu In Beat/Verify All Mouse Charts")]
    private static void Run()
    {
        int failures = 0, warnings = 0;
        foreach (string id in ChartIds)
        {
            string path = "Assets/MouseMode/ChartSystem/Charts/" + id + ".asset";
            MouseModeChartAsset asset = AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(path);
            if (asset == null || asset.data == null)
            {
                Debug.LogError("[ChartAudit] Missing or invalid chart: " + path);
                failures++;
                continue;
            }

            MouseModeChartData data = asset.data;
            data.EnsureLists();
            if (data.bpm <= 0f || data.notes.Count == 0)
            {
                Debug.LogError("[ChartAudit] Invalid BPM or no notes: " + path);
                failures++;
                continue;
            }
            if (asset.song == null)
            {
                Debug.LogWarning("[ChartAudit] Missing AudioClip in " + id +
                    ". Import the three MP3s with their original .meta files.");
                warnings++;
            }

            var ordered = new System.Collections.Generic.List<MouseModeChartNote>(data.notes);
            ordered.Sort((a, b) => a.beat.CompareTo(b.beat));
            double previousTime = -1d;
            Vector2 previousPosition = Vector2.zero;
            int overlapCount = 0, cursorWarnings = 0, badNoteCount = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                MouseModeChartNote a = ordered[i];
                if (a == null) { badNoteCount++; continue; }
                double ta = MouseModeChartTiming.BeatToSeconds(data, a.beat);
                if (double.IsNaN(ta) || ta < 0d ||
                    (asset.song != null && ta > asset.song.length + .05d))
                    badNoteCount++;
                if (a.kind == MouseModeNoteKind.Wheel &&
                    (a.endBeat <= a.beat || a.path == null || a.path.Count < 2))
                    badNoteCount++;

                if (previousTime >= 0d && ta > previousTime)
                {
                    float pixels = Vector2.Distance(
                        new Vector2(a.position.x * VirtualWidth, a.position.y * VirtualHeight),
                        new Vector2(previousPosition.x * VirtualWidth, previousPosition.y * VirtualHeight));
                    float speed = (float)(pixels / (ta - previousTime));
                    if (speed > CursorSpeedWarningPixelsPerSecond) cursorWarnings++;
                }

                for (int j = i + 1; j < ordered.Count; j++)
                {
                    MouseModeChartNote b = ordered[j];
                    if (b == null) continue;
                    double tb = MouseModeChartTiming.BeatToSeconds(data, b.beat);
                    if (tb - ta >= DisplayLeadSeconds) break;
                    float dx = (a.position.x - b.position.x) * VirtualWidth;
                    float dy = (a.position.y - b.position.y) * VirtualHeight;
                    if (Mathf.Sqrt(dx * dx + dy * dy) < MinimumSpacingPixels)
                        overlapCount++;
                }
                previousTime = ta;
                previousPosition = a.position;
            }

            if (overlapCount > 0 || badNoteCount > 0)
            {
                failures++;
                Debug.LogError("[ChartAudit] " + id + ": note overlap pairs=" +
                    overlapCount + ", invalid notes=" + badNoteCount, asset);
            }
            if (cursorWarnings > 0)
            {
                warnings++;
                Debug.LogWarning("[ChartAudit] " + id + ": cursor speed warnings=" +
                    cursorWarnings, asset);
            }

            // A playable mouse chart without any barrage events is an
            // incomplete rhythm-and-dodge chart, not a valid finished song.
            if (data.attacks.Count == 0)
            {
                failures++;
                Debug.LogError("[ChartAudit] No bullet attacks authored in " +
                    id + ". Add attacks in the chart editor.", asset);
            }
            else
            {
                float earliestAttack = float.MaxValue;
                foreach (MouseModeChartAttack cue in data.attacks)
                    if (cue != null)
                        earliestAttack = Mathf.Min(earliestAttack,cue.beat);
                if (earliestAttack > 24f)
                {
                    warnings++;
                    Debug.LogWarning("[ChartAudit] First barrage starts late at beat " +
                        earliestAttack.ToString("0.#") + " in " + id, asset);
                }
            }

            int unknownPatterns = 0;
            foreach (MouseModeChartAttack attack in data.attacks)
            {
                if (attack == null) continue;
                var spec = MouseModeBulletDirector.ParseAttack(attack.patternId);
                if (spec.kind != MouseModeBulletDirector.AttackKind.Legacy)
                    continue;
                string key = spec.legacyId;
                if (MouseModeBulletPatternLibrary.IsSupported(key) ||
                    key == "basic" || key == "left" || key == "right" ||
                    key == "split" || key == "spread" || key == "fan" ||
                    key == "rain" || key == "aim" || key.StartsWith("ex_") ||
                    key.StartsWith("adv_"))
                    continue;
                unknownPatterns++;
            }
            if (unknownPatterns > 0)
            {
                warnings++;
                Debug.LogWarning("[ChartAudit] " + id + ": unrecognized attack IDs=" +
                    unknownPatterns, asset);
            }

            Debug.Log("[ChartAudit] " + id +
                ": notes=" + ordered.Count +
                ", attacks=" + data.attacks.Count +
                ", BPM markers=" + (data.tempoChanges != null ? data.tempoChanges.Count : 0) +
                ", body overlaps=" + overlapCount +
                ", excessive cursor moves=" + cursorWarnings +
                ", unknown patterns=" + unknownPatterns,
                asset);
        }

        string summary = "Charts checked: " + ChartIds.Length +
            "\nErrors: " + failures + "\nWarnings: " + warnings +
            "\n\nStatic geometry only. Unity Play Mode is still needed to check actual bullet trajectories.";
        Debug.Log("[ChartAudit] " + summary);
        EditorUtility.DisplayDialog("All Mouse Charts Audit", summary, "OK");
    }
}
#endif
