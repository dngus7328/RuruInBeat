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
        Write("Tutorial/Guide_Dodge", MouseGuide(4), Vector4.zero);
        Write("Tutorial/Guide_Mix", MouseGuide(5), Vector4.zero);
        // The chart catalog has six distinct jackets. Existing assigned
        // sprites are preserved; missing references are filled after import.
        string[] jackets = { "Jacket_BOOTBEAT", "Jacket_OddbeatShuffle",
            "Jacket_FullBandHit", "Jacket_SHUNSOU",
            "Jacket_CottonPulse", "Jacket_PopnDash" };
        for (int i=0; i<jackets.Length; i++)
            if (!File.Exists(Root+"Jackets/"+jackets[i]+".png"))
                Write("Jackets/"+jackets[i], Jacket(i), Vector4.zero);
        string[] backgrounds = { "BG_BOOTBEAT", "BG_OddbeatShuffle",
            "BG_FullBandHit", "BG_SHUNSOU", "BG_CottonPulse" };
        for (int i=0; i<backgrounds.Length; i++)
            if (!File.Exists(Root+"Backgrounds/"+backgrounds[i]+".png"))
                Write("Backgrounds/"+backgrounds[i], Background(i), Vector4.zero);
        AssetDatabase.Refresh();
        ApplyImportSettings();
        for(int i=0;i<jackets.Length;i++) Set("Jackets/"+jackets[i],Vector4.zero);
        for(int i=0;i<backgrounds.Length;i++) Set("Backgrounds/"+backgrounds[i],Vector4.zero);
        CopyExistingPinoResultArt();
        LinkCharts();
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
            pixels[(Height-1-y)*Width+x]=c;
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
        public void Polygon(Vector2[] v, Color32 c)
        {
            int x0=Width,y0=Height,x1=0,y1=0;
            foreach(var p in v)
            {
                x0=Mathf.Min(x0,Mathf.FloorToInt(p.x));
                y0=Mathf.Min(y0,Mathf.FloorToInt(p.y));
                x1=Mathf.Max(x1,Mathf.CeilToInt(p.x));
                y1=Mathf.Max(y1,Mathf.CeilToInt(p.y));
            }
            for(int y=Mathf.Max(0,y0);y<Mathf.Min(Height,y1);y++)
            for(int x=Mathf.Max(0,x0);x<Mathf.Min(Width,x1);x++)
            {
                bool inside=false;
                for(int i=0,j=v.Length-1;i<v.Length;j=i++)
                {
                    float yi=v[i].y,yj=v[j].y;
                    if((yi>y+.5f)!=(yj>y+.5f) &&
                        x+.5f < (v[j].x-v[i].x)*(y+.5f-yi)/(yj-yi)+v[i].x)
                        inside=!inside;
                }
                if(inside) Pixel(x,y,c);
            }
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
        s.Line(596,226,615,208,4,Ink);
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
        // Dodge lesson: incoming coral projectiles and an escape arrow.
        // The mouse is intentionally a mouse, not a redrawn Pino.
        if(mode==4)
        {
            s.Disc(43,57,27,Ink);s.Disc(43,57,21,Pink);
            s.Disc(213,195,23,Ink);s.Disc(213,195,17,Pink);
            s.Line(13,174,57,174,7,Mid);
            s.Line(15,174,28,162,6,Mid);s.Line(15,174,28,186,6,Mid);
            s.Line(201,60,239,60,7,Mid);
            s.Line(239,60,226,48,6,Mid);s.Line(239,60,226,72,6,Mid);
        }
        // Final lesson: click-and-dodge motif, not obsolete 4 lanes.
        if(mode==5)
        {
            s.Disc(40,67,25,Ink);s.Disc(40,67,19,Cream);
            s.Disc(217,67,25,Ink);s.Disc(217,67,19,Lilac);
            s.Disc(215,190,23,Ink);s.Disc(215,190,17,Pink);
            s.Line(17,169,58,169,6,Mid);
            s.Line(17,169,31,156,5,Mid);s.Line(17,169,31,182,5,Mid);
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


    // The jackets deliberately have different compositions. These are flat
    // graphic illustrations, not baked-in song labels or fake UI.
    static Texture2D Jacket(int index)
    {
        var s = new Surface(1024, 1024);
        switch(index)
        {
            case 0:
                s.Rect(0,0,1024,1024,Ink);
                s.Rect(0,0,1024,100,Lilac);
                s.Round(160,170,860,850,90,Shadow);
                s.Round(185,195,835,825,68,Cream);
                s.Round(248,264,775,760,48,Ink);
                s.Round(273,288,750,735,38,Lilac);
                for(int j=0;j<3;j++)
                    s.Round(320+j*120,480,390+j*120,540,14,j==1?Pink:Shadow);
                s.Rect(280,620,716,645,Shadow);
                break;
            case 1:
                s.Rect(0,0,1024,1024,C(223,206,245));
                s.Polygon(new[]{new Vector2(0,0),new Vector2(280,0),
                    new Vector2(0,490)},Mid);
                s.Polygon(new[]{new Vector2(860,290),new Vector2(1024,130),
                    new Vector2(1024,1024),new Vector2(650,1024)},Shadow);
                for(int j=0;j<5;j++)
                {
                    int xx=185+j*125, yy=180+j%2*150+j*80;
                    s.Polygon(new[]{new Vector2(xx,yy),new Vector2(xx+165,yy+35),
                        new Vector2(xx+135,yy+220),new Vector2(xx-30,yy+180)},Ink);
                    s.Polygon(new[]{new Vector2(xx+13,yy+17),new Vector2(xx+145,yy+44),
                        new Vector2(xx+122,yy+200),new Vector2(xx-13,yy+168)},
                        j==2?Pink:Cream);
                }
                break;
            case 2:
                s.Rect(0,0,1024,1024,C(43,30,62));
                s.Disc(505,530,347,Lilac);
                s.Disc(505,530,313,Ink);
                s.Disc(505,530,260,C(94,71,121));
                s.Disc(505,530,216,Ink);
                s.Disc(505,530,150,Cream);
                s.Polygon(new[]{new Vector2(0,238),new Vector2(1024,614),
                    new Vector2(1024,697),new Vector2(0,320)},Lilac);
                s.Polygon(new[]{new Vector2(0,780),new Vector2(1024,346),
                    new Vector2(1024,396),new Vector2(0,830)},Cream);
                s.Rect(180,130,265,202,Pink);
                s.Rect(756,815,832,894,Pink);
                break;
            case 3:
                s.Rect(0,0,1024,1024,Cream);
                s.Polygon(new[]{new Vector2(-120,80),new Vector2(145,-40),
                    new Vector2(1080,810),new Vector2(1010,1015)},Ink);
                s.Polygon(new[]{new Vector2(-40,250),new Vector2(60,140),
                    new Vector2(1055,980),new Vector2(924,1060)},Lilac);
                s.Polygon(new[]{new Vector2(-130,620),new Vector2(-80,530),
                    new Vector2(750,1120),new Vector2(865,1050)},Mid);
                s.Polygon(new[]{new Vector2(312,550),new Vector2(550,310),
                    new Vector2(790,550),new Vector2(550,780)},Ink);
                s.Polygon(new[]{new Vector2(345,550),new Vector2(550,345),
                    new Vector2(755,550),new Vector2(550,744)},Cream);
                s.Disc(550,550,78,Pink);
                break;
            case 4:
                s.Rect(0,0,1024,1024,C(239,230,247));
                s.Disc(350,535,265,Ink);s.Disc(350,540,246,Cream);
                s.Disc(630,540,230,Ink);s.Disc(630,545,210,Lilac);
                s.Disc(505,675,255,Ink);s.Disc(505,670,230,Cream);
                s.Disc(270,360,142,Ink);s.Disc(270,367,130,Lilac);
                s.Disc(718,357,156,Ink);s.Disc(718,362,142,Cream);
                s.Round(452,435,572,588,38,Ink);
                s.Round(470,458,554,567,26,Mint);
                break;
            default:
                s.Rect(0,0,1024,1024,C(240,179,208));
                s.Polygon(new[]{new Vector2(0,80),new Vector2(610,80),
                    new Vector2(1024,415),new Vector2(1024,520)},Cream);
                s.Round(260,285,759,732,160,Ink);
                s.Round(287,309,734,710,144,Cream);
                s.Rect(345,415,421,493,Ink);
                s.Rect(590,415,665,493,Ink);
                s.Round(440,585,577,615,16,Mid);
                s.Line(225,693,100,755,33,Ink);
                s.Line(760,693,892,743,33,Ink);
                s.Line(376,748,337,857,36,Ink);
                s.Line(637,748,688,856,36,Ink);
                s.Disc(149,275,63,Mint);
                break;
        }
        return s.Make();
    }

    static Texture2D Background(int index)
    {
        var s=new Surface(1920,1080);
        Color32[] baseColors={
            C(45,33,60),C(58,40,68),C(37,31,51),C(49,35,65),C(76,63,88)};
        Color32[] accent={
            C(78,57,94),C(100,72,107),C(75,62,91),C(91,69,112),C(114,98,132)};
        s.Rect(0,0,1920,1080,baseColors[index]);
        // Keep 60% center free of bright/high-contrast note-like objects.
        if(index==0)
        {
            s.Round(95,160,335,410,33,accent[index]);
            s.Round(80,555,375,760,38,C(65,49,82));
            s.Round(1600,210,1820,510,32,accent[index]);
            s.Round(1590,690,1780,875,32,C(65,49,82));
        }
        else if(index==1)
        {
            s.Polygon(new[]{new Vector2(0,180),new Vector2(300,90),
                new Vector2(415,490),new Vector2(0,600)},accent[index]);
            s.Polygon(new[]{new Vector2(1920,210),new Vector2(1620,280),
                new Vector2(1535,700),new Vector2(1920,620)},accent[index]);
            s.Polygon(new[]{new Vector2(0,890),new Vector2(340,820),
                new Vector2(510,1080),new Vector2(0,1080)},C(77,53,85));
        }
        else if(index==2)
        {
            for(int i=0;i<3;i++)
            {
                s.Round(20+i*43,120+i*140,350+i*24,225+i*140,24,accent[index]);
                s.Round(1570-i*24,200+i*140,1895-i*43,305+i*140,24,
                    C(63,54,81));
            }
        }
        else if(index==3)
        {
            s.Polygon(new[]{new Vector2(0,100),new Vector2(210,0),
                new Vector2(510,320),new Vector2(0,505)},accent[index]);
            s.Polygon(new[]{new Vector2(1920,0),new Vector2(1590,0),
                new Vector2(1490,340),new Vector2(1920,720)},accent[index]);
            s.Polygon(new[]{new Vector2(0,790),new Vector2(305,575),
                new Vector2(445,1080),new Vector2(0,1080)},C(64,45,84));
        }
        else
        {
            s.Disc(105,270,255,accent[index]);
            s.Disc(320,950,310,C(95,80,112));
            s.Disc(1860,280,350,accent[index]);
            s.Disc(1680,935,290,C(95,80,112));
        }
        return s.Make();
    }

    static void CopyExistingPinoResultArt()
    {
        string sourceDir="Assets/Image/";
        string targetDir=Root+"Characters/";
        Directory.CreateDirectory(targetDir);
        foreach(string name in new[]{ "피노_Clear", "피노_False" })
        {
            string source=sourceDir+name+".png";
            string target=targetDir+name+".png";
            if(!File.Exists(source) || File.Exists(target)) continue;
            if(AssetDatabase.CopyAsset(source,target))
                Debug.Log("[LuluArt] Reused original "+source);
        }
    }

    static void LinkCharts()
    {
        string[] titles={ "Jacket_BOOTBEAT", "Jacket_OddbeatShuffle",
            "Jacket_FullBandHit", "Jacket_SHUNSOU",
            "Jacket_CottonPulse", "Jacket_PopnDash" };
        string[] bgs={ "BG_BOOTBEAT", "BG_OddbeatShuffle",
            "BG_FullBandHit", "BG_SHUNSOU", "BG_CottonPulse" };
        int changed=0;
        foreach(string guid in AssetDatabase.FindAssets("t:MouseModeChartAsset"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var chart=AssetDatabase.LoadAssetAtPath<
                LuluInBeat.MouseMode.ChartSystem.MouseModeChartAsset>(path);
            if(chart==null) continue;
            string key=(path+" "+chart.displayTitle).ToLowerInvariant();
            int idx=-1;
            if(key.Contains("bootbeat")) idx=0;
            else if(key.Contains("arcadegroove")||key.Contains("oddbeat")) idx=1;
            else if(key.Contains("fullbandhit")) idx=2;
            else if(key.Contains("mabAtaki".ToLowerInvariant())||
                    key.Contains("shunsou")||key.Contains("瞬奏")) idx=3;
            else if(key.Contains("rhythmgametheme")||key.Contains("cottonpulse")) idx=4;
            else if(key.Contains("popn_dash")||key.Contains("pop'n dash")||
                    key.Contains("popndash")) idx=5;
            if(idx<0) continue;
            bool dirty=false;
            if(chart.songSelectJacket==null)
            {
                Sprite jacket=AssetDatabase.LoadAssetAtPath<Sprite>(
                    Root+"Jackets/"+titles[idx]+".png");
                if(jacket!=null){chart.songSelectJacket=jacket;dirty=true;}
            }
            if(idx<5 && chart.backgroundImage==null)
            {
                Sprite background=AssetDatabase.LoadAssetAtPath<Sprite>(
                    Root+"Backgrounds/"+bgs[idx]+".png");
                if(background!=null){chart.backgroundImage=background;dirty=true;}
            }
            if(dirty){EditorUtility.SetDirty(chart);changed++;}
        }
        if(changed>0) AssetDatabase.SaveAssets();
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
        Set("Tutorial/Guide_Dodge",Vector4.zero);
        Set("Tutorial/Guide_Mix",Vector4.zero);
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
