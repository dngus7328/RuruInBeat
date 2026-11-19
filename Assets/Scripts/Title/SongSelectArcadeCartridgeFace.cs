using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple matte cartridge geometry: no 3-slot decoration or triangular pins.
/// The level is displayed separately by an editable TMPro label.
/// </summary>
[AddComponentMenu("Lulu In Beat/Song Select/Arcade Cartridge Face")]
public sealed class SongSelectArcadeCartridgeFace : Image
{
    [SerializeField] private int shapeStyle; // 0 shelf, 1 package

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;
        Color c = color;
        if (shapeStyle == 1)
        {
            Poly(vh,r,Tone(c,.80f),V(0,.06f),V(.05f,0),V(.95f,0),
                V(1,.06f),V(1,.94f),V(.95f,1),V(.06f,1),V(0,.94f));
            Poly(vh,r,Tone(c,1.02f),V(.018f,.08f),V(.06f,.024f),
                V(.94f,.024f),V(.98f,.08f),V(.98f,.92f),
                V(.935f,.976f),V(.07f,.976f),V(.018f,.92f));
            Quad(vh,r,.07f,.065f,.103f,.935f,Tone(c,.75f));
            Quad(vh,r,.13f,.89f,.89f,.91f,Tone(c,1.15f));
            return;
        }

        Poly(vh,r,Tone(c,.8f),V(0,.18f),V(.023f,.07f),
            V(.95f,.07f),V(.987f,.15f),V(1,.25f),V(1,.76f),
            V(.975f,.86f),V(.95f,.93f),V(.023f,.93f),V(0,.82f));
        Poly(vh,r,Tone(c,1.05f),V(.012f,.19f),V(.034f,.11f),
            V(.943f,.11f),V(.975f,.19f),V(.975f,.81f),
            V(.943f,.89f),V(.034f,.89f),V(.012f,.81f));
        Quad(vh,r,.03f,.18f,.088f,.82f,Tone(c,.73f));
        Quad(vh,r,.105f,.17f,.746f,.83f,Tone(c,1.18f));
        Quad(vh,r,.105f,.783f,.746f,.805f,Tone(c,1.32f));
        // The final section is visually calm, reserved for "레벨 09".
        Quad(vh,r,.755f,.17f,.966f,.83f,Tone(c,.77f));
    }

    private static Vector2 V(float x,float y) {return new Vector2(x,y);}
    private static Color Tone(Color c,float k)
    {
        return new Color(Mathf.Clamp01(c.r*k),
            Mathf.Clamp01(c.g*k),Mathf.Clamp01(c.b*k),c.a);
    }
    private static void Quad(VertexHelper h,Rect r,float x0,float y0,
        float x1,float y1,Color c)
    {
        Poly(h,r,c,V(x0,y0),V(x1,y0),V(x1,y1),V(x0,y1));
    }
    private static void Poly(VertexHelper h,Rect r,Color c,
        params Vector2[] points)
    {
        if(points.Length<3)return;
        int first=h.currentVertCount;
        UIVertex v=UIVertex.simpleVert;
        v.color=c;
        for(int i=0;i<points.Length;i++)
        {
            v.position=new Vector3(r.xMin+r.width*points[i].x,
                r.yMin+r.height*points[i].y,0f);
            v.uv0=points[i];
            h.AddVert(v);
        }
        for(int i=1;i<points.Length-1;i++)
            h.AddTriangle(first,first+i,first+i+1);
    }
}
