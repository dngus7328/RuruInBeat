#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using LuluInBeat.MouseMode.ChartSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace LuluInBeat.MouseMode.ChartSystem.EditorTools
{
    public class MouseModeChartEditorWindow : EditorWindow
    {
        private enum Tool { Select, Left, Right, Wheel, Attack }
        private static readonly string[] ToolLabels =
            { "선택", "좌클릭", "우클릭", "휠", "탄막 이벤트" };

        [SerializeField] private MouseModeChartAsset chartAsset;
        private Tool tool = Tool.Left;
        private float currentBeat = 4f;
        private float snap = 0.25f;
        private int selectedNote = -1;
        private int selectedAttack = -1;
        private Vector2 listScroll;
        private Vector2 windowScroll;
        private const string LastChartGuidKey = "LuluMouseChart.LastChartGuid.v2";
        private bool previewPlaying;
        private double fallbackPreviewStart;
        private float previewStartSeconds;
        private Rect stageRect;
        // 편집 화면의 위치 점 대신 실제 판정 원에 가까운 모습을 미리 보여준다.
        // 이 미리보기는 게임을 실행하지 않으며, 노트/접근 원/휠 경로만 시각화한다.
        [SerializeField] private bool gameplayNotePreview;
        private MouseModeChartPlayer previewSource;
        private GUIStyle previewLetterStyle;
        private GUIStyle previewSmallStyle;

        // 제작용 타임라인: 파형, 박자 눈금, 노트 및 탄막 트랙
        private float viewStartBeat = 0f;
        private float pixelsPerBeat = 30f;
        private Rect timelineRect;
        private Rect timelineGraph;
        private int timelineDragNote = -1;
        private int timelineDragAttack = -1;
        private int stageDragNote = -1;
        private int stageDragPathPoint = -1;
        private Vector2 stageDragOffset;
        private bool stageIsDragging;
        private float[] waveform;
        private AudioClip waveformClip;
        private bool waveformAvailable;
        private static readonly Color LeftNoteColor = new Color(0.69f, 0.55f, 0.82f);
        private static readonly Color RightNoteColor = new Color(0.43f, 0.69f, 0.72f);
        private static readonly Color WheelNoteColor = new Color(0.82f, 0.69f, 0.44f);
        private static readonly Color AttackColor = new Color(0.9f, 0.46f, 0.5f);


        private MouseModeChartData Data
        {
            get { return chartAsset != null ? chartAsset.data : null; }
        }

        [MenuItem("Tools/Lulu In Beat/Mouse Mode Chart Editor")]
        public static void Open()
        {
            MouseModeChartEditorWindow window =
                GetWindow<MouseModeChartEditorWindow>("Mouse Chart Editor v3");
            window.minSize = new Vector2(630f, 570f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += EditorTick;
            if (chartAsset == null)
            {
                string guid = EditorPrefs.GetString(LastChartGuidKey, "");
                if (!string.IsNullOrEmpty(guid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                        chartAsset = AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(path);
                }
            }
        }

        private void OnDisable()
        {
            StopPreview();
            EditorApplication.update -= EditorTick;
        }

        private void EditorTick()
        {
            if (!previewPlaying || chartAsset == null || chartAsset.song == null)
                return;

            float time = MouseModeEditorAudioPreview.CurrentSeconds(chartAsset.song);
            if (time < 0f)
                time = previewStartSeconds +
                    (float)(EditorApplication.timeSinceStartup - fallbackPreviewStart);

            currentBeat = Mathf.Max(0f,
                (time - Data.beatZeroSeconds) * Mathf.Max(1f, Data.bpm) / 60f);
            float shownBeats = timelineGraph.width > 0f ? timelineGraph.width / pixelsPerBeat : 14f;
            if (currentBeat > viewStartBeat + Mathf.Max(2f, shownBeats - 2f) || currentBeat < viewStartBeat)
                viewStartBeat = Mathf.Max(0f, currentBeat - 2f);
            if (time >= chartAsset.song.length)
                StopPreview();
            Repaint();
        }

        private void OnGUI()
        {
            HandleGlobalShortcuts(Event.current);
            DrawTopBar();
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            if (chartAsset == null)
            {
                DrawWelcome();
                EditorGUILayout.EndScrollView();
                return;
            }

            if (chartAsset.data == null)
                chartAsset.data = new MouseModeChartData();
            Data.EnsureLists();

            GUILayout.Space(6);
            GUILayout.Label("1. 곡 정보", EditorStyles.boldLabel);
            DrawChartSettings();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("JSON 가져오기", GUILayout.Height(27f)))
                ImportJson();
            if (GUILayout.Button("JSON 내보내기", GUILayout.Height(27f)))
                ExportJson();
            GUILayout.EndHorizontal();

            DrawAttackDataStatus();

            GUILayout.Space(5);
            GUILayout.Label("2. 음악 파형 / 채보 타임라인", EditorStyles.boldLabel);
            DrawTransport();
            DrawTimeline();

            GUILayout.Space(5);
            GUILayout.Label("3. 화면 미리보기 — 모든 위치에 배치 가능", EditorStyles.boldLabel);
            tool = (Tool)GUILayout.Toolbar((int)tool, ToolLabels);
            GUILayout.BeginHorizontal();
            bool nextPreview = GUILayout.Toggle(gameplayNotePreview,
                gameplayNotePreview ? "● 노트 재생 미리보기 ON" : "○ 노트 재생 미리보기 OFF",
                EditorStyles.toolbarButton, GUILayout.Width(192f));
            if (nextPreview != gameplayNotePreview)
            {
                gameplayNotePreview = nextPreview;
                Repaint();
            }
            GUILayout.Label(gameplayNotePreview
                ? "현재 박자의 노트와 레이저/폭발탄 표시 / 편집 잠금"
                : "편집 화면: 위치를 클릭해서 노트와 이벤트 배치",
                EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            if (position.width < 890f)
            {
                Rect stageArea = GUILayoutUtility.GetRect(180f, 310f,
                    GUILayout.ExpandWidth(true));
                DrawStage(stageArea);
                GUILayout.Space(8);
                DrawSelectedInspector();
            }
            else
            {
                GUILayout.BeginHorizontal();
                Rect stageArea = GUILayoutUtility.GetRect(280f, 340f,
                    GUILayout.ExpandWidth(true));
                DrawStage(stageArea);
                GUILayout.BeginVertical(GUILayout.Width(270f));
                DrawSelectedInspector();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(3);
            EditorGUILayout.LabelField("단축키: Space 미리듣기  |  1 선택  2 좌클릭  3 우클릭  4 휠  5 탄막  |  Delete 삭제  |  Ctrl+S 저장", EditorStyles.miniLabel);
            DrawEntries();
            GUILayout.Space(12);
            EditorGUILayout.HelpBox(
                "사용법: 타임라인 빈 곳 클릭=시점 이동 / 노트 점 드래그=박자 변경 / " +
                "Ctrl+휠=확대 / 휠=시간 스크롤. " +
                "미리보기에서는 노트를 드래그하여 위치를 바꿀 수 있어요. " +
                "[미리듣기]는 편집 중 음악만 재생해요. " +
                "[▶ 게임에서 테스트]를 누르면 현재 Mouse 씬에서 " +
                "노트·판정·피노를 함께 테스트할 수 있어요.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawTopBar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("＋ 새 채보", EditorStyles.toolbarButton, GUILayout.Width(85f)))
                CreateChart(false);

            MouseModeChartAsset selected = (MouseModeChartAsset)
                EditorGUILayout.ObjectField(chartAsset, typeof(MouseModeChartAsset), false,
                    GUILayout.MinWidth(90f));
            if (selected != chartAsset)
            {
                StopPreview();
                waveformClip = null;
                waveform = null;
                viewStartBeat = 0f;
                chartAsset = selected;
                selectedNote = -1;
                selectedAttack = -1;
                currentBeat = 4f;
                RememberChart();
            }

            GUI.enabled = chartAsset != null;
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                SaveChart();
            if (GUILayout.Button("▶ 게임에서 테스트", EditorStyles.toolbarButton, GUILayout.Width(115f)))
                StartGameTest();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawWelcome()
        {
            GUILayout.Space(22f);
            GUILayout.Label("루루 인 비트 · 마우스 채보 에디터", EditorStyles.largeLabel);
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "시작하기: 채보를 하나 만들면 곡 선택, BPM, 노트 배치, " +
                "게임 테스트까지 이 창에서 이어서 할 수 있어요. " +
                "기존 4레인 채보에는 영향을 주지 않아요.", MessageType.Info);
            GUILayout.Space(12f);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("처음 만드는 채보", EditorStyles.boldLabel);
            GUILayout.Label("빈 채보를 만들고 음악을 연결해서 직접 노트를 찍어요.",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6f);
            if (GUILayout.Button("1. 새 빈 채보 만들기", GUILayout.Height(44f)))
                CreateChart(false);
            GUILayout.Space(10f);
            GUILayout.Label("먼저 게임 플레이를 확인하고 싶다면", EditorStyles.boldLabel);
            GUILayout.Label("시험용 노트가 들어 있는 채보를 바로 만들어요. " +
                "음악이 없어도 무음으로 플레이 테스트가 가능해요.",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(6f);
            if (GUILayout.Button("▶ 샘플 채보 만들기", GUILayout.Height(44f)))
                CreateChart(true);
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "이미 만든 채보가 있다면 위쪽의 오브젝트 선택 칸에 " +
                "MouseModeChartAsset을 넣어 주세요. 마지막 사용 채보는 자동으로 기억해요.",
                MessageType.None);
        }

        private void RememberChart()
        {
            string path = chartAsset != null ? AssetDatabase.GetAssetPath(chartAsset) : "";
            string guid = !string.IsNullOrEmpty(path) ? AssetDatabase.AssetPathToGUID(path) : "";
            EditorPrefs.SetString(LastChartGuidKey, guid);
        }

        private void SaveChart()
        {
            if (chartAsset == null) return;
            EditorUtility.SetDirty(chartAsset);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("채보 저장 완료"));
        }

        private void DrawChartSettings()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            AudioClip chosenSong = (AudioClip)EditorGUILayout.ObjectField(
                "음악", chartAsset.song, typeof(AudioClip), false);
            string chosenId = EditorGUILayout.TextField("곡 ID", Data.songId);
            float chosenBpm = Mathf.Max(1f, EditorGUILayout.FloatField("BPM", Data.bpm));
            float chosenOffset = EditorGUILayout.FloatField(
                "0박 오프셋 (초)", Data.beatZeroSeconds);
            float chosenApproach = Mathf.Max(0.1f, EditorGUILayout.FloatField(
                "접근 원 표시 시간 (초)", Data.approachSeconds));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(chartAsset, "Edit Mouse Chart Settings");
                chartAsset.song = chosenSong;
                Data.songId = chosenId;
                Data.bpm = chosenBpm;
                Data.beatZeroSeconds = chosenOffset;
                Data.approachSeconds = chosenApproach;
                EditorUtility.SetDirty(chartAsset);
                StopPreview();
                if (chartAsset.song != waveformClip) { waveform = null; waveformClip = null; }
            }
            if (chartAsset.song == null)
                EditorGUILayout.HelpBox("음악이 연결되지 않았어요. 음악 파일을 [음악] 칸에 넣거나, " +
                    "노트부터 찍어서 무음으로 테스트할 수 있어요.", MessageType.Warning);
            GUILayout.EndVertical();
        }

        // These are real serialized attack entries, not editor-only preview objects.
        private void DrawAttackDataStatus()
        {
            int lasers = 0, bursts = 0, rains = 0;
            foreach (MouseModeChartAttack a in Data.attacks)
            {
                if (a == null) continue;
                MouseModeBulletDirector.AttackSpec spec = MouseModeBulletDirector.ParseAttack(a.patternId);
                if (spec.kind == MouseModeBulletDirector.AttackKind.Laser) lasers++;
                if (spec.kind == MouseModeBulletDirector.AttackKind.Burst) bursts++;
                if (spec.kind == MouseModeBulletDirector.AttackKind.Rain) rains++;
            }
            EditorGUILayout.HelpBox("실제 채보 공격 데이터: " + Data.attacks.Count +
                "개 | 레이저 " + lasers + "개 | 폭발탄 " + bursts +
                "개 | 안무형 탄막 " + rains + "개. " +
                "이 값이 0이면 게임에서 해당 공격이 나오지 않습니다. " +
                "JSON 파일을 프로젝트에 복사하는 것만으로는 현재 채보에 적용되지 않습니다.",
                Data.attacks.Count == 0 ? MessageType.Warning : MessageType.Info);
            if (GUILayout.Button("현재 채보에 레이저 + 폭발탄 실제 이벤트 추가 (현재 박자 + 2 / + 10)"))
            {
                if (EditorUtility.DisplayDialog("채보에 실제 탄막 이벤트 추가",
                    "현재 채보의 공격 목록에 레이저 1개와 폭발탄 1개를 추가합니다. " +
                    "미리보기 전용이 아니며 저장과 게임 테스트에 적용됩니다. 계속할까요?",
                    "추가", "취소"))
                {
                    Undo.RecordObject(chartAsset, "Add Real Laser and Burst Attacks");
                    float start = Mathf.Max(0f, SnappedBeat() + 2f);
                    Data.attacks.Add(new MouseModeChartAttack
                    {
                        beat = start,
                        patternId = "laser|x=0.50|angle=0|width=0.038|warning=2.3|duration=1.2"
                    });
                    Data.attacks.Add(new MouseModeChartAttack
                    {
                        beat = start + 8f,
                        patternId = "burst|x=0.58|y=0.70|radius=0.40|count=12|warning=2.1|duration=2.1"
                    });
                    EditorUtility.SetDirty(chartAsset);
                    ShowNotification(new GUIContent("실제 탄막 이벤트 2개 추가됨 — 저장해 주세요"));
                    Repaint();
                }
            }
            if (GUILayout.Button("안무형 탄막 추가 (현재 박자부터 6박)"))
                AddRainAttackAtCurrentBeat();
        }

        // Adds actual attack data to MouseModeChartAsset. Never a preview-only marker.
        private void AddRainAttackAtCurrentBeat()
        {
            Undo.RecordObject(chartAsset, "Add Real Rain Barrage");
            MouseModeBulletDirector.AttackSpec info = new MouseModeBulletDirector.AttackSpec
            {
                kind = MouseModeBulletDirector.AttackKind.Rain,
                x = .5f,
                drift = .10f,
                rows = 7,
                gap = 3,
                style = 0,
                warning = 1.5f,
                duration = 6.0f
            };
            Data.attacks.Add(new MouseModeChartAttack
            {
                beat = Mathf.Max(0f, SnappedBeat()),
                patternId = MouseModeBulletDirector.EncodeAttack(info)
            });
            EditorUtility.SetDirty(chartAsset);
            ShowNotification(new GUIContent("안무형 탄막이 채보에 추가됨"));
        }

        private void DrawTransport()
        {
            float maxBeat = chartAsset.song == null ? 128f :
                Mathf.Max(16f, (chartAsset.song.length - Data.beatZeroSeconds) *
                    Data.bpm / 60f);
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("처음", EditorStyles.toolbarButton, GUILayout.Width(33f)))
                SeekBeat(0f);
            if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(26f)))
                SeekBeat(currentBeat - 1f);
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(26f)))
                SeekBeat(currentBeat + 1f);
            GUI.enabled = chartAsset.song != null;
            if (GUILayout.Button(previewPlaying ? "정지" : "미리듣기",
                EditorStyles.toolbarButton, GUILayout.Width(74)))
            {
                if (previewPlaying) StopPreview(); else StartPreview();
            }
            GUI.enabled = true;
            GUILayout.Label("현재 박자", GUILayout.Width(62));
            float changedBeat = EditorGUILayout.FloatField(currentBeat, GUILayout.Width(66));
            if (!Mathf.Approximately(changedBeat, currentBeat))
                SeekBeat(changedBeat);
            float sliderBeat = GUILayout.HorizontalSlider(currentBeat, 0f,
                maxBeat, GUILayout.MinWidth(80));
            if (!Mathf.Approximately(sliderBeat, currentBeat))
                SeekBeat(sliderBeat);
            GUILayout.Label("스냅", GUILayout.Width(30));
            int snapIndex = snap == 1f ? 0 : snap == 0.5f ? 1 :
                snap == 0.25f ? 2 : snap == 0.125f ? 3 : 4;
            int newSnapIndex = EditorGUILayout.Popup(snapIndex,
                new[] { "1박", "1/2박", "1/4박", "1/8박", "자유" },
                EditorStyles.toolbarPopup, GUILayout.Width(65));
            snap = new[] { 1f, 0.5f, 0.25f, 0.125f, 0f }[newSnapIndex];
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("타임라인 확대", GUILayout.Width(90f));
            float changedZoom = GUILayout.HorizontalSlider(pixelsPerBeat, 8f, 100f, GUILayout.Width(180f));
            if (Mathf.Abs(changedZoom - pixelsPerBeat) > 0.01f) pixelsPerBeat = changedZoom;
            GUILayout.Label(pixelsPerBeat.ToString("0") + " px/박", EditorStyles.miniLabel, GUILayout.Width(75f));
            if (GUILayout.Button("◀ 4박", EditorStyles.miniButton, GUILayout.Width(56f)))
                viewStartBeat = Mathf.Max(0f, viewStartBeat - 4f);
            if (GUILayout.Button("4박 ▶", EditorStyles.miniButton, GUILayout.Width(56f)))
                viewStartBeat += 4f;
            if (GUILayout.Button("현재 박자 보기", EditorStyles.miniButton, GUILayout.Width(95f)))
                viewStartBeat = Mathf.Max(0f, currentBeat - 2f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void SeekBeat(float beat)
        {
            if (previewPlaying) StopPreview();
            currentBeat = Mathf.Max(0f, beat);
            float shownBeats = timelineGraph.width > 0f ? timelineGraph.width / pixelsPerBeat : 12f;
            if (currentBeat < viewStartBeat || currentBeat > viewStartBeat + shownBeats)
                viewStartBeat = Mathf.Max(0f, currentBeat - 2f);
            Repaint();
        }

        private float SnappedBeat()
        {
            return snap <= 0f ? Mathf.Max(0f, currentBeat) :
                Mathf.Max(0f, Mathf.Round(currentBeat / snap) * snap);
        }

        // 타임라인은 게임 재생기와 동일한 박자 데이터 단위를 사용한다.
        private float SongEndBeat()
        {
            float end = chartAsset.song != null
                ? Mathf.Max(16f, (chartAsset.song.length - Data.beatZeroSeconds) * Data.bpm / 60f)
                : 64f;
            foreach (MouseModeChartNote n in Data.notes)
                if (n != null) end = Mathf.Max(end, (n.kind == MouseModeNoteKind.Wheel ? n.endBeat : n.beat) + 8f);
            foreach (MouseModeChartAttack a in Data.attacks)
                if (a != null) end = Mathf.Max(end, a.beat + 8f);
            return end;
        }

        private float TimelineX(float beat) { return timelineGraph.x + (beat - viewStartBeat) * pixelsPerBeat; }
        private float XToBeat(float x) { return Mathf.Max(0f, viewStartBeat + (x - timelineGraph.x) / pixelsPerBeat); }
        private float SnapBeat(float value)
        {
            return Mathf.Max(0f, snap <= 0f ? value : Mathf.Round(value / snap) * snap);
        }

        private void DrawTimeline()
        {
            timelineRect = GUILayoutUtility.GetRect(10f, 215f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(timelineRect, new Color(0.115f, 0.12f, 0.15f));
            timelineGraph = new Rect(timelineRect.x + 64f, timelineRect.y + 1f,
                Mathf.Max(1f, timelineRect.width - 69f), timelineRect.height - 7f);

            // 좌측 라벨 및 트랙
            string[] labels = { "WAVE", "L", "R", "휠", "탄막" };
            float[] y = { timelineGraph.y + 48f, timelineGraph.y + 112f,
                timelineGraph.y + 135f, timelineGraph.y + 158f, timelineGraph.y + 181f };
            for (int i = 0; i < labels.Length; i++)
            {
                GUI.Label(new Rect(timelineRect.x + 5f, y[i] - 8f, 56f, 18f),
                    labels[i], EditorStyles.miniBoldLabel);
            }
            EditorGUI.DrawRect(new Rect(timelineGraph.x, timelineGraph.y + 17f,
                timelineGraph.width, 78f), new Color(0.17f, 0.18f, 0.22f));
            for (int i = 0; i < 4; i++)
            {
                EditorGUI.DrawRect(new Rect(timelineGraph.x, y[i + 1] - 10f,
                    timelineGraph.width, 21f), i % 2 == 0 ?
                    new Color(0.19f, 0.19f, 0.23f) : new Color(0.16f, 0.17f, 0.20f));
            }

            EnsureWaveform();
            Handles.BeginGUI();
            Handles.color = new Color(0.68f, 0.72f, 0.79f, 0.72f);
            if (waveformAvailable && waveform != null && chartAsset.song != null)
            {
                float totalSeconds = Mathf.Max(0.001f, chartAsset.song.length);
                int steps = Mathf.CeilToInt(timelineGraph.width / 2f);
                for (int k = 0; k < steps; k++)
                {
                    float x = timelineGraph.x + k * 2f;
                    float beat = XToBeat(x);
                    float seconds = Data.beatZeroSeconds + beat * 60f / Mathf.Max(1f, Data.bpm);
                    if (seconds < 0f || seconds >= totalSeconds) continue;
                    int bin = Mathf.Clamp((int)(seconds / totalSeconds * waveform.Length), 0, waveform.Length - 1);
                    float amplitude = waveform[bin] * 34f;
                    Handles.DrawLine(new Vector3(x, y[0] - amplitude), new Vector3(x, y[0] + amplitude));
                }
            }

            float endBeat = viewStartBeat + timelineGraph.width / pixelsPerBeat;
            int firstBeat = Mathf.Max(0, Mathf.FloorToInt(viewStartBeat));
            for (int b = firstBeat; b <= Mathf.CeilToInt(endBeat); b++)
            {
                float x = TimelineX(b);
                if (x < timelineGraph.x || x > timelineGraph.xMax) continue;
                bool major = b % 4 == 0;
                Handles.color = major ? new Color(0.81f, 0.82f, 0.93f, 0.34f) :
                    new Color(0.81f, 0.82f, 0.93f, 0.15f);
                Handles.DrawLine(new Vector3(x, timelineGraph.y + 16f),
                    new Vector3(x, timelineGraph.yMax - 1f));
                GUI.Label(new Rect(x + 2f, timelineGraph.y, 35f, 16f),
                    b.ToString(), EditorStyles.miniLabel);
                if (pixelsPerBeat >= 38f && snap > 0f && snap < 1f)
                {
                    for (float sub = snap; sub < 0.999f; sub += snap)
                    {
                        float subX = TimelineX(b + sub);
                        if (subX > timelineGraph.xMax) break;
                        Handles.color = new Color(0.7f, 0.75f, 0.85f, 0.07f);
                        Handles.DrawLine(new Vector3(subX, timelineGraph.y + 17f),
                            new Vector3(subX, timelineGraph.yMax));
                    }
                }
            }

            foreach (MouseModeChartNote note in Data.notes)
            {
                if (note == null || note.kind != MouseModeNoteKind.Wheel) continue;
                float start = TimelineX(note.beat);
                float end = TimelineX(note.endBeat);
                Handles.color = new Color(WheelNoteColor.r, WheelNoteColor.g, WheelNoteColor.b, 0.52f);
                if (end >= timelineGraph.x && start <= timelineGraph.xMax)
                    Handles.DrawLine(new Vector3(Mathf.Max(start, timelineGraph.x), y[3]),
                        new Vector3(Mathf.Min(end, timelineGraph.xMax), y[3]));
            }
            for (int i = 0; i < Data.notes.Count; i++)
            {
                MouseModeChartNote note = Data.notes[i];
                if (note == null) continue;
                float x = TimelineX(note.beat);
                if (x < timelineGraph.x || x > timelineGraph.xMax) continue;
                int row = note.kind == MouseModeNoteKind.Left ? 1 :
                    note.kind == MouseModeNoteKind.Right ? 2 : 3;
                Handles.color = note.kind == MouseModeNoteKind.Left ? LeftNoteColor :
                    note.kind == MouseModeNoteKind.Right ? RightNoteColor : WheelNoteColor;
                Handles.DrawSolidDisc(new Vector3(x, y[row]), Vector3.forward, i == selectedNote ? 7f : 5f);
                if (i == selectedNote)
                {
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(new Vector3(x, y[row]), Vector3.forward, 9f);
                }
            }
            for (int i = 0; i < Data.attacks.Count; i++)
            {
                MouseModeChartAttack a = Data.attacks[i];
                if (a == null) continue;
                float x = TimelineX(a.beat);
                if (x < timelineGraph.x || x > timelineGraph.xMax) continue;
                MouseModeBulletDirector.AttackSpec spec =
                    MouseModeBulletDirector.ParseAttack(a.patternId);
                float attackLength = spec.kind == MouseModeBulletDirector.AttackKind.Legacy
                    ? 1f : spec.duration;
                float endX = Mathf.Min(timelineGraph.xMax, TimelineX(a.beat + attackLength));
                float barW = Mathf.Max(4f, endX - x);
                Color c = spec.kind == MouseModeBulletDirector.AttackKind.Laser ?
                    new Color(.89f, .39f, .52f, .88f) :
                    spec.kind == MouseModeBulletDirector.AttackKind.Burst ?
                    new Color(.94f, .65f, .42f, .88f) :
                    spec.kind == MouseModeBulletDirector.AttackKind.Rain ?
                    new Color(.79f, .42f, .67f, .82f) : AttackColor;
                EditorGUI.DrawRect(new Rect(x, y[4] - (i == selectedAttack ? 6f : 4f),
                    barW, i == selectedAttack ? 12f : 8f), c);
                if (spec.kind != MouseModeBulletDirector.AttackKind.Legacy && barW > 36f)
                    GUI.Label(new Rect(x + 2f, y[4] - 9f, barW - 3f, 17f),
                        spec.kind == MouseModeBulletDirector.AttackKind.Laser ? "LASER" :
                        spec.kind == MouseModeBulletDirector.AttackKind.Burst ? "BURST" :
                        (spec.style == 0 ? "FAN" : spec.style == 1 ? "CROSS" :
                         spec.style == 2 ? "SNAKE" : "PINCER"),
                        EditorStyles.whiteMiniLabel);
            }
            float playX = TimelineX(currentBeat);
            if (playX >= timelineGraph.x && playX <= timelineGraph.xMax)
            {
                Handles.color = new Color(0.97f, 0.89f, 0.65f);
                Handles.DrawLine(new Vector3(playX, timelineGraph.y),
                    new Vector3(playX, timelineGraph.yMax));
            }
            Handles.EndGUI();
            if (chartAsset.song != null && !waveformAvailable)
                GUI.Label(new Rect(timelineGraph.x + 10f, timelineGraph.y + 37f,
                    timelineGraph.width - 20f, 35f),
                    "파형을 읽지 못했어요. AudioClip의 Load Type을 Decompress On Load로 설정해 주세요.",
                    EditorStyles.wordWrappedMiniLabel);
            if (chartAsset.song == null)
                GUI.Label(new Rect(timelineGraph.x + 10f, timelineGraph.y + 37f,
                    timelineGraph.width - 20f, 35f), "위 [음악] 칸에 AudioClip을 넣으면 파형이 나타나요.",
                    EditorStyles.wordWrappedMiniLabel);
            HandleTimeline(Event.current, y);
        }

        private void EnsureWaveform()
        {
            AudioClip clip = chartAsset != null ? chartAsset.song : null;
            if (clip == waveformClip) return;
            waveformClip = clip;
            waveformAvailable = false;
            waveform = null;
            if (clip == null || clip.samples <= 0 || clip.channels <= 0) return;
            const int bins = 2048;
            float[] peaks = new float[bins];
            const int frameBlock = 32768;
            float[] buffer = new float[frameBlock * clip.channels];
            int frames = clip.samples;
            try
            {
                for (int offset = 0; offset < frames; offset += frameBlock)
                {
                    int count = Mathf.Min(frameBlock, frames - offset);
                    if (!clip.GetData(buffer, offset)) return;
                    for (int i = 0; i < count; i++)
                    {
                        int bin = (int)((long)(offset + i) * bins / frames);
                        float sum = 0f;
                        for (int ch = 0; ch < clip.channels; ch++)
                            sum = Mathf.Max(sum, Mathf.Abs(buffer[i * clip.channels + ch]));
                        if (sum > peaks[bin]) peaks[bin] = sum;
                    }
                }
                waveform = peaks;
                waveformAvailable = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Mouse Chart 파형 분석 불가: " + ex.Message);
            }
        }

        private void HandleTimeline(Event e, float[] rowY)
        {
            if (e.type == EventType.ScrollWheel && timelineGraph.Contains(e.mousePosition))
            {
                if (e.control || e.command)
                {
                    float anchorBeat = XToBeat(e.mousePosition.x);
                    pixelsPerBeat = Mathf.Clamp(pixelsPerBeat * (e.delta.y > 0f ? 0.85f : 1.15f), 8f, 100f);
                    viewStartBeat = Mathf.Max(0f,
                        anchorBeat - (e.mousePosition.x - timelineGraph.x) / pixelsPerBeat);
                }
                else
                {
                    viewStartBeat = Mathf.Clamp(viewStartBeat + e.delta.y * 1.25f,
                        0f, Mathf.Max(0f, SongEndBeat() - 1f));
                }
                e.Use();
                Repaint();
            }
            if (e.type == EventType.MouseDown && e.button == 0 && timelineGraph.Contains(e.mousePosition))
            {
                int hitNote = -1;
                int hitAttack = -1;
                float nearest = 10f;
                for (int i = 0; i < Data.notes.Count; i++)
                {
                    MouseModeChartNote n = Data.notes[i];
                    if (n == null) continue;
                    int row = n.kind == MouseModeNoteKind.Left ? 1 :
                        n.kind == MouseModeNoteKind.Right ? 2 : 3;
                    float distance = Vector2.Distance(e.mousePosition,
                        new Vector2(TimelineX(n.beat), rowY[row]));
                    if (distance < nearest) { nearest = distance; hitNote = i; }
                }
                for (int i = 0; i < Data.attacks.Count; i++)
                {
                    MouseModeChartAttack a = Data.attacks[i];
                    if (a == null) continue;
                    MouseModeBulletDirector.AttackSpec info =
                        MouseModeBulletDirector.ParseAttack(a.patternId);
                    float startX = TimelineX(a.beat);
                    float endX = TimelineX(a.beat + (info.kind ==
                        MouseModeBulletDirector.AttackKind.Legacy ? 1f : info.duration));
                    bool onBar = e.mousePosition.x >= startX - 3f &&
                        e.mousePosition.x <= endX + 3f &&
                        Mathf.Abs(e.mousePosition.y - rowY[4]) < 9f;
                    float distance = onBar ? Mathf.Abs(e.mousePosition.y - rowY[4]) : 999f;
                    if (distance < nearest) { nearest = distance; hitNote = -1; hitAttack = i; }
                }
                selectedNote = hitNote;
                selectedAttack = hitAttack;
                timelineDragNote = hitNote;
                timelineDragAttack = hitAttack;
                if (hitNote >= 0)
                {
                    Undo.RecordObject(chartAsset, "Drag Mouse Chart Note Beat");
                    currentBeat = Data.notes[hitNote].beat;
                }
                else if (hitAttack >= 0)
                {
                    Undo.RecordObject(chartAsset, "Drag Mouse Chart Attack Beat");
                    currentBeat = Data.attacks[hitAttack].beat;
                }
                else
                {
                    SeekBeat(SnapBeat(XToBeat(e.mousePosition.x)));
                }
                e.Use(); Repaint();
            }
            if (e.type == EventType.MouseDrag && e.button == 0 &&
                (timelineDragNote >= 0 || timelineDragAttack >= 0))
            {
                float beat = SnapBeat(XToBeat(e.mousePosition.x));
                if (timelineDragNote >= 0 && timelineDragNote < Data.notes.Count)
                {
                    MouseModeChartNote n = Data.notes[timelineDragNote];
                    float length = n.endBeat - n.beat;
                    n.beat = beat;
                    if (n.kind == MouseModeNoteKind.Wheel) n.endBeat = beat + Mathf.Max(0.01f, length);
                }
                if (timelineDragAttack >= 0 && timelineDragAttack < Data.attacks.Count)
                    Data.attacks[timelineDragAttack].beat = beat;
                currentBeat = beat;
                EditorUtility.SetDirty(chartAsset);
                e.Use(); Repaint();
            }
            if (e.rawType == EventType.MouseUp && (timelineDragNote >= 0 || timelineDragAttack >= 0))
            {
                timelineDragNote = -1;
                timelineDragAttack = -1;
                Repaint();
            }
        }

        private void HandleGlobalShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;
            if ((e.control || e.command) && e.keyCode == KeyCode.S)
            {
                SaveChart(); e.Use(); return;
            }
            if (e.keyCode == KeyCode.Space && chartAsset != null)
            {
                if (previewPlaying) StopPreview(); else StartPreview();
                e.Use(); return;
            }
            if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha5)
            {
                tool = (Tool)(e.keyCode - KeyCode.Alpha1);
                e.Use(); Repaint(); return;
            }
            if (chartAsset != null && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
            {
                if (selectedNote >= 0 && selectedNote < Data.notes.Count)
                {
                    Undo.RecordObject(chartAsset, "Delete Mouse Chart Note");
                    Data.notes.RemoveAt(selectedNote);
                    selectedNote = -1;
                    EditorUtility.SetDirty(chartAsset);
                    e.Use(); Repaint();
                }
                else if (selectedAttack >= 0 && selectedAttack < Data.attacks.Count)
                {
                    Undo.RecordObject(chartAsset, "Delete Mouse Chart Attack");
                    Data.attacks.RemoveAt(selectedAttack);
                    selectedAttack = -1;
                    EditorUtility.SetDirty(chartAsset);
                    e.Use(); Repaint();
                }
            }
        }

        private void DrawStage(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.19f));
            float width = Mathf.Min(area.width - 14f, (area.height - 14f) * 16f / 9f);
            float height = width * 9f / 16f;
            stageRect = new Rect(area.center.x - width * 0.5f,
                area.center.y - height * 0.5f, width, height);
            EditorGUI.DrawRect(stageRect, new Color(0.20f, 0.20f, 0.25f));
            // 가이드만 표시: 피노는 하단 절반이지만 노트는 화면 전체에 배치한다.
            EditorGUI.DrawRect(new Rect(stageRect.x, stageRect.center.y,
                stageRect.width, stageRect.height * 0.5f), new Color(0.26f, 0.24f, 0.33f, 0.24f));

            Handles.BeginGUI();
            Handles.color = new Color(0.6f, 0.6f, 0.7f, 0.22f);
            for (int i = 1; i < 4; i++)
            {
                float x = stageRect.x + stageRect.width * i / 4f;
                float y = stageRect.y + stageRect.height * i / 4f;
                Handles.DrawLine(new Vector3(x, stageRect.y),
                    new Vector3(x, stageRect.yMax));
                Handles.DrawLine(new Vector3(stageRect.x, y),
                    new Vector3(stageRect.xMax, y));
            }

            if (gameplayNotePreview)
            {
                DrawSpecialAttackPreview();
                DrawGameplayNotePreview();
                Handles.EndGUI();
                GUI.Label(new Rect(area.x + 10f, area.y + 4f, area.width - 20f, 20f),
                    "노트 표시 미리보기 | 현재 " + currentBeat.ToString("0.###") +
                    "박 | 노트/휠/레이저/폭발/안무 탄막", EditorStyles.miniLabel);
                GUI.Label(new Rect(stageRect.x + 8f, stageRect.yMax - 20f,
                    stageRect.width - 16f, 17f),
                    "이 화면은 편집 전용 시뮬레이션입니다. 실제 플레이 테스트는 위 버튼을 사용하세요.",
                    EditorStyles.miniLabel);
                return; // 미리보기 상태에서 실수로 노트가 편집되지 않도록 한다.
            }

            DrawSpecialAttackPreview();
            for (int i = 0; i < Data.notes.Count; i++)
            {
                MouseModeChartNote note = Data.notes[i];
                if (note == null || (i != selectedNote && Mathf.Abs(note.beat - currentBeat) > 2f)) continue;
                Vector2 p = note.kind == MouseModeNoteKind.Wheel &&
                            note.path != null && note.path.Count > 0 ?
                    note.path[0] : note.position;
                Vector2 point = StagePoint(p);
                Handles.color = note.kind == MouseModeNoteKind.Left ?
                    new Color(0.69f, 0.55f, 0.82f) :
                    note.kind == MouseModeNoteKind.Right ?
                    new Color(0.43f, 0.69f, 0.72f) :
                    new Color(0.82f, 0.69f, 0.44f);
                Handles.DrawSolidDisc(point, Vector3.forward, i == selectedNote ? 11f : 8f);
                if (i == selectedNote)
                {
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(point, Vector3.forward, 13f);
                }
                if (note.kind == MouseModeNoteKind.Wheel && note.path != null)
                {
                    Handles.color = new Color(0.82f, 0.69f, 0.44f, 0.8f);
                    for (int j = 1; j < note.path.Count; j++)
                        Handles.DrawLine(StagePoint(note.path[j - 1]), StagePoint(note.path[j]));
                    if (i == selectedNote)
                    {
                        for (int j = 0; j < note.path.Count; j++)
                        {
                            Vector2 pp = StagePoint(note.path[j]);
                            Handles.color = j == 0 ? Color.white : WheelNoteColor;
                            Handles.DrawSolidDisc(pp, Vector3.forward, 5f);
                            GUI.Label(new Rect(pp.x + 7f, pp.y - 14f, 24f, 15f),
                                (j + 1).ToString(), EditorStyles.miniLabel);
                        }
                    }
                }
            }
            // No fake corner dots for attacks. The real attack geometry is drawn above.
            Handles.EndGUI();
            GUI.Label(new Rect(area.x + 10f, area.y + 4f, area.width - 20f, 20f),
                "박자 " + currentBeat.ToString("0.##") + "  |  " +
                (tool == Tool.Select ? "선택·이동: 노트를 클릭" : "클릭하여 " + ToolLabels[(int)tool] + " 배치") +
                "  |  현재 ±2박 + 선택 노트 표시", EditorStyles.miniLabel);
            GUI.Label(new Rect(stageRect.x + 8f, stageRect.yMax - 20f,
                stageRect.width - 16f, 17f),
                "아래 50%: 피노 회피 영역 (노트 배치는 전체 가능)", EditorStyles.miniLabel);
            HandleStageClick(Event.current);
            HandleStageDrag(Event.current);
        }

        // Uses the same serialized patternId parameters AND the same normalized
        // coordinate conventions as the runtime. Only actual lasers/bursts render.
        private void DrawSpecialAttackPreview()
        {
            if (Data == null || Data.attacks == null) return;
            foreach (MouseModeChartAttack attack in Data.attacks)
            {
                if (attack == null) continue;
                MouseModeBulletDirector.AttackSpec s =
                    MouseModeBulletDirector.ParseAttack(attack.patternId);
                if (s.kind == MouseModeBulletDirector.AttackKind.Legacy) continue;
                float start = attack.beat;
                float prep = (currentBeat - (start - s.warning)) / s.warning;
                float fire = (currentBeat - start) / s.duration;
                float visibleFrom = s.kind == MouseModeBulletDirector.AttackKind.Rain
                    ? start - 2.25f - s.warning : start - s.warning;
                float visibleTo = s.kind == MouseModeBulletDirector.AttackKind.Rain
                    ? start + s.duration + .85f : start + s.duration;
                if (currentBeat < visibleFrom || currentBeat >= visibleTo) continue;
                bool active = currentBeat >= start;
                float radius = s.radius * Mathf.Min(stageRect.width, stageRect.height);
                if (s.kind == MouseModeBulletDirector.AttackKind.Rain)
                {
                    DrawRainAttackPreview(s, start);
                }
                else if (s.kind == MouseModeBulletDirector.AttackKind.Laser)
                {
                    Vector2 origin = new Vector2(stageRect.x + s.x * stageRect.width,
                        stageRect.y);
                    Vector2 end = PreviewLaserEnd(origin, s.angle);
                    float width = Mathf.Max(2f, s.width * stageRect.width);
                    if (active)
                    {
                        Handles.color = new Color(.13f, .09f, .16f, 1f);
                        Handles.DrawAAPolyLine(width * 1.23f, origin, end);
                        Handles.color = new Color(.82f, .30f, .41f, .86f);
                        Handles.DrawAAPolyLine(width, origin, end);
                        Handles.color = new Color(1f, .83f, .79f, .86f);
                        Handles.DrawAAPolyLine(Mathf.Max(1.3f, width * .28f), origin, end);
                    }
                    else
                    {
                        // Width-first hazard area and two moving charge notches.
                        Handles.color = new Color(.92f, .46f, .53f, .05f + .15f * Mathf.Clamp01(prep));
                        Handles.DrawAAPolyLine(width, origin, end);
                        Handles.color = new Color(.95f, .60f, .67f, .24f + Mathf.Clamp01(prep) * .30f);
                        Handles.DrawAAPolyLine(Mathf.Max(1.5f, width * .15f), origin, end);
                        Vector2 dir = (end - origin).normalized;
                        Vector2 side = new Vector2(-dir.y, dir.x) * Mathf.Max(5f, width * .6f);
                        Vector2 pos1 = Vector2.Lerp(origin, end, .33f);
                        Vector2 pos2 = Vector2.Lerp(origin, end, .67f);
                        Handles.color = new Color(1f, .81f, .72f, .78f);
                        Handles.DrawAAPolyLine(2.2f, pos1 - side, pos1 + side);
                        Handles.DrawAAPolyLine(2.2f, pos2 - side, pos2 + side);
                    }
                    Handles.color = active ? new Color(.98f, .73f, .68f) :
                        new Color(.88f, .47f, .55f);
                    Handles.DrawSolidDisc(origin, Vector3.forward, Mathf.Max(3f, width * .7f));
                }
                else
                {
                    Vector2 target = StagePoint(new Vector2(s.x, s.y));
                    if (!active)
                    {
                        Vector2 source = StagePoint(new Vector2(.5f, 0f));
                        Vector2 orb = Vector2.Lerp(source, target, Mathf.Clamp01(prep));
                        Handles.color = new Color(.92f, .53f, .58f, .45f);
                        Handles.DrawAAPolyLine(Mathf.Max(1f, stageRect.height * .006f), source, orb);
                        Handles.color = new Color(.94f, .60f, .65f, .9f);
                        float gem = Mathf.Max(3f, stageRect.height * .021f);
                        Handles.DrawAAConvexPolygon(orb + Vector2.up * gem,
                            orb + Vector2.right * gem, orb + Vector2.down * gem, orb + Vector2.left * gem);
                        Handles.color = new Color(.92f, .45f, .51f, .48f);
                        Handles.DrawWireDisc(target, Vector3.forward,
                            radius * (.92f + .08f * Mathf.Clamp01(prep)));
                        Handles.color = new Color(1f, .81f, .75f, .60f);
                        Handles.DrawAAPolyLine(1.5f, target + Vector2.left * gem * 2f, target + Vector2.right * gem * 2f);
                        Handles.DrawAAPolyLine(1.5f, target + Vector2.up * gem * 2f, target + Vector2.down * gem * 2f);
                    }
                    else
                    {
                        float travel = radius * Mathf.Clamp01(fire);
                        Handles.color = new Color(.92f, .48f, .53f, Mathf.Clamp01(1f - fire * 7f) * .6f);
                        Handles.DrawWireDisc(target, Vector3.forward,
                            radius * Mathf.Min(1f, fire * 3f));
                        Handles.color = new Color(.83f, .38f, .47f, .95f);
                        for (int j = 0; j < s.count; j++)
                        {
                            float a = (j + .5f) * Mathf.PI * 2f / s.count;
                            Vector2 p = target + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * travel;
                            if (!stageRect.Contains(p)) continue;
                            Handles.DrawSolidDisc(p, Vector3.forward, Mathf.Max(2.5f, stageRect.height * .012f));
                        }
                    }
                }
            }
        }

        // Same procedurally choreographed waves as the runtime director.
        private void DrawRainAttackPreview(MouseModeBulletDirector.AttackSpec spec, float beat)
        {
            float iconSize = Mathf.Max(3.4f, stageRect.height * .020f);
            for (int row = 0; row < spec.rows; row++)
            {
                float t = spec.rows <= 1 ? 0f : row / (float)(spec.rows - 1);
                float impact = beat + t * Mathf.Max(.25f, spec.duration - .55f);
                float launch = impact - 2.2f; // Matches Director default.
                float end = impact + .85f;
                if (currentBeat < launch - spec.warning || currentBeat > end) continue;
                List<MouseModeBulletDirector.WaveShot> shots =
                    MouseModeBulletDirector.GetWaveShots(spec, row);
                foreach (MouseModeBulletDirector.WaveShot shot in shots)
                {
                    Vector2 from = StagePoint(new Vector2(shot.startX, 0f));
                    Vector2 hit = StagePoint(new Vector2(shot.targetX, .72f));
                    if (currentBeat < launch)
                    {
                        // A small source cue; no long lines covering the notes.
                        if (row == 0)
                        {
                            Handles.color = new Color(.94f, .51f, .58f, .50f);
                            Handles.DrawWireDisc(from, Vector3.forward, iconSize * .85f);
                        }
                        continue;
                    }
                    Vector2 pos;
                    if (currentBeat <= impact)
                    {
                        float p = Mathf.Clamp01((currentBeat - launch) / 2.2f);
                        pos = Vector2.Lerp(from, hit, p);
                        pos.x += Mathf.Sin(p * Mathf.PI) * shot.curve * stageRect.width;
                    }
                    else
                        pos = Vector2.Lerp(hit, StagePoint(new Vector2(shot.targetX, 1.02f)),
                            Mathf.Clamp01((currentBeat - impact) / .85f));
                    if (!stageRect.Contains(pos)) continue;
                    Handles.color = new Color(.91f, .47f, .54f, .20f);
                    Handles.DrawAAPolyLine(2f, pos + Vector2.up * iconSize * 1.4f, pos);
                    Handles.color = new Color(.96f, .60f, .62f, .95f);
                    Handles.DrawAAConvexPolygon(pos + Vector2.up * iconSize,
                        pos + Vector2.right * iconSize * .85f,
                        pos + Vector2.down * iconSize,
                        pos + Vector2.left * iconSize * .85f);
                }
            }
        }

        private Vector2 PreviewLaserEnd(Vector2 source, float angle)
        {
            float r = angle * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            float travel = (stageRect.yMax - source.y) / Mathf.Max(.01f, d.y);
            if (d.x > .001f)
                travel = Mathf.Min(travel, (stageRect.xMax - source.x) / d.x);
            else if (d.x < -.001f)
                travel = Mathf.Min(travel, (stageRect.xMin - source.x) / d.x);
            return source + d * Mathf.Max(0f, travel);
        }

        private struct PreviewSettings
        {
            public float diameter;
            public float approachMaxScale;
            public float approachOpacity;
            public float railGap;
            public Color left;
            public Color right;
            public Color wheel;
        }

        private PreviewSettings GetPreviewSettings()
        {
            PreviewSettings settings = new PreviewSettings
            {
                diameter = Mathf.Max(9f, stageRect.height * 0.12f),
                approachMaxScale = 1.65f,
                approachOpacity = 0.65f,
                railGap = Mathf.Max(6f, stageRect.height * 0.06f),
                left = LeftNoteColor,
                right = RightNoteColor,
                wheel = WheelNoteColor
            };

            // Mouse 씬의 실제 NoteLayer 크기와 Inspector 값을 읽어 비율을 맞춘다.
            if (previewSource == null)
            {
                MouseModeChartPlayer[] players =
                    UnityEngine.Object.FindObjectsOfType<MouseModeChartPlayer>(true);
                foreach (MouseModeChartPlayer item in players)
                {
                    if (item != null && item.gameObject.scene.IsValid() &&
                        item.gameObject.scene.isLoaded)
                    {
                        previewSource = item;
                        if (item.gameObject.scene.name == "Mouse") break;
                    }
                }
            }
            if (previewSource == null) return settings;

            SerializedObject serialized = new SerializedObject(previewSource);
            SerializedProperty noteSizeProp = serialized.FindProperty("noteSize");
            SerializedProperty noteScaleProp = serialized.FindProperty("noteVisualScale");
            SerializedProperty approachScaleProp = serialized.FindProperty("approachStartScale");
            SerializedProperty approachLimitProp = serialized.FindProperty("readableApproachScaleLimit");
            SerializedProperty approachOpacityProp = serialized.FindProperty("approachOpacity");
            SerializedProperty gapProp = serialized.FindProperty("wheelRailGap");
            SerializedProperty layerProp = serialized.FindProperty("noteLayer");
            SerializedProperty leftProp = serialized.FindProperty("leftColor");
            SerializedProperty rightProp = serialized.FindProperty("rightColor");
            SerializedProperty wheelProp = serialized.FindProperty("wheelColor");

            RectTransform layer = layerProp != null ? layerProp.objectReferenceValue as RectTransform : null;
            float baseHeight = layer != null && layer.rect.height > 1f ? layer.rect.height : 720f;
            float scale = stageRect.height / baseHeight;
            float noteSize = noteSizeProp != null ? noteSizeProp.floatValue : 104f;
            float noteScale = noteScaleProp != null ? noteScaleProp.floatValue : 1f;
            // 최신 플레이어에 시각 크기 제한이 있으면 동일한 제한을 사용한다.
            float renderedSize = noteScaleProp != null
                ? Mathf.Clamp(Mathf.Min(noteSize * noteScale, baseHeight * 0.14f), 52f, 96f)
                : noteSize;
            settings.diameter = Mathf.Max(8f, renderedSize * scale);
            settings.approachMaxScale = Mathf.Clamp(
                approachScaleProp != null ? approachScaleProp.floatValue : 2.3f,
                1.25f, approachLimitProp != null ? approachLimitProp.floatValue : 2.3f);
            settings.approachOpacity = approachOpacityProp != null
                ? Mathf.Clamp01(approachOpacityProp.floatValue) : 0.65f;
            settings.railGap = gapProp != null ? Mathf.Max(4f, gapProp.floatValue * scale)
                : Mathf.Max(6f, stageRect.height * 0.06f);
            if (leftProp != null) settings.left = leftProp.colorValue;
            if (rightProp != null) settings.right = rightProp.colorValue;
            if (wheelProp != null) settings.wheel = wheelProp.colorValue;
            return settings;
        }

        private void EnsurePreviewStyles()
        {
            if (previewLetterStyle == null)
            {
                previewLetterStyle = new GUIStyle(EditorStyles.boldLabel);
                previewLetterStyle.alignment = TextAnchor.MiddleCenter;
                previewLetterStyle.normal.textColor = Color.white;
            }
            if (previewSmallStyle == null)
            {
                previewSmallStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                previewSmallStyle.alignment = TextAnchor.MiddleCenter;
                previewSmallStyle.normal.textColor = Color.white;
            }
        }

        private void DrawGameplayNotePreview()
        {
            if (Data == null || Data.notes == null) return;
            PreviewSettings settings = GetPreviewSettings();
            EnsurePreviewStyles();
            // 현재 박자를 음악의 실제 시간으로 환산한다 (0박 오프셋도 포함).
            float nowSeconds = Data.beatZeroSeconds +
                currentBeat * 60f / Mathf.Max(1f, Data.bpm);
            float approachSeconds = Mathf.Max(0.1f, Data.approachSeconds);
            float secondsPerBeat = 60f / Mathf.Max(1f, Data.bpm);
            float missWindow = 0.13f; // 화면에 남는 시간의 미리보기 기준
            List<MouseModeChartNote> shown = new List<MouseModeChartNote>();

            foreach (MouseModeChartNote note in Data.notes)
            {
                if (note == null) continue;
                float hitTime = Data.beatZeroSeconds + note.beat * secondsPerBeat;
                float endTime = Data.beatZeroSeconds +
                    (note.kind == MouseModeNoteKind.Wheel ? note.endBeat : note.beat) * secondsPerBeat;
                if (nowSeconds < hitTime - approachSeconds ||
                    nowSeconds > endTime + missWindow) continue;
                shown.Add(note);
            }
            // 나중에 처리해야 하는 노트 위에 먼저 처리할 노트가 표시되도록 한다.
            shown.Sort((a, b) => b.beat.CompareTo(a.beat));

            foreach (MouseModeChartNote note in shown)
            {
                Color color = note.kind == MouseModeNoteKind.Left ? settings.left :
                    note.kind == MouseModeNoteKind.Right ? settings.right : settings.wheel;
                float hitTime = Data.beatZeroSeconds + note.beat * secondsPerBeat;
                float remaining = Mathf.Clamp01((hitTime - nowSeconds) / approachSeconds);
                float previewSize = settings.diameter;
                Vector2 position = StagePreviewPoint(note.position, previewSize);

                if (note.kind == MouseModeNoteKind.Wheel && note.path != null && note.path.Count > 0)
                {
                    float endTime = Data.beatZeroSeconds + note.endBeat * secondsPerBeat;
                    DrawWheelPreviewRail(note, settings, color);
                    if (nowSeconds >= hitTime && endTime > hitTime)
                    {
                        float progress = Mathf.Clamp01((nowSeconds - hitTime) / (endTime - hitTime));
                        position = GetPreviewWheelPosition(note, previewSize, progress);
                    }
                    else position = StagePreviewPoint(note.path[0], previewSize);
                }

                // 접근 원은 판정 순간 노트 본체 크기로 모인다.
                if (nowSeconds <= hitTime)
                {
                    float size = previewSize * 1.04f * Mathf.Lerp(1f,
                        settings.approachMaxScale, Mathf.Pow(remaining, 0.8f));
                    Color ring = color;
                    ring.a = Mathf.Min(settings.approachOpacity, 0.66f) *
                        Mathf.Lerp(1f, 0.55f, remaining);
                    Handles.color = ring;
                    Handles.DrawWireDisc(position, Vector3.forward, size * 0.5f);
                }

                Handles.color = new Color(0.10f, 0.10f, 0.16f, 1f);
                Handles.DrawSolidDisc(position, Vector3.forward, previewSize * 0.5f);
                Handles.color = color;
                Handles.DrawSolidDisc(position, Vector3.forward, previewSize * 0.45f);
                Handles.color = new Color(0.17f, 0.17f, 0.23f, 1f);
                Handles.DrawSolidDisc(position, Vector3.forward, previewSize * 0.33f);
                if (note.kind != MouseModeNoteKind.Wheel)
                {
                    float x = (note.kind == MouseModeNoteKind.Left ? -1f : 1f)
                        * previewSize * 0.31f;
                    Handles.color = Color.white;
                    Handles.DrawLine(position + new Vector2(x, -previewSize * 0.15f),
                        position + new Vector2(x, previewSize * 0.15f));
                }
                previewLetterStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(previewSize * 0.38f), 8, 24);
                GUI.Label(new Rect(position.x - previewSize * 0.5f,
                    position.y - previewSize * 0.5f, previewSize, previewSize),
                    note.kind == MouseModeNoteKind.Left ? "L" :
                    note.kind == MouseModeNoteKind.Right ? "R" : "W", previewLetterStyle);
            }
            Handles.color = Color.white;
        }

        private Vector2 StagePreviewPoint(Vector2 normalized, float diameter)
        {
            float padding = diameter * 0.62f;
            return new Vector2(
                Mathf.Clamp(stageRect.x + Mathf.Clamp01(normalized.x) * stageRect.width,
                    stageRect.xMin + padding, stageRect.xMax - padding),
                Mathf.Clamp(stageRect.y + Mathf.Clamp01(normalized.y) * stageRect.height,
                    stageRect.yMin + padding, stageRect.yMax - padding));
        }

        private Vector2 GetPreviewWheelPosition(MouseModeChartNote note, float diameter, float progress)
        {
            if (note.path == null || note.path.Count == 0)
                return StagePreviewPoint(note.position, diameter);
            if (note.path.Count == 1)
                return StagePreviewPoint(note.path[0], diameter);
            float total = 0f;
            for (int i = 1; i < note.path.Count; i++)
                total += Vector2.Distance(StagePreviewPoint(note.path[i - 1], diameter),
                    StagePreviewPoint(note.path[i], diameter));
            if (total < 0.001f) return StagePreviewPoint(note.path[0], diameter);
            float travel = Mathf.Clamp01(progress) * total;
            for (int i = 1; i < note.path.Count; i++)
            {
                Vector2 a = StagePreviewPoint(note.path[i - 1], diameter);
                Vector2 b = StagePreviewPoint(note.path[i], diameter);
                float len = Vector2.Distance(a, b);
                if (travel <= len || i == note.path.Count - 1)
                    return Vector2.Lerp(a, b, len > 0f ? Mathf.Clamp01(travel / len) : 1f);
                travel -= len;
            }
            return StagePreviewPoint(note.path[note.path.Count - 1], diameter);
        }

        private void DrawWheelPreviewRail(MouseModeChartNote note,
            PreviewSettings settings, Color color)
        {
            if (note.path == null || note.path.Count < 2) return;
            float halfGap = settings.railGap * 0.5f;
            for (int i = 1; i < note.path.Count; i++)
            {
                Vector2 a = StagePreviewPoint(note.path[i - 1], settings.diameter);
                Vector2 b = StagePreviewPoint(note.path[i], settings.diameter);
                Vector2 direction = b - a;
                if (direction.sqrMagnitude < 0.01f) continue;
                Vector2 normal = new Vector2(-direction.y, direction.x).normalized * halfGap;
                Color rail = color; rail.a = 0.53f;
                Handles.color = rail;
                Handles.DrawLine(a + normal, b + normal);
                Handles.DrawLine(a - normal, b - normal);
            }
            Vector2 end = StagePreviewPoint(note.path[note.path.Count - 1], settings.diameter);
            float r = Mathf.Max(5f, settings.diameter * 0.25f);
            Handles.color = color;
            Handles.DrawWireDisc(end, Vector3.forward, r);
            previewSmallStyle.fontSize = 9;
            GUI.Label(new Rect(end.x - 20f, end.y - 8f, 40f, 16f), "END", previewSmallStyle);
        }

        private Vector2 StagePoint(Vector2 normalized)
        {
            return new Vector2(
                stageRect.x + Mathf.Clamp01(normalized.x) * stageRect.width,
                stageRect.y + Mathf.Clamp01(normalized.y) * stageRect.height);
        }

        private Vector2 NormalizedPosition(Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp01((point.x - stageRect.x) / stageRect.width),
                Mathf.Clamp01((point.y - stageRect.y) / stageRect.height));
        }

        private void HandleStageClick(Event e)
        {
            if (e.type != EventType.MouseDown || e.button != 0 ||
                !stageRect.Contains(e.mousePosition)) return;

            Vector2 p = NormalizedPosition(e.mousePosition);
            Undo.RecordObject(chartAsset, "Edit Mouse Mode Chart");
            if (tool == Tool.Select)
            {
                // Shift+클릭: 선택된 휠 노트에 경로점 추가
                if (e.shift && selectedNote >= 0 && selectedNote < Data.notes.Count &&
                    Data.notes[selectedNote].kind == MouseModeNoteKind.Wheel)
                {
                    MouseModeChartNote wheel = Data.notes[selectedNote];
                    if (wheel.path == null) wheel.path = new List<Vector2>();
                    wheel.path.Add(p);
                    EditorUtility.SetDirty(chartAsset);
                    e.Use(); Repaint(); return;
                }
                // 선택된 휠 경로의 점을 드래그해 변경 가능
                stageDragPathPoint = -1;
                if (selectedNote >= 0 && selectedNote < Data.notes.Count &&
                    Data.notes[selectedNote].kind == MouseModeNoteKind.Wheel)
                {
                    List<Vector2> path = Data.notes[selectedNote].path;
                    if (path != null)
                        for (int j = 0; j < path.Count; j++)
                            if (Vector2.Distance(StagePoint(path[j]), e.mousePosition) < 10f)
                            { stageDragPathPoint = j; stageDragNote = selectedNote; break; }
                }
                float nearest = 18f;
                int result = -1;
                for (int i = 0; i < Data.notes.Count; i++)
                {
                    MouseModeChartNote note = Data.notes[i];
                    if (note == null || (i != selectedNote && Mathf.Abs(note.beat - currentBeat) > 2f)) continue;
                    Vector2 pos = note.kind == MouseModeNoteKind.Wheel &&
                                  note.path != null && note.path.Count > 0 ?
                        note.path[0] : note.position;
                    float distance = Vector2.Distance(StagePoint(pos), e.mousePosition);
                    if (distance < nearest) { nearest = distance; result = i; }
                }
                if (stageDragPathPoint < 0) stageDragNote = result;
                if (stageDragNote >= 0)
                {
                    selectedNote = stageDragNote;
                    MouseModeChartNote hit = Data.notes[stageDragNote];
                    stageDragOffset = hit.position - p;
                    stageIsDragging = true;
                }
                else selectedNote = -1;
                selectedAttack = -1;
            }
            else if (tool == Tool.Attack)
            {
                Data.attacks.Add(new MouseModeChartAttack
                {
                    beat = SnappedBeat(),
                    patternId = "laser|x=0.5|angle=0|width=0.055|warning=1.5|duration=1.75"
                });
                selectedAttack = Data.attacks.Count - 1;
                selectedNote = -1;
            }
            else
            {
                MouseModeNoteKind kind = tool == Tool.Left ? MouseModeNoteKind.Left :
                    tool == Tool.Right ? MouseModeNoteKind.Right : MouseModeNoteKind.Wheel;
                MouseModeChartNote note = new MouseModeChartNote
                {
                    id = Data.NextId(),
                    kind = kind,
                    beat = SnappedBeat(),
                    position = p,
                    endBeat = SnappedBeat() + 2f
                };
                if (kind == MouseModeNoteKind.Wheel)
                {
                    note.path.Add(p);
                    note.path.Add(new Vector2(Mathf.Clamp01(p.x + 0.2f), p.y));
                }
                Data.notes.Add(note);
                selectedNote = Data.notes.Count - 1;
                selectedAttack = -1;
            }
            EditorUtility.SetDirty(chartAsset);
            e.Use();
            Repaint();
        }

        private void HandleStageDrag(Event e)
        {
            if (e.type == EventType.MouseDrag && e.button == 0 && stageIsDragging &&
                stageDragNote >= 0 && stageDragNote < Data.notes.Count)
            {
                MouseModeChartNote n = Data.notes[stageDragNote];
                Vector2 p = NormalizedPosition(e.mousePosition);
                if (stageDragPathPoint >= 0 && n.path != null && stageDragPathPoint < n.path.Count)
                {
                    n.path[stageDragPathPoint] = p;
                    if (stageDragPathPoint == 0) n.position = p;
                }
                else
                {
                    Vector2 destination = Clamp01(p + stageDragOffset);
                    Vector2 delta = destination - n.position;
                    n.position = destination;
                    if (n.kind == MouseModeNoteKind.Wheel && n.path != null)
                    {
                        for (int i = 0; i < n.path.Count; i++)
                            n.path[i] = Clamp01(n.path[i] + delta);
                    }
                }
                EditorUtility.SetDirty(chartAsset);
                e.Use(); Repaint();
            }
            if (e.rawType == EventType.MouseUp && stageIsDragging)
            {
                stageIsDragging = false;
                stageDragNote = -1;
                stageDragPathPoint = -1;
                Repaint();
            }
        }

        private void DrawSelectedInspector()
        {
            GUILayout.Label("선택 항목", EditorStyles.boldLabel);
            if (selectedNote >= 0 && selectedNote < Data.notes.Count)
            {
                MouseModeChartNote note = Data.notes[selectedNote];
                if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                    Undo.RecordObject(chartAsset, "Edit Mouse Chart Note");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("노트 ID", note.id.ToString());
                note.kind = (MouseModeNoteKind)EditorGUILayout.EnumPopup("종류", note.kind);
                note.beat = Mathf.Max(0f, EditorGUILayout.FloatField("박자", note.beat));
                note.position = Clamp01(EditorGUILayout.Vector2Field("위치", note.position));
                if (note.kind == MouseModeNoteKind.Wheel)
                {
                    EditorGUILayout.HelpBox("경로점: 미리보기에서 드래그 / Shift+클릭 추가", MessageType.None);
                    note.endBeat = Mathf.Max(note.beat + 0.01f,
                        EditorGUILayout.FloatField("종료 박자", note.endBeat));
                    if (note.path == null) note.path = new System.Collections.Generic.List<Vector2>();
                    GUILayout.Label("휠 이동 경로 (0~1)", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < note.path.Count; i++)
                        note.path[i] = Clamp01(EditorGUILayout.Vector2Field(
                            "경로 " + (i + 1), note.path[i]));
                    if (GUILayout.Button("경로점 추가"))
                    {
                        Vector2 last = note.path.Count > 0 ?
                            note.path[note.path.Count - 1] : note.position;
                        note.path.Add(Clamp01(last + new Vector2(0.1f, 0f)));
                    }
                    if (note.path.Count > 2 && GUILayout.Button("마지막 경로점 삭제"))
                        note.path.RemoveAt(note.path.Count - 1);
                }
                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(chartAsset);
                GUILayout.Space(8);
                if (GUILayout.Button("이 노트 삭제"))
                {
                    Undo.RecordObject(chartAsset, "Delete Mouse Chart Note");
                    Data.notes.RemoveAt(selectedNote);
                    selectedNote = -1;
                    EditorUtility.SetDirty(chartAsset);
                }
            }
            else if (selectedAttack >= 0 && selectedAttack < Data.attacks.Count)
            {
                MouseModeChartAttack attack = Data.attacks[selectedAttack];
                if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                    Undo.RecordObject(chartAsset, "Edit Mouse Chart Attack");
                EditorGUI.BeginChangeCheck();
                attack.beat = Mathf.Max(0f,
                    EditorGUILayout.FloatField("발동 박자", attack.beat));
                MouseModeBulletDirector.AttackSpec spec =
                    MouseModeBulletDirector.ParseAttack(attack.patternId);
                string[] types = { "기존 패턴", "레이저", "폭발 확산탄", "안무형 탄막" };
                int nextKind = EditorGUILayout.Popup("공격 종류", (int)spec.kind, types);
                spec.kind = (MouseModeBulletDirector.AttackKind)nextKind;
                if (spec.kind == MouseModeBulletDirector.AttackKind.Legacy)
                    spec.legacyId = EditorGUILayout.TextField("기존 패턴 ID", spec.legacyId);
                else
                {
                    spec.x = EditorGUILayout.Slider("발사 X (0~1)", spec.x, 0f, 1f);
                    if (spec.kind == MouseModeBulletDirector.AttackKind.Laser)
                    {
                        spec.angle = EditorGUILayout.Slider("기울기 (도)", spec.angle, -70f, 70f);
                        spec.width = EditorGUILayout.Slider("레이저 폭 (화면 비율)", spec.width, .015f, .16f);
                    }
                    else if (spec.kind == MouseModeBulletDirector.AttackKind.Burst)
                    {
                        spec.y = EditorGUILayout.Slider("폭발 Y (위→아래)", spec.y, 0.05f, .95f);
                        spec.radius = EditorGUILayout.Slider("확산 반경", spec.radius, .1f, .9f);
                        spec.count = EditorGUILayout.IntSlider("확산탄 수", spec.count, 6, 24);
                    }
                    else if (spec.kind == MouseModeBulletDirector.AttackKind.Rain)
                    {
                        spec.style = EditorGUILayout.Popup("탄막 안무", spec.style,
                            new[] { "부채꼴 전개", "양측 교차", "S자 곡선", "양측 협공" });
                        spec.drift = EditorGUILayout.Slider("안전 통로 이동량", spec.drift, -.4f, .4f);
                        spec.rows = EditorGUILayout.IntSlider("낙하 줄 수", spec.rows, 3, 12);
                        spec.gap = EditorGUILayout.IntPopup("안전 통로 폭", spec.gap,
                            new[] { "1칸", "3칸", "5칸" }, new[] { 1, 3, 5 });
                        EditorGUILayout.HelpBox("곡선 이동과 방사형 전개로 탄환이 내려옵니다. " +
                            "실제 탄환 수는 안무와 빈 통로 설정에 따라 달라집니다.", MessageType.None);
                    }
                    spec.warning = EditorGUILayout.FloatField("예고 (박)", spec.warning);
                    spec.duration = EditorGUILayout.FloatField("공격 지속 (박)", spec.duration);
                    spec.warning = Mathf.Clamp(spec.warning, .25f, 8f);
                    spec.duration = Mathf.Clamp(spec.duration, .25f, 8f);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    attack.patternId = MouseModeBulletDirector.EncodeAttack(spec);
                    EditorUtility.SetDirty(chartAsset);
                }
                if (GUILayout.Button("이 이벤트 삭제"))
                {
                    Undo.RecordObject(chartAsset, "Delete Mouse Chart Attack");
                    Data.attacks.RemoveAt(selectedAttack);
                    selectedAttack = -1;
                    EditorUtility.SetDirty(chartAsset);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("도구를 선택하고 왼쪽 화면을 클릭하면 " +
                    "해당 박자에 노트가 추가돼요. '선택' 도구로 누르면 여기서 수치와 경로를 수정할 수 있어요.",
                    MessageType.None);
            }
        }

        private static Vector2 Clamp01(Vector2 p)
        {
            return new Vector2(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y));
        }

        private void DrawEntries()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("4. 채보 항목 목록  (" + Data.notes.Count + "개 노트 / " +
                Data.attacks.Count + "개 공격 이벤트)", EditorStyles.boldLabel);
            if (GUILayout.Button("샘플 노트 추가", GUILayout.Width(110f)))
            {
                if (Data.notes.Count == 0 || EditorUtility.DisplayDialog(
                    "샘플 노트 추가", "기존 노트는 유지하고 샘플 노트를 추가할까요?", "추가", "취소"))
                {
                    Undo.RecordObject(chartAsset, "Add Sample Mouse Notes");
                    AddSampleNotes();
                    EditorUtility.SetDirty(chartAsset);
                    currentBeat = 4f;
                }
            }
            GUILayout.EndHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(105f));
            for (int i = 0; i < Data.notes.Count; i++)
            {
                MouseModeChartNote note = Data.notes[i];
                if (note == null) continue;
                int index = i;
                if (GUILayout.Button(note.id + " | " + note.kind +
                    " | Beat " + note.beat.ToString("0.##"), EditorStyles.miniButton))
                {
                    selectedNote = index;
                    selectedAttack = -1;
                    SeekBeat(note.beat);
                }
            }
            for (int i = 0; i < Data.attacks.Count; i++)
            {
                MouseModeChartAttack attack = Data.attacks[i];
                if (attack == null) continue;
                int index = i;
                MouseModeBulletDirector.AttackSpec attackInfo =
                    MouseModeBulletDirector.ParseAttack(attack.patternId);
                string attackName = attackInfo.kind == MouseModeBulletDirector.AttackKind.Rain
                    ? (attackInfo.style == 0 ? "FAN" : attackInfo.style == 1 ? "CROSS" :
                        attackInfo.style == 2 ? "SNAKE" : "PINCER")
                    : attackInfo.kind == MouseModeBulletDirector.AttackKind.Legacy
                    ? attackInfo.legacyId : attackInfo.kind.ToString().ToUpperInvariant();
                if (GUILayout.Button("ATTACK | " + attackName +
                    " | Beat " + attack.beat.ToString("0.##"), EditorStyles.miniButton))
                {
                    selectedAttack = index;
                    selectedNote = -1;
                    SeekBeat(attack.beat);
                }
            }
            GUILayout.EndScrollView();
        }

        private void CreateChart(bool withSample)
        {
            // 기존 4레인 시스템과 완전히 분리된 폴더에 새 에셋을 만든다.
            EnsureFolder("Assets/MouseMode/ChartSystem/Charts");
            string fileName = withSample ? "SampleMouseChart" : "NewMouseChart";
            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/MouseMode/ChartSystem/Charts/" + fileName + ".asset");

            StopPreview();
            MouseModeChartAsset created = CreateInstance<MouseModeChartAsset>();
            created.data.songId = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);
            chartAsset = created;
            waveformClip = null;
            waveform = null;
            viewStartBeat = 0f;
            selectedNote = selectedAttack = -1;
            currentBeat = 4f;

            if (withSample)
                AddSampleNotes();

            SaveChart();
            RememberChart();
            Selection.activeObject = chartAsset;
            EditorGUIUtility.PingObject(chartAsset);
            Repaint();
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }

        private void AddSampleNotes()
        {
            if (Data == null) return;
            int id = Data.NextId();
            float start = 4f;
            MouseModeNoteKind[] kinds = {
                MouseModeNoteKind.Left, MouseModeNoteKind.Right,
                MouseModeNoteKind.Left, MouseModeNoteKind.Right,
                MouseModeNoteKind.Left, MouseModeNoteKind.Right,
                MouseModeNoteKind.Left, MouseModeNoteKind.Right
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                Data.notes.Add(new MouseModeChartNote
                {
                    id = id++,
                    kind = kinds[i],
                    beat = start + i,
                    position = new Vector2(i % 2 == 0 ? 0.33f : 0.67f,
                        0.35f + (i % 3) * 0.13f)
                });
            }
            // 근처 위치에 나오는 동시치기.
            Data.notes.Add(new MouseModeChartNote
            {
                id = id++,
                kind = MouseModeNoteKind.Left,
                beat = 13f,
                position = new Vector2(0.44f, 0.5f)
            });
            Data.notes.Add(new MouseModeChartNote
            {
                id = id++,
                kind = MouseModeNoteKind.Right,
                beat = 13f,
                position = new Vector2(0.56f, 0.5f)
            });
            MouseModeChartNote wheel = new MouseModeChartNote
            {
                id = id++,
                kind = MouseModeNoteKind.Wheel,
                beat = 15f,
                endBeat = 18f,
                position = new Vector2(0.30f, 0.48f)
            };
            wheel.path.Add(new Vector2(0.30f, 0.48f));
            wheel.path.Add(new Vector2(0.50f, 0.60f));
            wheel.path.Add(new Vector2(0.70f, 0.46f));
            Data.notes.Add(wheel);
        }

        private void StartGameTest()
        {
            if (chartAsset == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("게임 실행 중",
                    "이미 플레이 중이에요. Unity 위쪽 Play 버튼으로 먼저 정지해 주세요.", "확인");
                return;
            }
            if (Data == null || Data.notes == null || Data.notes.Count == 0)
            {
                EditorUtility.DisplayDialog("아직 노트가 없어요",
                    "채보 화면을 클릭해 노트를 배치하거나, 아래 [샘플 노트 추가]를 눌러 주세요.", "확인");
                return;
            }

            MouseModeChartPlayer player = null;
            MouseModeChartPlayer[] players =
                UnityEngine.Object.FindObjectsOfType<MouseModeChartPlayer>(true);
            foreach (MouseModeChartPlayer candidate in players)
            {
                if (candidate == null || !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded) continue;
                if (candidate.gameObject.scene.name == "Mouse")
                {
                    player = candidate;
                    break;
                }
                if (player == null) player = candidate;
            }
            if (player == null)
            {
                EditorUtility.DisplayDialog("재생 오브젝트를 찾을 수 없어요",
                    "Hierarchy에서 Mouse 씬을 연 다음, Systems의 MouseNoteManager에 " +
                    "MouseModeChartPlayer가 붙어 있는지 확인해 주세요.", "확인");
                return;
            }
            if (!player.gameObject.activeInHierarchy || !player.enabled)
            {
                EditorUtility.DisplayDialog("재생 컴포넌트가 꺼져 있어요",
                    "MouseNoteManager와 MouseModeChartPlayer를 활성화해 주세요.", "확인");
                return;
            }

            SerializedObject serialized = new SerializedObject(player);
            serialized.Update();
            SerializedProperty assetProp = serialized.FindProperty("chartAsset");
            SerializedProperty layerProp = serialized.FindProperty("noteLayer");
            if (assetProp == null || layerProp == null)
            {
                EditorUtility.DisplayDialog("스크립트 버전이 달라요",
                    "기존 MouseModeChartPlayer.cs를 확인해 주세요. " +
                    "현재 에디터가 사용하는 필드를 찾을 수 없어요.", "확인");
                return;
            }

            // 기존 NoteLayer 연결을 우선 유지한다.
            if (layerProp.objectReferenceValue == null)
            {
                RectTransform[] rects = UnityEngine.Object.FindObjectsOfType<RectTransform>(true);
                foreach (RectTransform rt in rects)
                {
                    if (rt.name == "NoteLayer" && rt.gameObject.scene == player.gameObject.scene)
                    {
                        layerProp.objectReferenceValue = rt;
                        break;
                    }
                }
            }
            if (layerProp.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog("NoteLayer 연결이 필요해요",
                    "MouseNoteManager > MouseModeChartPlayer의 Note Layer 필드에 " +
                    "기존 PlayArea/NoteLayer를 연결해 주세요.", "확인");
                return;
            }

            // 채보만 연결하고 기존 Pino, 기타 씬 오브젝트에는 손대지 않는다.
            assetProp.objectReferenceValue = chartAsset;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            SaveChart();

            if (chartAsset.song == null && !EditorUtility.DisplayDialog(
                "음악 없이 테스트할까요?",
                "아직 음악이 연결되지 않았어요. 노트와 판정을 무음으로 테스트할 수 있어요.",
                "무음으로 시작", "취소"))
                return;

            StopPreview();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorApplication.playModeStateChanged -= FocusGameView;
            EditorApplication.playModeStateChanged += FocusGameView;
            EditorApplication.isPlaying = true;
        }

        private static void FocusGameView(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= FocusGameView;
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
                EditorWindow.GetWindow(gameViewType).Focus();
        }

        private void ExportJson()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "JSON 내보내기", chartAsset.data.songId, "json",
                "채보 데이터를 JSON으로 저장합니다. 음악은 에셋에 남습니다.");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(Path.GetFullPath(path),
                JsonUtility.ToJson(chartAsset.data, true));
            AssetDatabase.ImportAsset(path);
            ShowNotification(new GUIContent("JSON 내보내기 완료"));
            Debug.Log("[Mouse Chart] JSON 저장: " + path);
        }

        private void ImportJson()
        {
            string file = EditorUtility.OpenFilePanel("Mouse 채보 JSON", "", "json");
            if (string.IsNullOrEmpty(file)) return;
            try
            {
                MouseModeChartData result =
                    JsonUtility.FromJson<MouseModeChartData>(File.ReadAllText(file));
                if (result == null || result.version != 1 || result.bpm <= 0f)
                    throw new Exception("지원하지 않는 채보 데이터입니다.");
                result.EnsureLists();
                // 현재 채보 데이터를 교체한다. AudioClip은 Unity 에셋에 남겨둔다.
                int existing = 0;
                if (Data != null)
                    existing = (Data.notes != null ? Data.notes.Count : 0) +
                        (Data.attacks != null ? Data.attacks.Count : 0);
                if (existing > 0 && !EditorUtility.DisplayDialog(
                    "현재 채보를 교체할까요?",
                    "현재 에셋의 노트/탄막 " + existing +
                    "개를 JSON 데이터로 교체합니다. 취소하면 아무것도 바뀌지 않습니다.\n" +
                    "필요하다면 먼저 [JSON 내보내기]로 백업하세요.",
                    "가져오기", "취소"))
                    return;
                StopPreview();
                Undo.RecordObject(chartAsset, "Import Mouse Mode Chart JSON");
                chartAsset.data = result;
                selectedNote = selectedAttack = -1;
                viewStartBeat = 0f;
                currentBeat = Mathf.Max(0f, result.notes.Count > 0 ? result.notes[0].beat : 0f);
                EditorUtility.SetDirty(chartAsset);
                AssetDatabase.SaveAssets();
                RememberChart();
                ShowNotification(new GUIContent("JSON 가져오기 완료"));
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("가져오기 실패", ex.Message, "확인");
            }
        }

        private void StartPreview()
        {
            if (chartAsset.song == null) return;
            float songSeconds = (float)MouseModeChartTiming.BeatToSeconds(
                Data, currentBeat);
            songSeconds = Mathf.Clamp(songSeconds, 0f,
                Mathf.Max(0f, chartAsset.song.length - 0.05f));
            if (!MouseModeEditorAudioPreview.Play(chartAsset.song, songSeconds))
            {
                EditorUtility.DisplayDialog("미리듣기 불가",
                    "이 Unity 버전에서는 오디오 미리듣기를 사용할 수 없습니다. " +
                    "박자 편집과 플레이 테스트는 그대로 가능합니다.", "확인");
                return;
            }
            previewPlaying = true;
            previewStartSeconds = songSeconds;
            fallbackPreviewStart = EditorApplication.timeSinceStartup;
        }

        private void StopPreview()
        {
            previewPlaying = false;
            MouseModeEditorAudioPreview.Stop();
        }
    }

    internal static class MouseModeEditorAudioPreview
    {
        private static readonly Type AudioUtil =
            typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static bool Play(AudioClip clip, float seconds)
        {
            if (AudioUtil == null || clip == null) return false;
            try
            {
                Stop();
                MethodInfo method = AudioUtil.GetMethod("PlayPreviewClip", Flags,
                    null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                if (method == null) return false;
                int sample = Mathf.Clamp((int)(seconds * clip.frequency),
                    0, Mathf.Max(0, clip.samples - 1));
                method.Invoke(null, new object[] { clip, sample, false });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Mouse Chart preview: " + e.Message);
                return false;
            }
        }

        public static float CurrentSeconds(AudioClip clip)
        {
            if (AudioUtil == null || clip == null || clip.frequency <= 0) return -1f;
            try
            {
                MethodInfo method = AudioUtil.GetMethod("GetPreviewClipSamplePosition", Flags);
                if (method == null) return -1f;
                return Convert.ToInt32(method.Invoke(null, null)) / (float)clip.frequency;
            }
            catch { return -1f; }
        }

        public static void Stop()
        {
            if (AudioUtil == null) return;
            try
            {
                MethodInfo method = AudioUtil.GetMethod("StopAllPreviewClips", Flags);
                if (method != null) method.Invoke(null, null);
            }
            catch { }
        }
    }
}
#endif
