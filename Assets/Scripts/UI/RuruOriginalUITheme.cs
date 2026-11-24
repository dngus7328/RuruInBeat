using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// Shared references to the project's existing, user-authored TitleUI sprites.
/// These assets remain in Assets/Image/TitleUI and keep their original GUIDs.
///
/// The theme asset lives under Resources so runtime-created screens can reuse
/// the same illustrations without duplicating or restyling any PNG.
/// </summary>
[CreateAssetMenu(menuName = "Ruru In Beat/Original UI Theme",
    fileName = "RuruOriginalUITheme")]
public sealed class RuruOriginalUITheme : ScriptableObject
{
    [Header("Existing TitleUI artwork - no generated replacements")]
    public Sprite titleButton;
    public Sprite selectedCategory;
    public Sprite confirmationPanel;
    public TMP_FontAsset interfaceFont;
    public TMP_FontAsset displayFont;
}


/// <summary>Hand-drawn pixel geometry for the matte handheld-console interface.
/// Each independent sprite is nine-sliced; text and button states stay live.</summary>
public static class RuruConsoleUI
{
    public static readonly Color Ink = new Color32(36, 29, 52, 255);
    public static readonly Color Paper = new Color32(241, 235, 249, 255);
    public static readonly Color Muted = new Color32(191, 180, 211, 255);
    public static readonly Color Lilac = new Color32(192, 172, 229, 255);
    public static readonly Color Mint = new Color32(157, 222, 201, 255);
    public static readonly Color Rose = new Color32(247, 166, 183, 255);
    private static Sprite shell, inset, mainKey, altKey;
    public static Sprite Shell => shell != null ? shell : (shell = Draw("ConsoleShell", new Color32(48,39,66,255),new Color32(133,112,170,255),new Color32(69,56,91,255)));
    public static Sprite Inset => inset != null ? inset : (inset = Draw("ConsoleInset",new Color32(35,29,49,255),new Color32(91,77,116,255),new Color32(44,36,62,255)));
    public static Sprite MainKey => mainKey != null ? mainKey : (mainKey = Draw("ConsolePrimary",new Color32(193,174,228,255),new Color32(227,215,247,255),new Color32(165,143,207,255)));
    public static Sprite AltKey => altKey != null ? altKey : (altKey = Draw("ConsoleSecondary",new Color32(64,52,86,255),new Color32(135,115,170,255),new Color32(49,40,70,255)));

    // A clipped-corner contour with a two-pixel outer stroke and a quiet
    // inner bevel. The center is flat; no glow, gradient or baked lettering.
    private static bool Inside(float x,float y,float insetPx)
    {
        float d = Mathf.Min(Mathf.Min(x,y), Mathf.Min(64f-x,64f-y));
        float c = Mathf.Min(Mathf.Min(x+y,64f-x+y),Mathf.Min(x+64f-y,128f-x-y));
        return d >= insetPx && c >= 11f + insetPx * 1.414214f;
    }

    private static Sprite Draw(string name, Color32 fill, Color32 edge, Color32 lip)
    {
        const int size=64;
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.name=name;
        texture.hideFlags=HideFlags.HideAndDontSave;
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.filterMode=FilterMode.Bilinear;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)
        for(int x=0;x<size;x++)
        {
            int coverage=0; Color rgb=Color.clear;
            for(int sy=0;sy<2;sy++)
            for(int sx=0;sx<2;sx++)
            {
                float u=x+.25f+sx*.5f, v=y+.25f+sy*.5f;
                if(!Inside(u,v,0)) continue;
                coverage++;
                rgb += (Color)(!Inside(u,v,2) ? edge : !Inside(u,v,4) ? lip : fill);
            }
            if(coverage>0)
            {
                rgb /= coverage; rgb.a=coverage/4f;
                pixels[y*size+x]=rgb;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
        sprite.name=name;
        sprite.hideFlags=HideFlags.HideAndDontSave;
        return sprite;
    }

    public static RectTransform Region(Transform parent,string name,float l,float b,float r,float t)
    {
        var go=new GameObject(name,typeof(RectTransform));
        go.layer=parent.gameObject.layer;
        var rt=go.GetComponent<RectTransform>();
        rt.SetParent(parent,false);
        rt.anchorMin=new Vector2(l,b); rt.anchorMax=new Vector2(r,t);
        rt.offsetMin=rt.offsetMax=Vector2.zero;
        return rt;
    }
    public static RectTransform Stage(Transform parent,string name)
    {
        var rt=Region(parent,name,0,0,1,1);
        var fit=rt.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio=16f/9f;
        return rt;
    }
    public static Image Frame(Transform parent,string name,float l,float b,float r,float t,Sprite sprite)
    {
        var image=Region(parent,name,l,b,r,t).gameObject.AddComponent<Image>();
        image.sprite=sprite; image.type=Image.Type.Sliced;
        image.raycastTarget=false; return image;
    }
    public static Image Strip(Transform parent,string name,float l,float b,float r,float t,Color color)
    {
        var image=Region(parent,name,l,b,r,t).gameObject.AddComponent<Image>();
        image.color=color; image.raycastTarget=false; return image;
    }
    public static TMP_FontAsset Font(bool display=false)
    {
        var theme=Resources.Load<RuruOriginalUITheme>("RuruOriginalUITheme");
        if(theme!=null)
        {
            var f=display ? theme.displayFont : theme.interfaceFont;
            if(f!=null) return f;
        }
        return TMP_Settings.defaultFontAsset;
    }
    public static TMP_Text Text(Transform parent,string name,string value,float l,float b,float r,float t,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft,bool display=false)
    {
        var label=Region(parent,name,l,b,r,t).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=Font(display);
        label.text=value; label.color=color; label.alignment=alignment;
        label.enableWordWrapping=false; label.enableAutoSizing=true;
        label.fontSize=size; label.fontSizeMin=size*.50f; label.fontSizeMax=size;
        label.overflowMode=TextOverflowModes.Ellipsis; label.raycastTarget=false;
        return label;
    }
    public static Button Key(Transform parent,string name,string title,float l,float b,float r,float t,bool primary,UnityAction action)
    {
        var image=Frame(parent,name,l,b,r,t,primary ? MainKey : AltKey);
        image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>();
        button.targetGraphic=image;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=button.colors;
        colors.normalColor=Color.white; colors.highlightedColor=new Color(.91f,.94f,1f);
        colors.pressedColor=new Color(.72f,.72f,.83f); colors.selectedColor=colors.highlightedColor;
        colors.fadeDuration=.09f; button.colors=colors;
        Text(image.transform,"Caption",title,.06f,.12f,.94f,.88f,30,primary ? Ink : Paper,TextAlignmentOptions.Center);
        button.onClick.AddListener(action);
        return button;
    }
}
