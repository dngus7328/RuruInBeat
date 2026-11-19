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
            // One quiet cartridge frame: thin rim, no faux chips, stripes
            // or oversized bevels competing with the actual cover art.
            Poly(vh,r,Tone(c,.81f),
                V(0f,.035f),V(.035f,0f),V(.965f,0f),
                V(1f,.035f),V(1f,.965f),V(.965f,1f),
                V(.035f,1f),V(0f,.965f));
            Poly(vh,r,Tone(c,1.06f),
                V(.021f,.046f),V(.045f,.022f),V(.955f,.022f),
                V(.978f,.047f),V(.978f,.954f),V(.954f,.978f),
                V(.046f,.978f),V(.022f,.954f));
            return;
        }

        Poly(vh,r,Tone(c,.8f),V(0,.18f),V(.023f,.07f),
            V(.95f,.07f),V(.987f,.15f),V(1,.25f),V(1,.76f),
            V(.975f,.86f),V(.95f,.93f),V(.023f,.93f),V(0,.82f));
        Poly(vh,r,Tone(c,1.05f),V(.012f,.19f),V(.034f,.11f),
            V(.943f,.11f),V(.975f,.19f),V(.975f,.81f),
            V(.943f,.89f),V(.034f,.89f),V(.012f,.81f));
        // Shared centered structure for all cartridges:
        // full-width title label above, identical level badge below.
        // Neither the level text nor its badge is pushed to one side.
        Quad(vh,r,.03f,.18f,.088f,.82f,Tone(c,.73f));
        Quad(vh,r,.112f,.425f,.935f,.82f,Tone(c,1.18f));
        Quad(vh,r,.112f,.782f,.935f,.805f,Tone(c,1.32f));
        Quad(vh,r,.275f,.13f,.725f,.415f,Tone(c,.71f));
        Quad(vh,r,.283f,.385f,.717f,.407f,Tone(c,1.14f));
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
