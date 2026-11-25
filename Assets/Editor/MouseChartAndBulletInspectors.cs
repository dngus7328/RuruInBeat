#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;
using LuluInBeat.MouseMode.ChartSystem.EditorTools;

[CustomEditor(typeof(MouseModeChartAsset))]
public sealed class MouseChartAssetInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("채보 제작은 타임라인 에디터에서 하세요. 아래 필드는 직접 수정할 수도 있습니다.", MessageType.Info);
        if (GUILayout.Button("쉬운 채보 편집기에서 열기", GUILayout.Height(32)))
            RuruSimpleChartEditor.OpenChart((MouseModeChartAsset)target);
        if (GUILayout.Button("고급 편집기 (탄막 포함)"))
            MouseModeChartEditorWindow.OpenChart((MouseModeChartAsset)target);
        DrawDefaultInspector();
    }
}

[CustomEditor(typeof(MouseCustomBulletPattern))]
public sealed class MouseCustomBulletPatternInspector : Editor
{
    private float beat;
    private bool playing;
    private double last;
    private float bpm = 120f;

    [MenuItem("Tools/Lulu In Beat/새 탄막 패턴 만들기")]
    public static void CreatePattern()
    {
        const string folder = "Assets/Resources/MouseBulletPatterns";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Resources", "MouseBulletPatterns");
        var pattern = CreateInstance<MouseCustomBulletPattern>();
        pattern.AssignNewId();
        AssetDatabase.CreateAsset(pattern, AssetDatabase.GenerateUniqueAssetPath(folder + "/BulletPattern.asset"));
        AssetDatabase.SaveAssets();
        Selection.activeObject = pattern;
        EditorGUIUtility.PingObject(pattern);
    }
    private void OnEnable() { EditorApplication.update += Tick; }
    private void OnDisable() { EditorApplication.update -= Tick; }
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (playing) { beat += (float)System.Math.Min(.1, now-last)*bpm/60f; Repaint(); }
        last=now;
    }
    public override void OnInspectorGUI()
    {
        var pattern = (MouseCustomBulletPattern)target;
        EditorGUILayout.HelpBox("발사 묶음을 추가하세요. 반복 횟수, 간격, 탄환 수, 방향, 확산각, 이동 형태를 수정하면 아래 미리보기에 반영됩니다. -90° = 아래, 0° = 오른쪽.", MessageType.Info);
        if (!AssetDatabase.GetAssetPath(pattern).Contains("/Resources/MouseBulletPatterns/"))
            EditorGUILayout.HelpBox("게임에서 선택하려면 Assets/Resources/MouseBulletPatterns 폴더에 저장해야 합니다.",MessageType.Warning);
        serializedObject.Update();
        EditorGUILayout.LabelField("패턴 ID (자동 생성)",pattern.Id,EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"),new GUIContent("패턴 이름"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("alignFirstImpact"),new GUIContent("이벤트 박자 = 첫 위험선 도달"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tutorialTracking"),new GUIContent("초보용 추가 조준탄"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("aimedReplyBeat"),new GUIContent("추가 조준탄 박자 (-1 = 없음)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("volleys"),new GUIContent("발사 묶음"),true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("individualBullets"),new GUIContent("개별 탄환 (복제 / 고급)"),true);
        serializedObject.ApplyModifiedProperties();
        var bullets = pattern.Build();
        float end = 4f;
        foreach (var p in bullets) end=Mathf.Max(end,p.atBeat+p.lifeBeats);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("탄막 궤적 미리보기",EditorStyles.boldLabel);
        bpm = EditorGUILayout.Slider("미리보기 BPM",bpm,60,300);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(playing?"정지":"재생")) { playing=!playing; last=EditorApplication.timeSinceStartup; }
        if (GUILayout.Button("처음")) beat=0;
        EditorGUILayout.EndHorizontal();
        if (beat>end) beat=0;
        beat=EditorGUILayout.Slider("박자",beat,0,end);
        EditorGUILayout.LabelField("발사 탄환 수",bullets.Count.ToString());
        Rect area=GUILayoutUtility.GetAspectRect(1.9f);
        EditorGUI.DrawRect(area,new Color(.12f,.11f,.17f));
        EditorGUI.DrawRect(new Rect(area.x,area.y+area.height*.5f,area.width,area.height*.5f),new Color(.18f,.16f,.24f));
        EditorGUI.DrawRect(new Rect(area.x,area.y+area.height*.72f,area.width,1),new Color(.54f,.48f,.65f));
        if (Event.current.type==EventType.Repaint)
        {
            Handles.BeginGUI();
            foreach(var p in bullets)
            {
                float elapsed=beat-p.atBeat;
                if(elapsed<0||elapsed>p.lifeBeats)continue;
                Vector2 pos=MouseModeBulletPatternLibrary.Position(p,elapsed,area.width,area.height);
                Vector2 at=area.center+new Vector2(pos.x,-pos.y);
                if(!area.Contains(at))continue;
                Handles.color=p.color==1?new Color(.72f,.58f,.88f):p.color==2?new Color(.97f,.71f,.43f):p.color==3?new Color(.54f,.8f,.89f):new Color(.88f,.46f,.52f);
                Handles.DrawSolidDisc(at,Vector3.forward,3);
            }
            Handles.EndGUI();
        }
        if(GUILayout.Button("저장")){EditorUtility.SetDirty(pattern);AssetDatabase.SaveAssets();}
        if(GUILayout.Button("복제본에 새 ID 부여") && EditorUtility.DisplayDialog("패턴 ID 변경","이미 채보에서 사용 중인 원본에는 사용하지 마세요. 복제본 전용입니다.","변경","취소"))
        {Undo.RecordObject(pattern,"New pattern ID");pattern.AssignNewId();EditorUtility.SetDirty(pattern);AssetDatabase.SaveAssets();}
    }
}

[CustomPropertyDrawer(typeof(MouseCustomBulletPattern.Volley))]
public sealed class MouseVolleyDrawer : PropertyDrawer
{
    private static readonly string[] Fields={"startBeat","repeat","intervalBeats","origin","originStep","bullets","direction","aimAtPino","spread","rotationPerRepeat","speed","acceleration","waveAmplitude","waveCyclesPerBeat","wavePhaseRadians","turnAfterBeats","turnAngle","pauseAfterBeats","pauseBeats","lifetime","color"};
    private static readonly string[] Labels={"발사 시작 박자","반복 횟수","반복 간격 (박)","발사 위치 (0~1)","반복마다 위치 이동","한 번에 탄환 수","발사 방향 (도)","피노 조준 (발사 순간)","확산 각도","반복마다 회전","속도 (화면 높이/박)","가속도","곡선 진폭","곡선 반복/박","곡선 위상","방향 전환 시점 (박)","방향 전환 각도","정지 시작 (박)","정지 길이 (박)","수명 (박)","탄환 색 (0~3)"};
    public override float GetPropertyHeight(SerializedProperty property,GUIContent label)
    {
        float h=EditorGUIUtility.singleLineHeight;
        if(property.isExpanded)foreach(string field in Fields)h+=EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field),true)+EditorGUIUtility.standardVerticalSpacing;
        return h;
    }
    public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
    {
        EditorGUI.BeginProperty(position,label,property);
        Rect row=new Rect(position.x,position.y,position.width,EditorGUIUtility.singleLineHeight);
        property.isExpanded=EditorGUI.Foldout(row,property.isExpanded,label,true);
        if(property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for(int i=0;i<Fields.Length;i++)
            {
                row.y+=row.height+EditorGUIUtility.standardVerticalSpacing;
                var value=property.FindPropertyRelative(Fields[i]);
                row.height=EditorGUI.GetPropertyHeight(value,true);
                EditorGUI.PropertyField(row,value,new GUIContent(Labels[i]),true);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
#endif
