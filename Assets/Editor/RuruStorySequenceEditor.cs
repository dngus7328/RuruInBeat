#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RuruStorySequence))]
public sealed class RuruStorySequenceEditor : Editor
{
    private int current;
    private SerializedProperty lines;
    private void OnEnable() { lines = serializedObject.FindProperty("lines"); }

    [MenuItem("Tools/Ruru In Beat/스토리/튜토리얼 도입부 편집")]
    private static void OpenTutorial()
    {
        const string folder = "Assets/Resources/Story";
        const string path = folder + "/TutorialOpening.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Resources", "Story");
        RuruStorySequence asset = AssetDatabase.LoadAssetAtPath<RuruStorySequence>(path);
        if (asset == null)
        {
            asset = CreateInstance<RuruStorySequence>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneTitle"), new GUIContent("장면 제목 (선택)"));
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("직접 작성한 대사와 별도 배경/정면 캐릭터 Sprite를 넣으세요. 비어 있는 이미지 칸은 이전 장면의 이미지를 유지합니다. 지우기 항목을 체크하면 제거됩니다.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("대사 추가"))
        {
            serializedObject.ApplyModifiedProperties();
            RuruStorySequence asset = (RuruStorySequence)target;
            Undo.RecordObject(asset, "Add story line");
            asset.lines.Add(new RuruStorySequence.Line());
            EditorUtility.SetDirty(asset);
            current = asset.Count - 1;
            serializedObject.Update();
        }
        if (GUILayout.Button("이전")) current = Mathf.Max(0, current - 1);
        if (GUILayout.Button("다음")) current = Mathf.Min(Mathf.Max(0,lines.arraySize - 1),current + 1);
        EditorGUILayout.EndHorizontal();
        if (lines.arraySize == 0)
        {
            EditorGUILayout.HelpBox("아직 작성된 대사가 없습니다.", MessageType.None);
            serializedObject.ApplyModifiedProperties(); return;
        }
        current = Mathf.Clamp(current, 0, lines.arraySize - 1);
        current = EditorGUILayout.IntSlider("편집 대사", current + 1, 1, lines.arraySize) - 1;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("위로 이동") && current > 0)
        { lines.MoveArrayElement(current,current-1); current--; }
        if (GUILayout.Button("아래로 이동") && current < lines.arraySize - 1)
        { lines.MoveArrayElement(current,current+1); current++; }
        if (GUILayout.Button("삭제") && EditorUtility.DisplayDialog("대사 삭제","이 대사를 삭제할까요?","삭제","취소"))
        { lines.DeleteArrayElementAtIndex(current); current = Mathf.Max(0,current-1); }
        EditorGUILayout.EndHorizontal();
        if (lines.arraySize == 0) { serializedObject.ApplyModifiedProperties(); return; }
        current = Mathf.Clamp(current, 0, lines.arraySize - 1);
        SerializedProperty line = lines.GetArrayElementAtIndex(current);
        Field(line,"speaker","화자");
        Field(line,"dialogue","대사");
        Field(line,"background","배경 일러스트");
        Field(line,"clearBackground","배경 지우기");
        Field(line,"leftPortrait","왼쪽 정면 일러스트");
        Field(line,"clearLeft","왼쪽 인물 퇴장");
        Field(line,"rightPortrait","오른쪽 정면 일러스트");
        Field(line,"clearRight","오른쪽 인물 퇴장");
        Field(line,"speaking","화자 위치");
        Field(line,"entrance","등장 연출");
        Field(line,"transitionSeconds","연출 시간");
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("이미지 구성 미리보기", EditorStyles.boldLabel);
        Rect preview = GUILayoutUtility.GetAspectRect(16f/9f);
        EditorGUI.DrawRect(preview, new Color(.12f,.12f,.13f));
        RuruStorySequence.Frame state = ((RuruStorySequence)target).Resolve(current);
        DrawSprite(preview, state.background, false);
        DrawSprite(new Rect(preview.x+preview.width*.035f,preview.y+preview.height*.04f,preview.width*.455f,preview.height*.83f),state.left,true);
        DrawSprite(new Rect(preview.x+preview.width*.51f,preview.y+preview.height*.04f,preview.width*.455f,preview.height*.83f),state.right,true);
        EditorGUILayout.HelpBox("미리보기는 이미지 배치 확인용입니다. 실제 대사 진행은 Tutorial 씬에서 확인할 수 있습니다.",MessageType.None);
    }

    private static void Field(SerializedProperty root,string key,string label)
    { EditorGUILayout.PropertyField(root.FindPropertyRelative(key),new GUIContent(label)); }

    private static void DrawSprite(Rect destination, Sprite sprite, bool fit)
    {
        if (sprite == null || sprite.texture == null) return;
        Rect r = sprite.rect;
        if (fit)
        {
            float scale = Mathf.Min(destination.width/r.width,destination.height/r.height);
            float w=r.width*scale,h=r.height*scale;
            destination=new Rect(destination.x+(destination.width-w)*.5f,destination.y+destination.height-h,w,h);
        }
        Rect uv = new Rect(r.x/sprite.texture.width,r.y/sprite.texture.height,r.width/sprite.texture.width,r.height/sprite.texture.height);
        GUI.DrawTextureWithTexCoords(destination,sprite.texture,uv,true);
    }
}
#endif
