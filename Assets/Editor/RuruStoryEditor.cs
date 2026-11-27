using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RuruStoryEditor
{
    [MenuItem("Tools/Lulu In Beat/스토리/1. 튜토리얼 대사 편집")]
    public static void OpenTutorial()
    {
        Select("Assets/Resources/TutorialStory.asset");
    }

    [MenuItem("Tools/Lulu In Beat/스토리/2. 새 스토리 만들기")]
    public static void NewStory()
    {
        string path = EditorUtility.SaveFilePanelInProject("새 스토리 만들기", "NewStory", "asset",
            "스토리 데이터가 저장될 위치를 선택하세요.");
        if (string.IsNullOrEmpty(path)) return;
        var story = ScriptableObject.CreateInstance<StoryData>();
        story.title = System.IO.Path.GetFileNameWithoutExtension(path);
        story.storyId = System.Guid.NewGuid().ToString("N");
        story.lines = new[] { new StoryLine() };
        AssetDatabase.CreateAsset(story, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = story;
        EditorGUIUtility.PingObject(story);
    }

    [MenuItem("Tools/Lulu In Beat/스토리/3. 스토리 연결 설정")]
    public static void OpenRoutes()
    {
        Select("Assets/Resources/StoryFlowSettings.asset");
    }

    [MenuItem("Tools/Lulu In Beat/스토리/4. 스토리 씬 열기 및 UI 편집 준비")]
    public static void OpenStoryScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string path = "Assets/Scene/StoryScene.unity";
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        StorySceneController controller = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            controller = root.GetComponentInChildren<StorySceneController>(true);
            if (controller != null) break;
        }
        if (controller == null)
        {
            Debug.LogError("StorySceneController가 없습니다.");
            return;
        }
        var serialized = new SerializedObject(controller);
        if (serialized.FindProperty("storyCanvas").objectReferenceValue == null)
        {
            Undo.RegisterFullObjectHierarchyUndo(controller.gameObject, "스토리 UI 만들기");
            controller.BuildUI();
            serialized.Update();
            var canvas = serialized.FindProperty("storyCanvas").objectReferenceValue as Canvas;
            if (canvas != null) Undo.RegisterCreatedObjectUndo(canvas.gameObject, "스토리 Canvas 만들기");
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Selection.activeGameObject = controller.gameObject;
        EditorGUIUtility.PingObject(controller);
    }

    private static void Select(string path)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        if (asset == null) { Debug.LogError("스토리 에셋이 없습니다: " + path); return; }
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}

[CustomEditor(typeof(StoryData))]
public sealed class RuruStoryDataInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Field("storyId", "스토리 고유 ID");
        Field("title", "스토리 제목");
        Field("background", "기본 배경");
        Field("music", "배경 음악");
        Field("secondsPerCharacter", "한 글자 표시 시간 (초)");
        EditorGUILayout.HelpBox("Lines의 Size 또는 + / - 버튼으로 대사를 추가·삭제하고, 순서를 바꾸세요. Speaker는 화자, Text는 대사, Expression은 표정 번호입니다. Left/Right Portrait에 직접 이미지를 넣으면 공용 표정 대신 표시합니다.", MessageType.Info);
        Field("lines", "대사 목록");
        serializedObject.ApplyModifiedProperties();
    }

    private void Field(string name, string label)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
    }
}

[CustomEditor(typeof(StoryFlowSettings))]
public sealed class RuruStoryFlowInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("BeforeTutorial: 최초 튜토리얼 전 / AfterTutorial: 튜토리얼 종료 후 / AfterSongClear: 곡 클리어 결과에서 곡 선택 버튼을 누를 때. 여러 항목을 추가하면 등록 순서대로 재생됩니다. Story가 비어 있으면 무시합니다. Chart를 비우면 모든 곡에 적용됩니다.", MessageType.Info);
        DrawDefaultInspector();
    }
}

[CustomEditor(typeof(TutorialStoryData))]
public sealed class RuruTutorialStoryInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Opening/Closing의 대사는 자유롭게 추가·삭제·정렬할 수 있습니다. ChapterNames/Guides/Success/Lead는 실제 채보의 실습 6단계와 연결되어 있으므로 배열의 순서를 유지하세요. IncompleteClosing을 비우면 실패 여부와 관계없이 첫 종료 대사가 표시됩니다.", MessageType.Info);
        DrawDefaultInspector();
    }
}
