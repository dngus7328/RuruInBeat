#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Capture the actual composed screen, not a screenshot. The result is a
// normal editable prefab with RectTransforms, Images, text and buttons.
public sealed class RuruUILayoutEditor : EditorWindow
{
    const string Folder="Assets/Resources/RuruUILayouts";
    [MenuItem("Tools/Lulu In Beat/UI 편집 프리팹")]
    public static void Open(){GetWindow<RuruUILayoutEditor>("UI 편집");}

    private void OnGUI()
    {
        var scene=SceneManager.GetActiveScene();
        string path=Folder+"/"+scene.name+".prefab";
        EditorGUILayout.LabelField("현재 씬",scene.name);
        EditorGUILayout.HelpBox("Play 중 편집할 화면을 띄운 뒤 'UI 프리팹 만들기'를 누르세요. Play를 끝내고 저장된 프리팹을 열어 Hierarchy 및 Inspector에서 편집합니다. 게임 동작은 기존 코드가 담당하고, 프리팹에서 바꾼 값만 실제 화면에 반영됩니다.",MessageType.Info);
        using(new EditorGUI.DisabledScope(!Application.isPlaying))
            if(GUILayout.Button("현재 씬 UI 프리팹 만들기 / 업데이트",GUILayout.Height(34)))Capture(scene,path);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        using(new EditorGUI.DisabledScope(prefab==null||Application.isPlaying))
            if(GUILayout.Button("UI 프리팹 편집",GUILayout.Height(32)))AssetDatabase.OpenAsset(prefab);
        if(prefab!=null)EditorGUILayout.ObjectField("편집용 프리팹",prefab,typeof(GameObject),false);
        if(!Application.isPlaying && AssetDatabase.IsValidFolder(Folder))
        {
            EditorGUILayout.LabelField("저장된 모든 화면",EditorStyles.boldLabel);
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{Folder}))
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if(asset!=null && GUILayout.Button(asset.name+" 편집"))AssetDatabase.OpenAsset(asset);
            }
        }
        EditorGUILayout.HelpBox("노트, 탄환, 판정 이펙트처럼 계속 생기는 물체는 채보/탄막 에디터와 해당 컴포넌트의 Inspector에서 수정합니다. 이 프리팹에는 정적인 UI만 저장합니다. 버튼 동작이나 점수 데이터는 복제하지 않습니다.",MessageType.None);
    }

    private static bool Skip(string name)
    {
        return name.StartsWith("MouseNote_")||name.StartsWith("Judge_")||
            name.StartsWith("AdvancedBullet")||name.StartsWith("Enemy01_")||
            name.StartsWith("AttackWarning")||name.StartsWith("JudgementVfx")||
            name.StartsWith("TapPulse")||name.StartsWith("PinoHIT_")||
            name.StartsWith("Bullet_")||name.StartsWith("Projectile_");
    }

    private static void Capture(Scene scene,string path)
    {
        if(!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources","RuruUILayouts");
        var previous=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var old=new Dictionary<string,RuruUILayoutElement>();
        if(previous!=null)
            foreach(var e in previous.GetComponentsInChildren<RuruUILayoutElement>(true))old[e.TargetPath]=e;
        var sourceCanvases=new List<Canvas>();
        foreach(var root in scene.GetRootGameObjects())
            foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))
                if(canvas.isRootCanvas)sourceCanvases.Add(canvas);
        if(sourceCanvases.Count==0){EditorUtility.DisplayDialog("UI 편집","이 씬에는 Canvas가 없습니다.","확인");return;}
        Canvas.ForceUpdateCanvases();
        var temp=new GameObject(scene.name+"_UILayout",typeof(RectTransform));
        try
        {
            foreach(var canvas in sourceCanvases)
            {
                var rect=Clone(canvas.transform as RectTransform,temp.transform,old);
                if(rect==null)continue;
                var c=rect.gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;
                c.sortingOrder=canvas.sortingOrder;
                var from=canvas.GetComponent<CanvasScaler>();
                if(from!=null)
                {
                    var scaler=rect.gameObject.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode=from.uiScaleMode;scaler.referenceResolution=from.referenceResolution;
                    scaler.screenMatchMode=from.screenMatchMode;scaler.matchWidthOrHeight=from.matchWidthOrHeight;
                    scaler.referencePixelsPerUnit=from.referencePixelsPerUnit;scaler.scaleFactor=from.scaleFactor;
                }
            }
            // Never silently delete an edited element if its lazy-built panel
            // has not been opened in this capture.
            var captured=new HashSet<string>();
            foreach(var element in temp.GetComponentsInChildren<RuruUILayoutElement>(true))captured.Add(element.TargetPath);
            foreach(var prior in old.Keys)
                if(!captured.Contains(prior))
                {
                    EditorUtility.DisplayDialog("기존 UI 보존","이전에 저장한 UI 일부가 현재 화면에 없습니다. 해당 메뉴를 먼저 열어 주세요. 기존 프리팹은 그대로 유지됩니다.","확인");
                    return;
                }
            var saved=PrefabUtility.SaveAsPrefabAsset(temp,path);
            if(saved==null)throw new System.Exception("프리팹 저장 실패");
            AssetDatabase.SaveAssets();
            Selection.activeObject=saved;
            EditorGUIUtility.PingObject(saved);
        }
        finally{DestroyImmediate(temp);}
    }

    private static RectTransform Clone(RectTransform source,Transform parent,Dictionary<string,RuruUILayoutElement> old)
    {
        if(source==null||Skip(source.name))return null;
        var go=new GameObject(source.name,typeof(RectTransform));
        var rt=(RectTransform)go.transform;rt.SetParent(parent,false);
        rt.anchorMin=source.anchorMin;rt.anchorMax=source.anchorMax;rt.pivot=source.pivot;
        rt.sizeDelta=source.sizeDelta;rt.anchoredPosition3D=source.anchoredPosition3D;
        rt.localScale=source.localScale;rt.localRotation=source.localRotation;
        var fromImage=source.GetComponent<Image>();
        if(fromImage!=null){
            var i=go.AddComponent<Image>();
            if(fromImage.sprite!=null&&EditorUtility.IsPersistent(fromImage.sprite))i.sprite=fromImage.sprite;
            i.color=fromImage.color;i.type=fromImage.type;i.preserveAspect=fromImage.preserveAspect;
            i.raycastTarget=false;i.fillMethod=fromImage.fillMethod;i.fillAmount=fromImage.fillAmount;
        }
        var fromText=source.GetComponent<TMP_Text>();
        if(fromText!=null){
            var t=go.AddComponent<TextMeshProUGUI>();
            t.font=fromText.font;t.text=fromText.text;t.color=fromText.color;t.fontSize=fromText.fontSize;
            t.fontSizeMin=fromText.fontSizeMin;t.fontSizeMax=fromText.fontSizeMax;
            t.enableAutoSizing=fromText.enableAutoSizing;t.enableWordWrapping=fromText.enableWordWrapping;
            t.alignment=fromText.alignment;t.fontStyle=fromText.fontStyle;t.overflowMode=fromText.overflowMode;t.raycastTarget=false;
        }
        var fromButton=source.GetComponent<Button>();
        if(fromButton!=null){
            var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<Graphic>();
            b.colors=fromButton.colors;b.transition=fromButton.transition;
        }
        var element=go.AddComponent<RuruUILayoutElement>();
        string path=RuruUILayoutElement.PathOf(source);
        element.CaptureBaseline(path);
        if(old.TryGetValue(path,out var existing)){existing.ApplyTo(rt);element.PreserveEditedBaseline(existing);}
        foreach(Transform child in source)Clone(child as RectTransform,rt,old);
        go.SetActive(source.gameObject.activeSelf);
        return rt;
    }
}
#endif
