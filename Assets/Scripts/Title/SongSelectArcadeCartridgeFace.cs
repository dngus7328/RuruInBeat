using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Matte, cut-corner game cartridge geometry. The original Image, Button and
/// Inspector color bindings stay intact, but the visible face is no longer a
/// generic rectangular bar. No textures, assets or glow required.
/// </summary>
[AddComponentMenu("Lulu In Beat/Song Select/Arcade Cartridge Face")]
public sealed class SongSelectArcadeCartridgeFace : Image
{
    [SerializeField] private int shapeStyle; // 0 = song spine, 1 = large package

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        Color baseColor = color;
        if (shapeStyle == 1)
        {
            // Asymmetrical 2D game package with a visible clipped corner,
            // rigid edge and printed-label panel.
            Poly(vh, r, Shade(baseColor, .44f),
                V(.00f,.09f), V(.09f,.00f), V(.91f,.00f),
                V(1f,.11f), V(1f,.88f), V(.90f,1f),
                V(.13f,1f), V(.00f,.89f));
            Poly(vh, r, Shade(baseColor, .96f),
                V(.018f,.12f), V(.105f,.027f), V(.90f,.027f),
                V(.975f,.13f), V(.975f,.86f), V(.875f,.973f),
                V(.15f,.973f), V(.018f,.865f));
            Quad(vh, r, .065f,.075f,.11f,.92f,Shade(baseColor,.63f));
            Quad(vh, r, .13f,.895f,.87f,.928f,Shade(baseColor,1.23f));
            Quad(vh, r, .13f,.85f,.87f,.876f,Shade(baseColor,.65f));
            Quad(vh, r, .13f,.13f,.87f,.18f,Shade(baseColor,.50f));
            for (int i=0;i<5;i++)
                Quad(vh,r,.74f+i*.026f,.087f,.752f+i*.026f,
                    .118f,Shade(baseColor,1.35f));
            return;
        }

        // A proper cartridge spine has cut corners, a printed paper label,
        // an exposed insertion edge and a raised lip. The very same Image
        // color tracks which cartridge is selected.
        Poly(vh,r,Shade(baseColor,.55f),
            V(.00f,.20f),V(.04f,.00f),V(.91f,.00f),
            V(1f,.17f),V(1f,.85f),V(.95f,1f),
            V(.06f,1f),V(.00f,.83f));
        Poly(vh,r,Shade(baseColor,1.04f),
            V(.012f,.23f),V(.05f,.07f),V(.895f,.07f),
            V(.978f,.21f),V(.978f,.83f),V(.938f,.948f),
            V(.072f,.948f),V(.012f,.815f));
        Quad(vh,r,.03f,.16f,.125f,.86f,Shade(baseColor,.65f));
        Quad(vh,r,.14f,.16f,.82f,.83f,Shade(baseColor,1.24f));
        Quad(vh,r,.14f,.79f,.82f,.825f,Shade(baseColor,1.62f));
        Quad(vh,r,.84f,.22f,.965f,.76f,Shade(baseColor,.70f));
        Quad(vh,r,.858f,.29f,.878f,.68f,Shade(baseColor,1.52f));
        Quad(vh,r,.89f,.29f,.91f,.68f,Shade(baseColor,1.52f));
        Quad(vh,r,.921f,.29f,.941f,.68f,Shade(baseColor,1.52f));
        Quad(vh,r,.14f,.12f,.80f,.145f,Shade(baseColor,.68f));
    }

    private static Vector2 V(float x,float y) {return new Vector2(x,y);}
    private static Color Shade(Color c,float amount)
    {
        return new Color(Mathf.Clamp01(c.r*amount),
            Mathf.Clamp01(c.g*amount),Mathf.Clamp01(c.b*amount),c.a);
    }
    private static void Quad(VertexHelper vh,Rect r,float x0,float y0,
        float x1,float y1,Color c)
    {
        Poly(vh,r,c,V(x0,y0),V(x1,y0),V(x1,y1),V(x0,y1));
    }
    private static void Poly(VertexHelper vh,Rect r,Color c,
        params Vector2[] points)
    {
        if(points == null || points.Length < 3)return;
        int first=vh.currentVertCount;
        UIVertex vertex=UIVertex.simpleVert;
        vertex.color=c;
        for(int i=0;i<points.Length;i++)
        {
            vertex.position=new Vector3(r.xMin+r.width*points[i].x,
                r.yMin+r.height*points[i].y,0f);
            // Sprite is deliberately absent: Unity's white UI texture.
            vertex.uv0=new Vector2(points[i].x,points[i].y);
            vh.AddVert(vertex);
        }
        for(int i=1;i<points.Length-1;i++)
            vh.AddTriangle(first,first+i,first+i+1);
    }
}
