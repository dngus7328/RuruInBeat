using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class RuruChartEditorWindow : EditorWindow
{
    private const float TIME_RULER_WIDTH = 85f;
    private const float HEADER_HEIGHT = 28f;
    private const float MIN_LANE_WIDTH = 90f;

    private ChartData chart;

    private Vector2 timelineScroll;

    private float pixelsPerSecond = 150f;
    private int snapDenominator = 16;

    private ChartNoteType placementType =
        ChartNoteType.Tap;

    private float defaultLongBeats = 1f;

    private float currentTime;

    private bool playing;
    private bool followPlayhead = true;

    private double playbackStartEditorTime;

    private int selectedNoteIndex = -1;
    private bool draggingNote;

    private float timelineViewportHeight;
    private float laneWidth;

    // =========================================================
    // JSON
    // =========================================================

    [Serializable]
    private class JsonChart
    {
        public string chartName;
        public string audioClipName;
        public string difficulty;

        public float bpm;
        public float offsetSeconds;
        public float travelTime;

        public JsonNote[] notes;
    }

    [Serializable]
    private class JsonNote
    {
        public float hitTime;
        public string lane;
        public string type;
        public float duration;
    }

    // =========================================================
    // Window
    // =========================================================

    [MenuItem("Tools/Ruru In Beat/Chart Editor")]
    public static void Open()
    {
        RuruChartEditorWindow window =
            GetWindow<RuruChartEditorWindow>();

        window.titleContent =
            new GUIContent("Ruru Chart Editor");

        window.minSize =
            new Vector2(650f, 600f);

        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;

        StopPlaybackAudio();
    }

    private void OnGUI()
    {
        DrawChartToolbar();

        if (chart == null)
        {
            EditorGUILayout.Space(30);

            EditorGUILayout.HelpBox(
                "Chart Data�� �ְų� Import JSON���� ä���� �ҷ�������.",
                MessageType.Info
            );

            return;
        }

        DrawChartInformation();
        DrawTransport();
        DrawEditorTools();

        HandleDeleteKey();

        DrawVerticalTimeline();
    }

    // =========================================================
    // Toolbar
    // =========================================================

    private void DrawChartToolbar()
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.toolbar
        );

        GUILayout.Label(
            "Chart",
            GUILayout.Width(35f)
        );

        ChartData newChart =
            (ChartData)EditorGUILayout.ObjectField(
                chart,
                typeof(ChartData),
                false,
                GUILayout.MinWidth(180f)
            );

        if (newChart != chart)
        {
            SetChart(newChart);
        }

        if (GUILayout.Button(
                "New",
                EditorStyles.toolbarButton,
                GUILayout.Width(45f)
            ))
        {
            CreateNewChart();
        }

        if (GUILayout.Button(
                "Import JSON",
                EditorStyles.toolbarButton,
                GUILayout.Width(85f)
            ))
        {
            ImportJson();
        }

        GUI.enabled = chart != null;

        if (GUILayout.Button(
                "Save",
                EditorStyles.toolbarButton,
                GUILayout.Width(50f)
            ))
        {
            SaveChart();
        }

        GUI.enabled = true;

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // New Chart
    // =========================================================

    private void CreateNewChart()
    {
        string path =
            EditorUtility.SaveFilePanelInProject(
                "Create Chart",
                "NewChart",
                "asset",
                "�� ä�� ������ ������ ��ġ�� �����ϼ���."
            );

        if (string.IsNullOrEmpty(path))
            return;

        ChartData newChart =
            CreateInstance<ChartData>();

        AssetDatabase.CreateAsset(
            newChart,
            path
        );

        AssetDatabase.SaveAssets();

        SetChart(newChart);

        Selection.activeObject =
            newChart;
    }

    // =========================================================
    // Import JSON
    // =========================================================

    private void ImportJson()
    {
        string jsonPath =
            EditorUtility.OpenFilePanel(
                "Import Ruru Chart JSON",
                "",
                "json"
            );

        if (string.IsNullOrEmpty(jsonPath))
            return;

        string jsonText;

        try
        {
            jsonText =
                File.ReadAllText(
                    jsonPath
                );
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog(
                "Import ����",
                "JSON ������ ���� ���߽��ϴ�.\n\n" +
                e.Message,
                "Ȯ��"
            );

            return;
        }

        JsonChart source;

        try
        {
            source =
                JsonUtility.FromJson<JsonChart>(
                    jsonText
                );
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog(
                "Import ����",
                "JSON ������ �ùٸ��� �ʽ��ϴ�.\n\n" +
                e.Message,
                "Ȯ��"
            );

            return;
        }

        if (source == null)
        {
            EditorUtility.DisplayDialog(
                "Import ����",
                "ä�� �����͸� ���� ���߽��ϴ�.",
                "Ȯ��"
            );

            return;
        }

        ChartData newChart =
            CreateInstance<ChartData>();

        newChart.bpm =
            Mathf.Max(
                1f,
                source.bpm
            );

        newChart.offsetSeconds =
            source.offsetSeconds;

        newChart.travelTime =
            Mathf.Max(
                0.1f,
                source.travelTime
            );

        newChart.audioClip =
            FindAudioClip(
                source.audioClipName
            );

        newChart.notes.Clear();

        if (source.notes != null)
        {
            foreach (JsonNote sourceNote
                     in source.notes)
            {
                NoteLane lane;

                if (!Enum.TryParse(
                        sourceNote.lane,
                        true,
                        out lane
                    ))
                {
                    Debug.LogWarning(
                        "�� �� ���� ����: " +
                        sourceNote.lane
                    );

                    continue;
                }

                ChartNoteType noteType;

                if (!Enum.TryParse(
                        sourceNote.type,
                        true,
                        out noteType
                    ))
                {
                    noteType =
                        ChartNoteType.Tap;
                }

                ChartNoteData note =
                    new ChartNoteData();

                note.hitTime =
                    Mathf.Max(
                        0f,
                        sourceNote.hitTime
                    );

                note.lane =
                    lane;

                note.type =
                    noteType;

                note.duration =
                    Mathf.Max(
                        0f,
                        sourceNote.duration
                    );

                newChart.notes.Add(note);
            }
        }

        EnsureFolder(
            "Assets/Charts"
        );

        string chartName =
            string.IsNullOrWhiteSpace(
                source.chartName
            )
            ? Path.GetFileNameWithoutExtension(
                jsonPath
            )
            : source.chartName;

        chartName =
            MakeSafeFileName(
                chartName
            );

        string assetPath =
            AssetDatabase.GenerateUniqueAssetPath(
                "Assets/Charts/" +
                chartName +
                ".asset"
            );

        AssetDatabase.CreateAsset(
            newChart,
            assetPath
        );

        EditorUtility.SetDirty(
            newChart
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SetChart(
            newChart
        );

        Selection.activeObject =
            newChart;

        EditorGUIUtility.PingObject(
            newChart
        );

        string musicText;

        if (newChart.audioClip != null)
        {
            musicText =
                "���� �ڵ� ���� �Ϸ�: " +
                newChart.audioClip.name;
        }
        else
        {
            musicText =
                "���� �ڵ� ���� ����: " +
                source.audioClipName;
        }

        Debug.Log(
            $"ä�� Import �Ϸ� | " +
            $"{chartName} | " +
            $"{newChart.notes.Count} Notes | " +
            $"{musicText}"
        );
    }

    private AudioClip FindAudioClip(
        string clipName
    )
    {
        if (string.IsNullOrWhiteSpace(
                clipName
            ))
        {
            return null;
        }

        string[] guids =
            AssetDatabase.FindAssets(
                clipName +
                " t:AudioClip"
            );

        foreach (string guid in guids)
        {
            string assetPath =
                AssetDatabase.GUIDToAssetPath(
                    guid
                );

            AudioClip clip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    assetPath
                );

            if (clip == null)
                continue;

            if (string.Equals(
                    clip.name,
                    clipName,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return clip;
            }
        }

        return null;
    }

    private void EnsureFolder(
        string folderPath
    )
    {
        string[] parts =
            folderPath.Split('/');

        string current =
            parts[0];

        for (int i = 1;
             i < parts.Length;
             i++)
        {
            string next =
                current +
                "/" +
                parts[i];

            if (!AssetDatabase.IsValidFolder(
                    next
                ))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[i]
                );
            }

            current = next;
        }
    }

    private string MakeSafeFileName(
        string fileName
    )
    {
        foreach (char c in
                 Path.GetInvalidFileNameChars())
        {
            fileName =
                fileName.Replace(
                    c,
                    '_'
                );
        }

        return fileName;
    }

    // =========================================================
    // Set / Save
    // =========================================================

    private void SetChart(
        ChartData newChart
    )
    {
        PausePlayback();

        chart =
            newChart;

        currentTime = 0f;

        timelineScroll =
            Vector2.zero;

        selectedNoteIndex = -1;

        if (chart != null)
        {
            SortNotes();
        }

        Repaint();
    }

    private void SaveChart()
    {
        if (chart == null)
            return;

        SortNotes();

        EditorUtility.SetDirty(
            chart
        );

        AssetDatabase.SaveAssets();

        Debug.Log(
            "ä�� ���� �Ϸ�: " +
            chart.name
        );
    }

    // =========================================================
    // Chart Info
    // =========================================================

    private void DrawChartInformation()
    {
        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(
            "Music",
            GUILayout.Width(55f)
        );

        EditorGUI.BeginChangeCheck();

        AudioClip newClip =
            (AudioClip)EditorGUILayout.ObjectField(
                chart.audioClip,
                typeof(AudioClip),
                false
            );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(
                chart,
                "Change Chart Music"
            );

            PausePlayback();

            chart.audioClip =
                newClip;

            currentTime = 0f;

            EditorUtility.SetDirty(
                chart
            );
        }

        EditorGUILayout.EndHorizontal();

        if (chart.audioClip == null)
        {
            EditorGUILayout.HelpBox(
                "������ ����Ǿ� ���� �ʽ��ϴ�.",
                MessageType.Warning
            );
        }
        else
        {
            EditorGUILayout.LabelField(
                "Loaded Song",
                chart.audioClip.name
            );

            EditorGUILayout.LabelField(
                "Length",
                FormatTime(
                    chart.audioClip.length
                )
            );
        }

        EditorGUI.BeginChangeCheck();

        float newBpm =
            EditorGUILayout.FloatField(
                "BPM",
                chart.bpm
            );

        float newOffset =
            EditorGUILayout.FloatField(
                "Offset",
                chart.offsetSeconds
            );

        float newTravelTime =
            EditorGUILayout.FloatField(
                "Travel Time",
                chart.travelTime
            );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(
                chart,
                "Change Chart Timing"
            );

            chart.bpm =
                Mathf.Max(
                    1f,
                    newBpm
                );

            chart.offsetSeconds =
                newOffset;

            chart.travelTime =
                Mathf.Max(
                    0.1f,
                    newTravelTime
                );

            EditorUtility.SetDirty(
                chart
            );
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // Transport
    // =========================================================

    private void DrawTransport()
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.toolbar
        );

        string playText =
            playing
            ? "Pause"
            : "Play";

        if (GUILayout.Button(
                playText,
                EditorStyles.toolbarButton,
                GUILayout.Width(60f)
            ))
        {
            if (playing)
            {
                PausePlayback();
            }
            else
            {
                StartPlayback();
            }
        }

        if (GUILayout.Button(
                "Stop",
                EditorStyles.toolbarButton,
                GUILayout.Width(50f)
            ))
        {
            StopPlayback();
        }

        float duration =
            GetChartDuration();

        EditorGUI.BeginChangeCheck();

        float newTime =
            EditorGUILayout.Slider(
                currentTime,
                0f,
                duration
            );

        if (EditorGUI.EndChangeCheck())
        {
            Seek(
                newTime
            );
        }

        GUILayout.Label(
            FormatTime(
                currentTime
            ),
            GUILayout.Width(85f)
        );

        followPlayhead =
            GUILayout.Toggle(
                followPlayhead,
                "Follow",
                EditorStyles.toolbarButton,
                GUILayout.Width(60f)
            );

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // Editor Tools
    // =========================================================

    private void DrawEditorTools()
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.toolbar
        );

        GUILayout.Label(
            "Note",
            GUILayout.Width(35f)
        );

        placementType =
            (ChartNoteType)
            EditorGUILayout.EnumPopup(
                placementType,
                GUILayout.Width(75f)
            );

        GUILayout.Space(8f);

        GUILayout.Label(
            "Snap",
            GUILayout.Width(35f)
        );

        int[] snapValues =
        {
            4,
            8,
            16,
            32
        };

        string[] snapLabels =
        {
            "1/4",
            "1/8",
            "1/16",
            "1/32"
        };

        int snapIndex =
            Array.IndexOf(
                snapValues,
                snapDenominator
            );

        if (snapIndex < 0)
            snapIndex = 2;

        snapIndex =
            EditorGUILayout.Popup(
                snapIndex,
                snapLabels,
                GUILayout.Width(55f)
            );

        snapDenominator =
            snapValues[
                snapIndex
            ];

        GUILayout.Space(8f);

        GUILayout.Label(
            "Zoom",
            GUILayout.Width(35f)
        );

        pixelsPerSecond =
            GUILayout.HorizontalSlider(
                pixelsPerSecond,
                60f,
                500f,
                GUILayout.Width(120f)
            );

        GUILayout.Label(
            Mathf.RoundToInt(
                pixelsPerSecond
            ).ToString(),
            GUILayout.Width(35f)
        );

        if (placementType ==
            ChartNoteType.Long)
        {
            GUILayout.Space(8f);

            GUILayout.Label(
                "Long",
                GUILayout.Width(30f)
            );

            defaultLongBeats =
                Mathf.Max(
                    0.25f,
                    EditorGUILayout.FloatField(
                        defaultLongBeats,
                        GUILayout.Width(45f)
                    )
                );

            GUILayout.Label(
                "beat",
                GUILayout.Width(30f)
            );
        }

        GUILayout.FlexibleSpace();

        if (chart != null)
        {
            GUILayout.Label(
                $"Notes : {chart.notes.Count}",
                GUILayout.Width(80f)
            );
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    // Timeline
    // =========================================================

    private void DrawVerticalTimeline()
    {
        float availableHeight =
            Mathf.Max(
                300f,
                position.height -
                245f
            );

        Rect fullRect =
            GUILayoutUtility.GetRect(
                0f,
                availableHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

        float usableWidth =
            fullRect.width -
            TIME_RULER_WIDTH -
            18f;

        laneWidth =
            Mathf.Max(
                MIN_LANE_WIDTH,
                usableWidth / 4f
            );

        float contentWidth =
            TIME_RULER_WIDTH +
            laneWidth * 4f;

        float duration =
            GetChartDuration();

        float contentHeight =
            HEADER_HEIGHT +
            duration *
            pixelsPerSecond +
            200f;

        Rect scrollRect =
            new Rect(
                fullRect.x,
                fullRect.y,
                fullRect.width,
                fullRect.height
            );

        Rect viewRect =
            new Rect(
                0f,
                0f,
                contentWidth,
                contentHeight
            );

        timelineViewportHeight =
            scrollRect.height;

        timelineScroll =
            GUI.BeginScrollView(
                scrollRect,
                timelineScroll,
                viewRect,
                true,
                true
            );

        DrawTimelineBackground(
            viewRect
        );

        DrawLaneHeaders();

        DrawVerticalGrid();

        DrawNotes();

        DrawPlayhead();

        HandleTimelineMouse();

        GUI.EndScrollView();
    }

    private void DrawTimelineBackground(
        Rect rect
    )
    {
        EditorGUI.DrawRect(
            rect,
            new Color(
                0.07f,
                0.07f,
                0.08f
            )
        );

        EditorGUI.DrawRect(
            new Rect(
                0f,
                0f,
                TIME_RULER_WIDTH,
                rect.height
            ),
            new Color(
                0.10f,
                0.10f,
                0.11f
            )
        );

        for (int lane = 0;
             lane < 4;
             lane++)
        {
            Rect laneRect =
                new Rect(
                    TIME_RULER_WIDTH +
                    lane * laneWidth,
                    HEADER_HEIGHT,
                    laneWidth,
                    rect.height -
                    HEADER_HEIGHT
                );

            Color bg =
                lane % 2 == 0
                ? new Color(
                    0.13f,
                    0.13f,
                    0.15f
                )
                : new Color(
                    0.10f,
                    0.10f,
                    0.12f
                );

            EditorGUI.DrawRect(
                laneRect,
                bg
            );

            EditorGUI.DrawRect(
                new Rect(
                    laneRect.x,
                    laneRect.y,
                    1f,
                    laneRect.height
                ),
                new Color(
                    0.3f,
                    0.3f,
                    0.33f
                )
            );
        }
    }

    private void DrawLaneHeaders()
    {
        GUI.Box(
            new Rect(
                0f,
                0f,
                TIME_RULER_WIDTH,
                HEADER_HEIGHT
            ),
            "TIME"
        );

        string[] laneNames =
        {
            "Q",
            "W",
            "O",
            "P"
        };

        for (int i = 0;
             i < 4;
             i++)
        {
            GUI.Box(
                new Rect(
                    TIME_RULER_WIDTH +
                    i * laneWidth,
                    0f,
                    laneWidth,
                    HEADER_HEIGHT
                ),
                laneNames[i]
            );
        }
    }

    // =========================================================
    // Grid
    // =========================================================

    private void DrawVerticalGrid()
    {
        if (chart == null)
            return;

        if (chart.bpm <= 0f)
            return;

        float step =
            GetSnapStep();

        if (step <= 0f)
            return;

        float visibleStartTime =
            Mathf.Max(
                0f,
                (
                    timelineScroll.y -
                    HEADER_HEIGHT
                ) /
                pixelsPerSecond
            );

        float visibleEndTime =
            Mathf.Max(
                0f,
                (
                    timelineScroll.y +
                    timelineViewportHeight
                ) /
                pixelsPerSecond
            );

        int startIndex =
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    visibleStartTime /
                    step
                ) - 2
            );

        int endIndex =
            Mathf.CeilToInt(
                visibleEndTime /
                step
            ) + 2;

        float beatLength =
            60f /
            chart.bpm;

        for (int i = startIndex;
             i <= endIndex;
             i++)
        {
            float time =
                i * step;

            float y =
                HEADER_HEIGHT +
                time *
                pixelsPerSecond;

            float beatPosition =
                Mathf.Repeat(
                    time -
                    chart.offsetSeconds,
                    beatLength
                );

            bool fullBeat =
                beatPosition <
                step * 0.25f ||
                beatLength -
                beatPosition <
                step * 0.25f;

            Color lineColor =
                fullBeat
                ? new Color(
                    0.55f,
                    0.55f,
                    0.60f,
                    0.65f
                )
                : new Color(
                    0.28f,
                    0.28f,
                    0.32f,
                    0.28f
                );

            EditorGUI.DrawRect(
                new Rect(
                    TIME_RULER_WIDTH,
                    y,
                    laneWidth * 4f,
                    fullBeat ? 2f : 1f
                ),
                lineColor
            );

            if (fullBeat)
            {
                GUI.Label(
                    new Rect(
                        3f,
                        y - 9f,
                        TIME_RULER_WIDTH - 5f,
                        18f
                    ),
                    FormatShortTime(
                        time
                    ),
                    EditorStyles.miniLabel
                );
            }
        }
    }

    // =========================================================
    // Notes
    // =========================================================

    private void DrawNotes()
    {
        if (chart == null)
            return;

        for (int i = 0;
             i < chart.notes.Count;
             i++)
        {
            ChartNoteData note =
                chart.notes[i];

            Rect rect =
                GetNoteRect(
                    note
                );

            Color noteColor =
                GetLaneColor(
                    note.lane
                );

            if (note.type ==
                ChartNoteType.Error)
            {
                noteColor =
                    new Color(
                        0.95f,
                        0.35f,
                        0.65f
                    );
            }
            else if (note.type ==
                     ChartNoteType.Hit)
            {
                noteColor =
                    new Color(
                        0.95f,
                        0.22f,
                        0.22f
                    );
            }

            if (i ==
                selectedNoteIndex)
            {
                EditorGUI.DrawRect(
                    new Rect(
                        rect.x - 3f,
                        rect.y - 3f,
                        rect.width + 6f,
                        rect.height + 6f
                    ),
                    Color.white
                );
            }

            EditorGUI.DrawRect(
                rect,
                noteColor
            );

            if (note.type ==
                ChartNoteType.Long)
            {
                GUI.Label(
                    rect,
                    "LONG",
                    EditorStyles.centeredGreyMiniLabel
                );
            }
            else if (
                note.type ==
                ChartNoteType.Error)
            {
                GUI.Label(
                    rect,
                    "ERR",
                    EditorStyles.centeredGreyMiniLabel
                );
            }
            else if (
                note.type ==
                ChartNoteType.Hit)
            {
                GUI.Label(
                    rect,
                    "HIT",
                    EditorStyles.centeredGreyMiniLabel
                );
            }
        }
    }

    private Rect GetNoteRect(
        ChartNoteData note
    )
    {
        int laneIndex =
            LaneToIndex(
                note.lane
            );

        float x =
            TIME_RULER_WIDTH +
            laneIndex *
            laneWidth +
            8f;

        float y =
            HEADER_HEIGHT +
            note.hitTime *
            pixelsPerSecond;

        float width =
            laneWidth -
            16f;

        float height =
            12f;

        if (note.type ==
            ChartNoteType.Long)
        {
            height =
                Mathf.Max(
                    20f,
                    note.duration *
                    pixelsPerSecond
                );
        }

        return new Rect(
            x,
            y - 6f,
            width,
            height
        );
    }

    // =========================================================
    // Mouse
    // =========================================================

    private void HandleTimelineMouse()
    {
        Event e =
            Event.current;

        Vector2 mouse =
            e.mousePosition;

        if (e.type ==
            EventType.MouseDown)
        {
            int noteIndex =
                FindNoteAtPosition(
                    mouse
                );

            if (noteIndex >= 0)
            {
                if (e.button == 1)
                {
                    DeleteNote(
                        noteIndex
                    );

                    e.Use();
                    return;
                }

                if (e.button == 0)
                {
                    selectedNoteIndex =
                        noteIndex;

                    draggingNote =
                        true;

                    e.Use();
                    return;
                }
            }

            if (e.button == 0 &&
                IsInsideLaneArea(
                    mouse
                ))
            {
                AddNoteAt(
                    mouse
                );

                e.Use();
                return;
            }

            if (e.button == 0 &&
                mouse.x <
                TIME_RULER_WIDTH &&
                mouse.y >
                HEADER_HEIGHT)
            {
                float clickedTime =
                    (
                        mouse.y -
                        HEADER_HEIGHT
                    ) /
                    pixelsPerSecond;

                Seek(
                    clickedTime
                );

                e.Use();
            }
        }

        if (e.type ==
            EventType.MouseDrag &&
            draggingNote &&
            selectedNoteIndex >= 0 &&
            selectedNoteIndex <
            chart.notes.Count)
        {
            MoveSelectedNote(
                mouse
            );

            e.Use();
        }

        if (e.type ==
            EventType.MouseUp &&
            draggingNote)
        {
            draggingNote =
                false;

            SortNotes();

            e.Use();
        }
    }

    private bool IsInsideLaneArea(
        Vector2 mouse
    )
    {
        return
            mouse.y >= HEADER_HEIGHT &&
            mouse.x >= TIME_RULER_WIDTH &&
            mouse.x <=
            TIME_RULER_WIDTH +
            laneWidth * 4f;
    }

    private int FindNoteAtPosition(
        Vector2 mouse
    )
    {
        for (int i =
                 chart.notes.Count - 1;
             i >= 0;
             i--)
        {
            if (GetNoteRect(
                    chart.notes[i]
                ).Contains(mouse))
            {
                return i;
            }
        }

        return -1;
    }

    private void AddNoteAt(
        Vector2 mouse
    )
    {
        int laneIndex =
            Mathf.FloorToInt(
                (
                    mouse.x -
                    TIME_RULER_WIDTH
                ) /
                laneWidth
            );

        laneIndex =
            Mathf.Clamp(
                laneIndex,
                0,
                3
            );

        float rawTime =
            (
                mouse.y -
                HEADER_HEIGHT
            ) /
            pixelsPerSecond;

        ChartNoteData note =
            new ChartNoteData();

        note.lane =
            IndexToLane(
                laneIndex
            );

        note.type =
            placementType;

        note.hitTime =
            SnapTime(
                rawTime
            );

        if (placementType ==
            ChartNoteType.Long)
        {
            float beatLength =
                60f /
                chart.bpm;

            note.duration =
                beatLength *
                defaultLongBeats;
        }
        else
        {
            note.duration = 0f;
        }

        Undo.RecordObject(
            chart,
            "Add Note"
        );

        chart.notes.Add(
            note
        );

        SortNotes();

        selectedNoteIndex =
            chart.notes.IndexOf(
                note
            );

        EditorUtility.SetDirty(
            chart
        );
    }

    private void MoveSelectedNote(
        Vector2 mouse
    )
    {
        ChartNoteData note =
            chart.notes[
                selectedNoteIndex
            ];

        Undo.RecordObject(
            chart,
            "Move Note"
        );

        int laneIndex =
            Mathf.FloorToInt(
                (
                    mouse.x -
                    TIME_RULER_WIDTH
                ) /
                laneWidth
            );

        laneIndex =
            Mathf.Clamp(
                laneIndex,
                0,
                3
            );

        float rawTime =
            (
                mouse.y -
                HEADER_HEIGHT
            ) /
            pixelsPerSecond;

        note.lane =
            IndexToLane(
                laneIndex
            );

        note.hitTime =
            SnapTime(
                rawTime
            );

        EditorUtility.SetDirty(
            chart
        );

        Repaint();
    }

    private void DeleteNote(
        int index
    )
    {
        Undo.RecordObject(
            chart,
            "Delete Note"
        );

        chart.notes.RemoveAt(
            index
        );

        selectedNoteIndex =
            -1;

        EditorUtility.SetDirty(
            chart
        );

        Repaint();
    }

    private void HandleDeleteKey()
    {
        Event e =
            Event.current;

        if (e.type !=
            EventType.KeyDown)
        {
            return;
        }

        if (e.keyCode !=
                KeyCode.Delete &&
            e.keyCode !=
                KeyCode.Backspace)
        {
            return;
        }

        if (selectedNoteIndex < 0)
            return;

        DeleteNote(
            selectedNoteIndex
        );

        e.Use();
    }

    // =========================================================
    // Playhead
    // =========================================================

    private void DrawPlayhead()
    {
        float y =
            HEADER_HEIGHT +
            currentTime *
            pixelsPerSecond;

        EditorGUI.DrawRect(
            new Rect(
                0f,
                y,
                TIME_RULER_WIDTH +
                laneWidth * 4f,
                2f
            ),
            new Color(
                1f,
                0.28f,
                0.28f,
                1f
            )
        );

        GUI.Label(
            new Rect(
                3f,
                y + 2f,
                TIME_RULER_WIDTH,
                18f
            ),
            FormatTime(
                currentTime
            ),
            EditorStyles.miniBoldLabel
        );
    }

    // =========================================================
    // Playback
    // =========================================================

    private void StartPlayback()
    {
        if (chart == null ||
            chart.audioClip == null)
        {
            return;
        }

        float duration =
            GetChartDuration();

        if (currentTime >= duration)
        {
            currentTime = 0f;
        }

        playing =
            true;

        playbackStartEditorTime =
            EditorApplication.timeSinceStartup -
            currentTime;

        AudioPreviewUtility.Play(
            chart.audioClip,
            currentTime
        );
    }

    private void PausePlayback()
    {
        playing =
            false;

        StopPlaybackAudio();

        Repaint();
    }

    private void StopPlayback()
    {
        playing =
            false;

        currentTime =
            0f;

        timelineScroll.y =
            0f;

        StopPlaybackAudio();

        Repaint();
    }

    private void Seek(
        float time
    )
    {
        currentTime =
            Mathf.Clamp(
                time,
                0f,
                GetChartDuration()
            );

        if (playing)
        {
            StopPlaybackAudio();

            playbackStartEditorTime =
                EditorApplication.timeSinceStartup -
                currentTime;

            AudioPreviewUtility.Play(
                chart.audioClip,
                currentTime
            );
        }

        Repaint();
    }

    private void EditorUpdate()
    {
        if (!playing ||
            chart == null)
        {
            return;
        }

        currentTime =
            (float)(
                EditorApplication.timeSinceStartup -
                playbackStartEditorTime
            );

        float duration =
            GetChartDuration();

        if (currentTime >=
            duration)
        {
            currentTime =
                duration;

            playing =
                false;

            StopPlaybackAudio();
        }

        if (followPlayhead)
        {
            FollowCurrentTime();
        }

        Repaint();
    }

    private void FollowCurrentTime()
    {
        float playheadY =
            HEADER_HEIGHT +
            currentTime *
            pixelsPerSecond;

        float visibleBottom =
            timelineScroll.y +
            timelineViewportHeight -
            100f;

        if (playheadY >
            visibleBottom)
        {
            timelineScroll.y =
                Mathf.Max(
                    0f,
                    playheadY -
                    timelineViewportHeight *
                    0.3f
                );
        }
    }

    private void StopPlaybackAudio()
    {
        AudioPreviewUtility.Stop();
    }

    // =========================================================
    // Utility
    // =========================================================

    private float SnapTime(
        float time
    )
    {
        float step =
            GetSnapStep();

        if (step <= 0f)
            return time;

        float offset =
            chart.offsetSeconds;

        return Mathf.Max(
            0f,
            offset +
            Mathf.Round(
                (
                    time -
                    offset
                ) /
                step
            ) *
            step
        );
    }

    private float GetSnapStep()
    {
        if (chart == null ||
            chart.bpm <= 0f)
        {
            return 0.25f;
        }

        float beatLength =
            60f /
            chart.bpm;

        return beatLength *
               (
                   4f /
                   snapDenominator
               );
    }

    private float GetChartDuration()
    {
        if (chart == null)
            return 30f;

        if (chart.audioClip != null)
        {
            return chart.audioClip.length;
        }

        if (chart.notes.Count > 0)
        {
            return Mathf.Max(
                30f,
                chart.notes.Max(
                    note =>
                        note.hitTime +
                        note.duration
                ) + 5f
            );
        }

        return 30f;
    }

    private void SortNotes()
    {
        if (chart == null)
            return;

        ChartNoteData selected =
            null;

        if (selectedNoteIndex >= 0 &&
            selectedNoteIndex <
            chart.notes.Count)
        {
            selected =
                chart.notes[
                    selectedNoteIndex
                ];
        }

        chart.notes =
            chart.notes
            .OrderBy(
                note =>
                    note.hitTime
            )
            .ThenBy(
                note =>
                    LaneToIndex(
                        note.lane
                    )
            )
            .ToList();

        if (selected != null)
        {
            selectedNoteIndex =
                chart.notes.IndexOf(
                    selected
                );
        }
    }

    private int LaneToIndex(
        NoteLane lane
    )
    {
        switch (lane)
        {
            case NoteLane.Q:
                return 0;

            case NoteLane.W:
                return 1;

            case NoteLane.O:
                return 2;

            case NoteLane.P:
                return 3;
        }

        return 0;
    }

    private NoteLane IndexToLane(
        int index
    )
    {
        switch (index)
        {
            case 0:
                return NoteLane.Q;

            case 1:
                return NoteLane.W;

            case 2:
                return NoteLane.O;

            case 3:
                return NoteLane.P;
        }

        return NoteLane.Q;
    }

    private Color GetLaneColor(
        NoteLane lane
    )
    {
        switch (lane)
        {
            case NoteLane.Q:
                return new Color(
                    0.35f,
                    0.65f,
                    1f
                );

            case NoteLane.W:
                return new Color(
                    0.4f,
                    0.85f,
                    0.75f
                );

            case NoteLane.O:
                return new Color(
                    0.85f,
                    0.55f,
                    1f
                );

            case NoteLane.P:
                return new Color(
                    1f,
                    0.55f,
                    0.65f
                );
        }

        return Color.white;
    }

    private string FormatTime(
        float time
    )
    {
        int minutes =
            Mathf.FloorToInt(
                time / 60f
            );

        float seconds =
            time -
            minutes * 60f;

        return
            $"{minutes:00}:{seconds:00.000}";
    }

    private string FormatShortTime(
        float time
    )
    {
        int minutes =
            Mathf.FloorToInt(
                time / 60f
            );

        int seconds =
            Mathf.FloorToInt(
                time -
                minutes * 60f
            );

        return
            $"{minutes:00}:{seconds:00}";
    }
}

// =============================================================
// Audio Preview
// =============================================================

internal static class AudioPreviewUtility
{
    private static Type audioUtilType;

    private static Type AudioUtilType
    {
        get
        {
            if (audioUtilType != null)
                return audioUtilType;

            Assembly assembly =
                typeof(AudioImporter)
                .Assembly;

            audioUtilType =
                assembly.GetType(
                    "UnityEditor.AudioUtil"
                );

            return audioUtilType;
        }
    }

    public static void Play(
        AudioClip clip,
        float startTime
    )
    {
        if (clip == null)
            return;

        Stop();

        MethodInfo method =
            FindMethod(
                "PlayPreviewClip",
                "PlayClip"
            );

        if (method == null)
        {
            Debug.LogWarning(
                "Audio Preview API�� ã�� ���߽��ϴ�."
            );

            return;
        }

        int startSample =
            Mathf.Clamp(
                Mathf.RoundToInt(
                    startTime *
                    clip.frequency
                ),
                0,
                Mathf.Max(
                    0,
                    clip.samples - 1
                )
            );

        ParameterInfo[] parameters =
            method.GetParameters();

        try
        {
            if (parameters.Length == 3)
            {
                method.Invoke(
                    null,
                    new object[]
                    {
                        clip,
                        startSample,
                        false
                    }
                );
            }
            else if (parameters.Length == 2)
            {
                method.Invoke(
                    null,
                    new object[]
                    {
                        clip,
                        startSample
                    }
                );
            }
            else
            {
                method.Invoke(
                    null,
                    new object[]
                    {
                        clip
                    }
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "���� �̸���� ����: " +
                e.Message
            );
        }
    }

    public static void Stop()
    {
        MethodInfo method =
            FindMethod(
                "StopAllPreviewClips",
                "StopAllClips"
            );

        if (method == null)
            return;

        try
        {
            method.Invoke(
                null,
                null
            );
        }
        catch
        {
        }
    }

    private static MethodInfo FindMethod(
        params string[] names
    )
    {
        if (AudioUtilType == null)
            return null;

        foreach (string name
                 in names)
        {
            MethodInfo method =
                AudioUtilType.GetMethod(
                    name,
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            if (method != null)
                return method;
        }

        return null;
    }
}