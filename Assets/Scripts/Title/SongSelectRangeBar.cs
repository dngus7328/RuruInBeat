using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A matte, dual-handle game UI range selector. Both the lower and upper
/// value live on a single track; supports mouse dragging in the song browser.
/// </summary>
[AddComponentMenu("Lulu In Beat/Song Select/Range Filter")]
public sealed class SongSelectRangeBar : MaskableGraphic,
    IPointerDownHandler, IDragHandler
{
    private int lowest=1;
    private int highest=15;
    private int selectedLow=1;
    private int selectedHigh=15;
    private int activeHandle;
    private bool ready;

    public Action<int,int> Changed;

    private static readonly Color32 TrackColor =
        new Color32(57,48,72,255);
    private static readonly Color32 RangeColor =
        new Color32(159,113,154,255);
    private static readonly Color32 ThumbColor =
        new Color32(230,198,218,255);
    private static readonly Color32 ThumbEdge =
        new Color32(73,54,83,255);

    public void Configure(int boundLow,int boundHigh,int low,int high)
    {
        lowest=Mathf.Min(boundLow,boundHigh);
        highest=Mathf.Max(lowest+1,boundHigh);
        SetValues(low,high,false);
        raycastTarget=true;
        ready=true;
        SetVerticesDirty();
    }

    public void SetValues(int low,int high,bool notify)
    {
        int l=Mathf.Clamp(low,lowest,highest);
        int h=Mathf.Clamp(highest<l?l:high,l,highest);
        if(l==selectedLow && h==selectedHigh)
        {
            SetVerticesDirty();
            return;
        }
        selectedLow=l;
        selectedHigh=h;
        SetVerticesDirty();
        if(notify && ready)
            Changed?.Invoke(selectedLow,selectedHigh);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r=rectTransform.rect;
        if(r.width<=0f || r.height<=0f)return;
        float left=r.xMin+r.width*.065f;
        float right=r.xMax-r.width*.065f;
        float center=r.center.y;
        float thickness=Mathf.Max(3f,r.height*.085f);
        Quad(vh,left,center-thickness,right,center+thickness,
            TrackColor);

        float xMin=Mathf.Lerp(left,right,Normalize(selectedLow));
        float xMax=Mathf.Lerp(left,right,Normalize(selectedHigh));
        Quad(vh,xMin,center-thickness,xMax,center+thickness,
            RangeColor);

        float thumbWidth=Mathf.Clamp(r.height*.30f,9f,16f);
        float thumbHeight=Mathf.Clamp(r.height*.65f,16f,28f);
        Thumb(vh,xMin,center,thumbWidth,thumbHeight);
        Thumb(vh,xMax,center,thumbWidth,thumbHeight);
    }

    private static void Thumb(VertexHelper vh,float x,float y,
        float width,float height)
    {
        Quad(vh,x-width*.54f,y-height*.54f,
            x+width*.54f,y+height*.54f,ThumbEdge);
        Quad(vh,x-width*.42f,y-height*.42f,
            x+width*.42f,y+height*.42f,ThumbColor);
    }

    private static void Quad(VertexHelper vh,
        float x0,float y0,float x1,float y1,Color32 color)
    {
        int vertex=vh.currentVertCount;
        vh.AddVert(new Vector3(x0,y0,0f),color,Vector2.zero);
        vh.AddVert(new Vector3(x0,y1,0f),color,Vector2.zero);
        vh.AddVert(new Vector3(x1,y1,0f),color,Vector2.zero);
        vh.AddVert(new Vector3(x1,y0,0f),color,Vector2.zero);
        vh.AddTriangle(vertex,vertex+1,vertex+2);
        vh.AddTriangle(vertex,vertex+2,vertex+3);
    }

    private float Normalize(int value)
    {
        return (value-lowest)/(float)(highest-lowest);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if(!ready)return;
        float position;
        if(!PointerFraction(eventData,out position))return;

        float distanceFromMin=Mathf.Abs(position-Normalize(selectedLow));
        float distanceFromMax=Mathf.Abs(position-Normalize(selectedHigh));
        activeHandle=distanceFromMin<=distanceFromMax?0:1;
        if(selectedLow==selectedHigh)
            activeHandle=position<Normalize(selectedLow)?0:1;
        MoveSelectedHandle(position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(!ready)return;
        float position;
        if(PointerFraction(eventData,out position))
            MoveSelectedHandle(position);
    }

    private bool PointerFraction(PointerEventData eventData,
        out float fraction)
    {
        fraction=0f;
        Vector2 local;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,eventData.position,eventData.pressEventCamera,
            out local))return false;
        Rect r=rectTransform.rect;
        float left=r.xMin+r.width*.065f;
        float width=Mathf.Max(1f,r.width*.87f);
        fraction=Mathf.Clamp01((local.x-left)/width);
        return true;
    }

    private void MoveSelectedHandle(float fraction)
    {
        int value=Mathf.RoundToInt(Mathf.Lerp(lowest,highest,fraction));
        if(activeHandle==0)
            SetValues(Mathf.Min(value,selectedHigh),selectedHigh,true);
        else
            SetValues(selectedLow,Mathf.Max(value,selectedLow),true);
    }
}
