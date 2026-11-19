using System;
using System.Collections.Generic;
using UnityEngine;

namespace LuluInBeat.MouseMode.ChartSystem
{
    public enum MouseModeNoteKind
    {
        Left,
        Right,
        Wheel
    }

    [Serializable]
    public class MouseModeChartNote
    {
        public int id;
        public MouseModeNoteKind kind;
        public float beat = 4f;
        public Vector2 position = new Vector2(0.5f, 0.5f);

        // Wheel 노트일 때만 사용한다.
        public float endBeat = 6f;
        public List<Vector2> path = new List<Vector2>();
    }

    [Serializable]
    public class MouseModeTempoChange
    {
        // Beat position where the new tempo starts. Beat 0 uses the base bpm.
        public float beat = 4f;
        public float bpm = 120f;
    }

    [Serializable]
    public class MouseModeChartAttack
    {
        public float beat = 4f;
        public string patternId = "basic";
    }

    [Serializable]
    public class MouseModeChartData
    {
        public int version = 1;
        public string songId = "new_song";
        // 채보별 임시 레벨; 플레이 테스트 후 확정. 기존 JSON에는 기본값 1 적용.
        public int level = 1;
        public string difficulty = "NORMAL";
        public float bpm = 120f;
        public float beatZeroSeconds = 0f;
        // Optional. Empty for all existing charts, preserving old timing exactly.
        public List<MouseModeTempoChange> tempoChanges = new List<MouseModeTempoChange>();
        public float approachSeconds = 1.2f;
        // Optional per-chart appearance tuning. Does not alter note hit time,
        // clickable location, judgement windows, or Pino movement.
        public float displayNoteScale = 1f;
        public float displayApproachScale = 1f;
        public List<MouseModeChartNote> notes = new List<MouseModeChartNote>();
        public List<MouseModeChartAttack> attacks = new List<MouseModeChartAttack>();

        public void EnsureLists()
        {
            if (notes == null) notes = new List<MouseModeChartNote>();
            if (attacks == null) attacks = new List<MouseModeChartAttack>();
            if (tempoChanges == null) tempoChanges = new List<MouseModeTempoChange>();
            tempoChanges.RemoveAll(t => t == null || t.beat <= 0f || t.bpm <= 0f);
            tempoChanges.Sort((a, b) => a.beat.CompareTo(b.beat));
            foreach (MouseModeChartNote note in notes)
                if (note != null && note.path == null)
                    note.path = new List<Vector2>();
        }

        public int NextId()
        {
            EnsureLists();
            int result = 1;
            foreach (MouseModeChartNote note in notes)
                if (note != null) result = Math.Max(result, note.id + 1);
            return result;
        }
    }

    public static class MouseModeChartTiming
    {
        // Piecewise-constant musical tempo map. One authoritative conversion is
        // used by notes, editor, and bullet scheduling. An empty map is legacy BPM.
        public static double BeatToSeconds(MouseModeChartData data, float beat)
        {
            if (data == null) return 0d;
            double baseBpm = Math.Max(1.0, data.bpm);
            if (beat <= 0f || data.tempoChanges == null || data.tempoChanges.Count == 0)
                return data.beatZeroSeconds + beat * 60d / baseBpm;
            double seconds = data.beatZeroSeconds;
            double previousBeat = 0d;
            double bpm = baseBpm;
            foreach (MouseModeTempoChange change in data.tempoChanges)
            {
                if (change == null || change.beat <= previousBeat || change.bpm <= 0f)
                    continue;
                if (change.beat >= beat) break;
                seconds += (change.beat - previousBeat) * 60d / bpm;
                previousBeat = change.beat;
                bpm = change.bpm;
            }
            return seconds + (beat - previousBeat) * 60d / bpm;
        }

        public static double SecondsToBeat(MouseModeChartData data, double seconds)
        {
            if (data == null) return 0d;
            double bpm = Math.Max(1.0, data.bpm);
            double remaining = seconds - data.beatZeroSeconds;
            if (remaining <= 0d || data.tempoChanges == null || data.tempoChanges.Count == 0)
                return remaining * bpm / 60d;
            double previousBeat = 0d;
            foreach (MouseModeTempoChange change in data.tempoChanges)
            {
                if (change == null || change.beat <= previousBeat || change.bpm <= 0f)
                    continue;
                double segmentSeconds = (change.beat - previousBeat) * 60d / bpm;
                if (remaining <= segmentSeconds)
                    return previousBeat + remaining * bpm / 60d;
                remaining -= segmentSeconds;
                previousBeat = change.beat;
                bpm = change.bpm;
            }
            return previousBeat + remaining * bpm / 60d;
        }

        public static double SecondsPerBeatAt(MouseModeChartData data, float beat)
        {
            if (data == null) return 0.5d;
            double bpm = Math.Max(1.0, data.bpm);
            if (data.tempoChanges != null)
            {
                foreach (MouseModeTempoChange change in data.tempoChanges)
                {
                    if (change == null || change.bpm <= 0f) continue;
                    if (change.beat > beat) break;
                    bpm = change.bpm;
                }
            }
            return 60d / bpm;
        }

        // 좌상단 (0,0), 우하단 (1,1) 좌표.
        public static Vector2 GetPathPosition(MouseModeChartNote note, float progress)
        {
            if (note == null) return new Vector2(0.5f, 0.5f);
            if (note.path == null || note.path.Count == 0) return note.position;
            if (note.path.Count == 1) return note.path[0];

            float total = 0f;
            for (int i = 1; i < note.path.Count; i++)
                total += Vector2.Distance(note.path[i - 1], note.path[i]);

            if (total < 0.0001f) return note.path[0];
            float remaining = Mathf.Clamp01(progress) * total;

            for (int i = 1; i < note.path.Count; i++)
            {
                Vector2 a = note.path[i - 1];
                Vector2 b = note.path[i];
                float length = Vector2.Distance(a, b);
                if (remaining <= length || i == note.path.Count - 1)
                    return Vector2.Lerp(a, b,
                        length > 0.0001f ? Mathf.Clamp01(remaining / length) : 1f);
                remaining -= length;
            }
            return note.path[note.path.Count - 1];
        }
    }
}
