using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// An editable copy of a screen element. The prefab is a design reference,
// never instantiated in the game. Only changes from the captured baseline
// are applied, so scores, live text, jacket changes and animations remain live.
[DisallowMultipleComponent]
public sealed class RuruUILayoutElement : MonoBehaviour
{
    [SerializeField] private string targetPath;
    [SerializeField, HideInInspector] private Vector2 anchorMin, anchorMax, pivot, size;
    [SerializeField, HideInInspector] private Vector3 position, scale, rotation;
    [SerializeField, HideInInspector] private Color graphicColor, textColor;
    [SerializeField, HideInInspector] private Sprite sprite;
    [SerializeField, HideInInspector] private Image.Type imageType;
    [SerializeField, HideInInspector] private bool preserveAspect;
    [SerializeField, HideInInspector] private TMP_FontAsset font;
    [SerializeField, HideInInspector] private string label;
    [SerializeField, HideInInspector] private float fontSize, fontMin, fontMax;
    [SerializeField, HideInInspector] private bool autoSize, wordWrap;
    [SerializeField, HideInInspector] private TextAlignmentOptions alignment;
    [SerializeField, HideInInspector] private FontStyles fontStyle;
    [SerializeField, HideInInspector] private ColorBlock buttonColors;
    [SerializeField, HideInInspector] private Selectable.Transition buttonTransition;
    public string TargetPath => targetPath;

    public static string PathOf(Transform t)
    {
        var names = new List<string>();
        while(t!=null)
        {
            int occurrence=0;
            if(t.parent!=null)
                for(int i=0;i<t.GetSiblingIndex();i++)
                    if(t.parent.GetChild(i).name==t.name) occurrence++;
            names.Add(Uri.EscapeDataString(t.name)+"#"+occurrence);
            t=t.parent;
        }
        names.Reverse();
        return string.Join("/",names.ToArray());
    }

    public void CaptureBaseline(string path)
    {
        targetPath=path;
        var rt=(RectTransform)transform;
        anchorMin=rt.anchorMin; anchorMax=rt.anchorMax; pivot=rt.pivot;
        size=rt.sizeDelta;position=rt.anchoredPosition3D;scale=rt.localScale;rotation=rt.localEulerAngles;
        var image=GetComponent<Image>();
        if(image!=null){graphicColor=image.color;sprite=image.sprite;imageType=image.type;preserveAspect=image.preserveAspect;}
        var text=GetComponent<TMP_Text>();
        if(text!=null){textColor=text.color;font=text.font;label=text.text;fontSize=text.fontSize;fontMin=text.fontSizeMin;fontMax=text.fontSizeMax;autoSize=text.enableAutoSizing;wordWrap=text.enableWordWrapping;alignment=text.alignment;fontStyle=text.fontStyle;}
        var button=GetComponent<Button>();
        if(button!=null){buttonColors=button.colors;buttonTransition=button.transition;}
    }

    public void PreserveEditedBaseline(RuruUILayoutElement previous)
    {
        if(previous==null)return;
        var design=(RectTransform)previous.transform;
        if(design.anchorMin!=previous.anchorMin)anchorMin=previous.anchorMin;
        if(design.anchorMax!=previous.anchorMax)anchorMax=previous.anchorMax;
        if(design.pivot!=previous.pivot)pivot=previous.pivot;
        if(design.sizeDelta!=previous.size)size=previous.size;
        if(design.anchoredPosition3D!=previous.position)position=previous.position;
        if(design.localScale!=previous.scale)scale=previous.scale;
        if(design.localEulerAngles!=previous.rotation)rotation=previous.rotation;
        var i=previous.GetComponent<Image>();
        if(i!=null){if(i.color!=previous.graphicColor)graphicColor=previous.graphicColor;if(i.sprite!=previous.sprite)sprite=previous.sprite;if(i.type!=previous.imageType)imageType=previous.imageType;if(i.preserveAspect!=previous.preserveAspect)preserveAspect=previous.preserveAspect;}
        var t=previous.GetComponent<TMP_Text>();
        if(t!=null){
            if(t.color!=previous.textColor)textColor=previous.textColor;
            if(t.font!=previous.font)font=previous.font;
            if(t.text!=previous.label)label=previous.label;
            if(!Mathf.Approximately(t.fontSize,previous.fontSize))fontSize=previous.fontSize;
            if(!Mathf.Approximately(t.fontSizeMin,previous.fontMin))fontMin=previous.fontMin;
            if(!Mathf.Approximately(t.fontSizeMax,previous.fontMax))fontMax=previous.fontMax;
            if(t.enableAutoSizing!=previous.autoSize)autoSize=previous.autoSize;
            if(t.enableWordWrapping!=previous.wordWrap)wordWrap=previous.wordWrap;
            if(t.alignment!=previous.alignment)alignment=previous.alignment;
            if(t.fontStyle!=previous.fontStyle)fontStyle=previous.fontStyle;
        }
        var b=previous.GetComponent<Button>();
        if(b!=null){if(!b.colors.Equals(previous.buttonColors))buttonColors=previous.buttonColors;if(b.transition!=previous.buttonTransition)buttonTransition=previous.buttonTransition;}
    }

    // Do not apply unedited properties. This also means reusing a template
    // does not revert a newer game default or freeze a dynamic value.
    public void ApplyTo(RectTransform rt,bool layoutOnly=false)
    {
        if(rt==null)return;
        var design=(RectTransform)transform;
        if(design.anchorMin!=anchorMin)rt.anchorMin=design.anchorMin;
        if(design.anchorMax!=anchorMax)rt.anchorMax=design.anchorMax;
        if(design.pivot!=pivot)rt.pivot=design.pivot;
        if(design.sizeDelta!=size)rt.sizeDelta=design.sizeDelta;
        if(design.anchoredPosition3D!=position)rt.anchoredPosition3D=design.anchoredPosition3D;
        if(design.localScale!=scale)rt.localScale=design.localScale;
        if(design.localEulerAngles!=rotation)rt.localEulerAngles=design.localEulerAngles;
        if(layoutOnly)return;
        var src=GetComponent<Image>();var dst=rt.GetComponent<Image>();
        if(src!=null&&dst!=null){
            if(src.color!=graphicColor)dst.color=src.color;
            if(src.sprite!=sprite)dst.sprite=src.sprite;
            if(src.type!=imageType)dst.type=src.type;
            if(src.preserveAspect!=preserveAspect)dst.preserveAspect=src.preserveAspect;
        }
        var st=GetComponent<TMP_Text>();var dt=rt.GetComponent<TMP_Text>();
        if(st!=null&&dt!=null){
            if(st.color!=textColor)dt.color=st.color;
            if(st.font!=font&&st.font!=null)dt.font=st.font;
            if(st.text!=label)dt.text=st.text;
            if(!Mathf.Approximately(st.fontSize,fontSize))dt.fontSize=st.fontSize;
            if(!Mathf.Approximately(st.fontSizeMin,fontMin))dt.fontSizeMin=st.fontSizeMin;
            if(!Mathf.Approximately(st.fontSizeMax,fontMax))dt.fontSizeMax=st.fontSizeMax;
            if(st.enableAutoSizing!=autoSize)dt.enableAutoSizing=st.enableAutoSizing;
            if(st.enableWordWrapping!=wordWrap)dt.enableWordWrapping=st.enableWordWrapping;
            if(st.alignment!=alignment)dt.alignment=st.alignment;
            if(st.fontStyle!=fontStyle)dt.fontStyle=st.fontStyle;
        }
        var sb=GetComponent<Button>();var db=rt.GetComponent<Button>();
        if(sb!=null&&db!=null){
            if(!sb.colors.Equals(buttonColors))db.colors=sb.colors;
            if(sb.transition!=buttonTransition)db.transition=sb.transition;
        }
    }
}

public static class RuruEditableUILayout
{
    private static readonly Dictionary<string,Dictionary<string,RuruUILayoutElement>> cache =
        new Dictionary<string,Dictionary<string,RuruUILayoutElement>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset(){cache.Clear();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register(){SceneManager.sceneLoaded-=OnScene;SceneManager.sceneLoaded+=OnScene;}

    private static Dictionary<string,RuruUILayoutElement> Load(string scene)
    {
        if(cache.TryGetValue(scene,out var map))return map;
        map=new Dictionary<string,RuruUILayoutElement>();
        var prefab=Resources.Load<GameObject>("RuruUILayouts/"+scene);
        if(prefab!=null)
            foreach(var item in prefab.GetComponentsInChildren<RuruUILayoutElement>(true))
                if(!string.IsNullOrEmpty(item.TargetPath))map[item.TargetPath]=item;
        cache[scene]=map;
        return map;
    }
    public static void Apply(RectTransform rect,bool layoutOnly=false)
    {
        if(!Application.isPlaying||rect==null||!rect.gameObject.scene.IsValid())return;
        var map=Load(rect.gameObject.scene.name);
        if(map.Count>0&&map.TryGetValue(RuruUILayoutElement.PathOf(rect),out var item))
            item.ApplyTo(rect,layoutOnly);
    }
    private static void OnScene(Scene scene,LoadSceneMode mode)
    {
        var map=Load(scene.name);
        if(map.Count==0)return;
        var root=new GameObject("RuruEditableUILayout_Runtime");
        SceneManager.MoveGameObjectToScene(root,scene);
        root.AddComponent<RuruUILayoutApplyRunner>().Initialize(map);
    }
}

[DefaultExecutionOrder(32100)]
public sealed class RuruUILayoutApplyRunner : MonoBehaviour
{
    private Dictionary<string,RuruUILayoutElement> map;
    private readonly HashSet<int> applied=new HashSet<int>();
    private float nextScan;
    public void Initialize(Dictionary<string,RuruUILayoutElement> items){map=items;}
    private void LateUpdate()
    {
        if(map==null||Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+.35f;
        // Existing scene UI and components manually added after Region() are
        // picked up here, including screens first opened later in the song.
        foreach(var root in gameObject.scene.GetRootGameObjects())
            foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if(!canvas.isRootCanvas)continue;
                foreach(var rt in canvas.GetComponentsInChildren<RectTransform>(true))
                {
                    int id=rt.GetInstanceID();
                    if(applied.Contains(id))continue;
                    if(map.TryGetValue(RuruUILayoutElement.PathOf(rt),out var design))
                    {
                        design.ApplyTo(rt);
                        applied.Add(id);
                    }
                }
            }
    }
}
