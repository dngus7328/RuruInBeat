#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reproducible production PNGs for the mouse-only UI. Creates individual
/// transparent sprites rather than a single atlas or mockup.
/// Existing files are NEVER overwritten automatically.
/// </summary>
[InitializeOnLoad]
public static class LuluMouseArtGenerator
{
    const string Root = "Assets/Resources/RuruArt/";
    static readonly Color32 Ink = C(38,25,55);
    static readonly Color32 Shadow = C(67,48,85);
    static readonly Color32 Mid = C(113,85,142);
    static readonly Color32 Lilac = C(206,184,245);
    static readonly Color32 Cream = C(246,240,252);
    static readonly Color32 Pink = C(242,169,206);
    static readonly Color32 Mint = C(162,223,212);
    static readonly Color32 Clear = C(0,0,0,0);

    static LuluMouseArtGenerator()
    {
        EditorApplication.delayCall += GenerateMissing;
    }

    [MenuItem("Tools/Lulu in Beat/Generate missing mouse-mode UI PNGs")]
    public static void GenerateMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Write("UI/UI_Panel", Panel(), new Vector4(60, 42, 60, 42));
        Write("UI/UI_Button", Button(), new Vector4(44, 25, 44, 25));
        Write("UI/Stability_Frame", GaugeFrame(), new Vector4(56, 30, 56, 30));
        Write("UI/Stability_Track", GaugeTrack(), new Vector4(24, 14, 24, 14));
        Write("UI/Stability_Fill", GaugeFill(), Vector4.zero);
        Write("Tutorial/Guide_MouseMove", MouseGuide(0), Vector4.zero);
        Write("Tutorial/Guide_LeftClick", MouseGuide(1), Vector4.zero);
        Write("Tutorial/Guide_RightClick", MouseGuide(2), Vector4.zero);
        Write("Tutorial/Guide_Wheel", MouseGuide(3), Vector4.zero);
        AssetDatabase.Refresh();
        ApplyImportSettings();
    }

    static Color32 C(byte r, byte g, byte b, byte a=255) => new Color32(r,g,b,a);

    sealed class Surface
    {
        public readonly int Width, Height;
        readonly Color32[] pixels;
        public Surface(int w,int h)
        {
            Width=w; Height=h;
            pixels=new Color32[w*h];
            for(int i=0;i<pixels.Length;i++) pixels[i]=Clear;
        }
        public void Pixel(int x,int y,Color32 c)
        {
            if(x<0||x>=Width||y<0||y>=Height) return;
            pixels[y*Width+x]=c;
        }
        public void Rect(int x0,int y0,int x1,int y1,Color32 c)
        {
            for(int y=Mathf.Max(0,y0);y<Mathf.Min(Height,y1);y++)
            for(int x=Mathf.Max(0,x0);x<Mathf.Min(Width,x1);x++)
                Pixel(x,y,c);
        }
        public void Round(int x0,int y0,int x1,int y1,int radius,Color32 c)
        {
            float r=Mathf.Max(1,radius);
            for(int y=Mathf.Max(0,y0);y<Mathf.Min(Height,y1);y++)
            for(int x=Mathf.Max(0,x0);x<Mathf.Min(Width,x1);x++)
            {
                float cx=Mathf.Clamp(x+.5f,x0+r,x1-r);
                float cy=Mathf.Clamp(y+.5f,y0+r,y1-r);
                float dx=(x+.5f)-cx,dy=(y+.5f)-cy;
                if(dx*dx+dy*dy<=r*r) Pixel(x,y,c);
            }
        }
        public void Disc(int cx,int cy,int r,Color32 c)
        {
            for(int y=cy-r;y<=cy+r;y++)
            for(int x=cx-r;x<=cx+r;x++)
                if((x-cx)*(x-cx)+(y-cy)*(y-cy)<=r*r) Pixel(x,y,c);
        }
        public void Line(int x0,int y0,int x1,int y1,int thickness,Color32 c)
        {
            int steps=Mathf.Max(Mathf.Abs(x1-x0),Mathf.Abs(y1-y0));
            if(steps==0){Disc(x0,y0,thickness/2,c);return;}
            for(int i=0;i<=steps;i++)
            {
                float t=(float)i/steps;
                Disc(Mathf.RoundToInt(Mathf.Lerp(x0,x1,t)),
                     Mathf.RoundToInt(Mathf.Lerp(y0,y1,t)),thickness/2,c);
            }
        }
        public Texture2D Make()
        {
            var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            texture.SetPixels32(pixels);
            texture.Apply(false,false);
            return texture;
        }
    }

    static Texture2D Panel()
    {
        var s=new Surface(640,256);
        s.Round(4,7,636,251,26,Ink);
        s.Round(8,12,632,247,23,Mid);
        s.Round(11,16,629,241,20,Cream);
        s.Round(11,189,629,241,20,Lilac);
        s.Rect(11,191,629,218,Lilac);
        s.Rect(58,225,582,228,Mid);
        s.Line(579,226,610,206,4,Ink);
        return s.Make();
    }
    static Texture2D Button()
    {
        var s=new Surface(320,96);
        s.Round(2,2,318,96,19,Ink);
        s.Round(6,6,314,91,15,Shadow);
        s.Round(7,12,313,90,14,Mid);
        s.Round(8,16,312,88,13,Lilac);
        s.Round(8,20,312,88,13,Cream);
        s.Rect(18,27,302,75,Cream);
        s.Rect(22,15,298,20,Lilac);
        s.Line(288,22,300,31,3,Mid);
        return s.Make();
    }
    static Texture2D GaugeFrame()
    {
        var s=new Surface(768,88);
        s.Round(3,6,765,82,27,Ink);
        s.Round(7,10,761,78,23,Mid);
        s.Round(10,12,758,73,21,Lilac);
        s.Round(15,20,753,68,19,Shadow);
        s.Round(20,25,748,64,15,Clear);
        s.Line(707,13,741,23,3,Cream);
        return s.Make();
    }
    static Texture2D GaugeTrack()
    {
        var s=new Surface(724,44);
        s.Round(0,0,724,44,16,Ink);
        s.Round(4,4,720,40,13,Shadow);
        s.Round(8,8,716,36,11,C(50,35,68));
        return s.Make();
    }
    static Texture2D GaugeFill()
    {
        var s=new Surface(700,30);
        s.Rect(0,0,700,30,Mid);
        s.Rect(0,9,700,30,Lilac);
        s.Rect(0,24,700,30,Cream);
        return s.Make();
    }

    // Four stages share exactly the same mouse dimensions and viewpoint.
    static Texture2D MouseGuide(int mode)
    {
        var s=new Surface(256,256);
        // Small directional marks, deliberately separate from the mouse body.
        if(mode==0)
        {
            s.Line(23,126,74,126,7,Ink);
            s.Line(24,126,39,139,7,Ink); s.Line(24,126,39,113,7,Ink);
            s.Line(182,126,232,126,7,Ink);
            s.Line(232,126,217,139,7,Ink); s.Line(232,126,217,113,7,Ink);
        }
        if(mode==1)
        {
            s.Line(64,181,45,201,6,Ink);
            s.Line(67,194,57,217,5,Ink);
        }
        if(mode==2)
        {
            s.Line(191,181,210,201,6,Ink);
            s.Line(188,194,198,217,5,Ink);
        }
        if(mode==3)
        {
            s.Line(205,88,205,115,6,Ink);
            s.Line(205,115,194,105,6,Ink);s.Line(205,115,216,105,6,Ink);
            s.Line(205,175,205,146,6,Ink);
            s.Line(205,146,194,156,6,Ink);s.Line(205,146,216,156,6,Ink);
        }

        s.Round(77,22,179,230,48,Ink);
        s.Round(83,28,173,224,42,Lilac);
        s.Round(84,69,172,219,38,Cream);
        s.Round(84,29,172,125,39,Cream);
        // Button highlight is contained inside the same silhouette.
        if(mode==1) s.Round(84,30,128,125,17,Mid);
        if(mode==2) s.Round(128,30,172,125,17,Mid);
        s.Rect(123,30,132,125,Ink);
        s.Rect(84,119,173,125,Ink);
        s.Round(117,54,139,98,11,Ink);
        s.Round(122,59,134,93,6,mode==3?Mid:Lilac);
        return s.Make();
    }

    static void Write(string name,Texture2D texture,Vector4 border)
    {
        string path=Root+name+".png";
        if(File.Exists(path)){UnityEngine.Object.DestroyImmediate(texture);return;}
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path,texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        Debug.Log("[LuluArt] Created "+path);
    }

    static void ApplyImportSettings()
    {
        Set("UI/UI_Panel",new Vector4(60,42,60,42));
        Set("UI/UI_Button",new Vector4(44,25,44,25));
        Set("UI/Stability_Frame",new Vector4(56,30,56,30));
        Set("UI/Stability_Track",new Vector4(24,14,24,14));
        Set("UI/Stability_Fill",Vector4.zero);
        Set("Tutorial/Guide_MouseMove",Vector4.zero);
        Set("Tutorial/Guide_LeftClick",Vector4.zero);
        Set("Tutorial/Guide_RightClick",Vector4.zero);
        Set("Tutorial/Guide_Wheel",Vector4.zero);
    }
    static void Set(string name,Vector4 border)
    {
        string path=Root+name+".png";
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null) return;
        bool changed=importer.textureType!=TextureImporterType.Sprite
            || importer.spriteImportMode!=SpriteImportMode.Single
            || importer.mipmapEnabled || !importer.alphaIsTransparency
            || importer.spriteBorder!=border
            || importer.textureCompression!=TextureImporterCompression.Uncompressed;
        if(!changed) return;
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;
        importer.spriteBorder=border;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
#endif
