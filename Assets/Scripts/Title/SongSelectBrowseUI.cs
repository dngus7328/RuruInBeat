using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Search, level filters, sorting and six-slot page navigation for the
/// single Unity song-select scene. Creates proper, working uGUI/TMP controls
/// inside the existing left-panel RectTransform in Play Mode. No art assets.
/// </summary>
[DisallowMultipleComponent]
public sealed class SongSelectBrowseUI : MonoBehaviour
{
    private SongSelectMenuUI owner;
    private TMP_FontAsset font;
    private TMP_InputField search;
    private TMP_Text searchPlaceholder;
    private TMP_Text sortCaption;
    private TMP_Text pageCaption;
    private TMP_Text emptyCaption;
    private Image previewFill;
    private Image previewTrack;
    private Button prevPage, nextPage;
    private Button[] filterButtons;
    private TMP_Text[] filterCaptions;
    private SongSelectLanguage language;

    private static readonly Color Field = new Color(.145f,.125f,.194f,1f);
    private static readonly Color ButtonFill = new Color(.20f,.177f,.252f,1f);
    private static readonly Color Selected = new Color(.53f,.37f,.53f,1f);
    private static readonly Color Ink = new Color(.96f,.91f,.94f,1f);
    private static readonly Color SoftInk = new Color(.77f,.70f,.80f,1f);

    public bool SearchFocused => search != null && search.isFocused;

    public void Initialize(SongSelectMenuUI menu,
        TMP_FontAsset typeface, SongSelectLanguage initialLanguage)
    {
        owner=menu;
        font=typeface != null ? typeface : TMP_Settings.defaultFontAsset;
        language=initialLanguage;

        RectTransform shelf = transform.Find("TrackListPanel") as RectTransform;
        if(shelf == null) return;
        AddShelfRails(shelf);
        CreatePreviewProgress();
        RectTransform bar=Root("BrowseBar",shelf,.015f,.755f,.985f,.992f);
        // A real TextMeshPro input field supports typing, Korean IME,
        // selection, paste and keyboard editing.
        search=SearchInput(bar,.030f,.565f,.756f,.945f);
        // Sorting is a small auxiliary control, not another primary action.
        Button sortButton=ButtonAt("SortButton",bar,
            .787f,.635f,.973f,.880f,out sortCaption,17);
        sortButton.onClick.AddListener(owner.CycleSortFromUI);

        filterButtons=new Button[4];
        filterCaptions=new TMP_Text[4];
        for(int i=0;i<4;i++)
        {
            int option=i;
            float l=.031f+i*.244f;
            // Thin filter tabs: enough room for two-digit level values,
            // noticeably less prominent than the song cartridges.
            filterButtons[i]=ButtonAt("LevelFilter_"+i,bar,
                l,.170f,l+.205f,.455f,out filterCaptions[i],16);
            filterButtons[i].onClick.AddListener(
                ()=>owner.SetLevelFilter(option));
        }

        // Page arrows are compact navigation controls below the six cases.
        RectTransform pager=Root("BrowsePager",shelf,
            .19f,.018f,.81f,.139f);
        prevPage=ButtonAt("PreviousPage",pager,
            .025f,.210f,.190f,.790f,out TMP_Text leftText,20);
        leftText.text="〈";
        nextPage=ButtonAt("NextPage",pager,
            .810f,.210f,.975f,.790f,out TMP_Text rightText,20);
        rightText.text="〉";
        pageCaption=Label("PageNumber",pager,.210f,.10f,.790f,.90f,18,
            Ink,TextAlignmentOptions.Center);
        prevPage.onClick.AddListener(()=>owner.ChangePage(-1));
        nextPage.onClick.AddListener(()=>owner.ChangePage(1));

        emptyCaption=Label("NoSearchResults",shelf,.12f,.40f,.88f,.56f,
            23,SoftInk,TextAlignmentOptions.Center);
        emptyCaption.raycastTarget=false;
        SetLanguage(initialLanguage);
        Refresh(0,0,0,1,0);
    }

    public void SetLanguage(SongSelectLanguage next)
    {
        language=next;
        if(searchPlaceholder != null)
            searchPlaceholder.text=Get("searchPlaceholder");
        if(filterCaptions != null)
        {
            string[] keys={"filterAll","filterLow","filterMid","filterHigh"};
            for(int i=0;i<filterCaptions.Length;i++)
                if(filterCaptions[i] != null)
                    filterCaptions[i].text=Get(keys[i]);
        }
        if(emptyCaption != null)
            emptyCaption.text=Get("noMatches");
        Refresh(owner != null ? owner.CurrentLevelFilter : 0,
            owner != null ? owner.CurrentSortIndex : 0,
            owner != null ? owner.CurrentPage : 0,
            owner != null ? owner.PageCount : 1,
            owner != null ? owner.MatchCount : 0);
    }

    public void Refresh(int filter,int sort,int page,int pageCount,
        int matches)
    {
        if(sortCaption != null)
        {
            string key=sort==1?"sortLevel":sort==2?"sortBpm":"sortTitle";
            sortCaption.text=Get("sort")+"  "+Get(key);
        }
        if(filterButtons != null)
            for(int i=0;i<filterButtons.Length;i++)
            {
                Image graphic=filterButtons[i].targetGraphic as Image;
                if(graphic != null)
                    graphic.color=i==filter?Selected:ButtonFill;
            }
        if(pageCaption != null)
            pageCaption.text=(page+1).ToString("00")+" / "+
                Mathf.Max(1,pageCount).ToString("00");
        bool manyPages=pageCount>1;
        if(prevPage != null)
        {
            prevPage.gameObject.SetActive(manyPages);
            prevPage.interactable=page>0;
        }
        if(nextPage != null)
        {
            nextPage.gameObject.SetActive(manyPages);
            nextPage.interactable=page<pageCount-1;
        }
        if(pageCaption != null)
            pageCaption.gameObject.SetActive(manyPages);
        if(emptyCaption != null)
            emptyCaption.gameObject.SetActive(matches==0);
    }

    private void Update()
    {
        if (previewFill == null) return;

        bool on = owner != null && owner.PreviewEnabled;
        previewFill.fillAmount = on ? owner.PreviewProgress : 0f;
        previewFill.color = on
            ? new Color(.83f,.66f,.76f,1f)
            : new Color(.46f,.39f,.51f,.28f);

        if (previewTrack != null)
            previewTrack.color = on
                ? new Color(.19f,.16f,.24f,1f)
                : new Color(.16f,.14f,.19f,.72f);
    }

    private void CreatePreviewProgress()
    {
        RectTransform detail = transform.Find("SelectedTrackPanel")
            as RectTransform;
        if (detail == null) return;
        RectTransform track=Root("AudioPreviewProgressTrack",
            detail,.089f,.083f,.671f,.104f);
        Image bg=track.gameObject.AddComponent<Image>();
        previewTrack=bg;
        bg.color=new Color(.19f,.16f,.24f,1f);
        bg.raycastTarget=false;

        RectTransform fill=Root("AudioPreviewProgressFill",
            track,0f,0f,1f,1f);
        previewFill=fill.gameObject.AddComponent<Image>();
        previewFill.color=new Color(.83f,.66f,.76f,1f);
        previewFill.type=Image.Type.Filled;
        previewFill.fillMethod=Image.FillMethod.Horizontal;
        previewFill.fillOrigin=0;
        previewFill.fillAmount=0f;
        previewFill.raycastTarget=false;
    }

    private void AddShelfRails(RectTransform shelf)
    {
        // Avoid heavy black boxes behind every cartridge. Thin shelves
        // indicate storage without adding another frame to each button.
        for (int i=0;i<6;i++)
        {
            int row=i/2, col=i%2;
            float y=.554f-row*.199f;
            float left=col==0?.043f:.512f;
            float right=col==0?.489f:.963f;
            RectTransform rail=Root("ShelfRail_"+(i+1),
                shelf,left,y-.008f,right,y);
            Image image=rail.gameObject.AddComponent<Image>();
            image.color=new Color(.33f,.26f,.36f,.55f);
            image.raycastTarget=false;
            rail.SetAsFirstSibling();
        }
    }

    private string Get(string key)
    {
        return SongSelectLocalization.Get(language,key);
    }

    private static RectTransform Root(string name,Transform parent,
        float left,float bottom,float right,float top)
    {
        GameObject go=new GameObject(name,typeof(RectTransform));
        RectTransform rt=go.GetComponent<RectTransform>();
        rt.SetParent(parent,false);
        Rect(rt,left,bottom,right,top);
        return rt;
    }

    private static void Rect(RectTransform r,float l,float b,float x,float t)
    {
        r.anchorMin=new Vector2(l,b);
        r.anchorMax=new Vector2(x,t);
        r.offsetMin=Vector2.zero;
        r.offsetMax=Vector2.zero;
        r.pivot=new Vector2(.5f,.5f);
        r.localScale=Vector3.one;
    }

    private TMP_Text Label(string name,Transform parent,float l,float b,
        float r,float t,int size,Color ink,TextAlignmentOptions alignment)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),
            typeof(CanvasRenderer),typeof(TextMeshProUGUI));
        RectTransform rt=go.GetComponent<RectTransform>();
        rt.SetParent(parent,false);
        Rect(rt,l,b,r,t);
        TextMeshProUGUI label=go.GetComponent<TextMeshProUGUI>();
        label.font=font;
        label.fontSize=size;
        label.enableAutoSizing=true;
        label.fontSizeMin=Mathf.Max(12,size*.62f);
        label.fontSizeMax=size;
        label.color=ink;
        label.alignment=alignment;
        label.raycastTarget=false;
        label.enableWordWrapping=false;
        label.overflowMode=TextOverflowModes.Ellipsis;
        return label;
    }

    private Button ButtonAt(string name,Transform parent,float l,float b,
        float r,float t,out TMP_Text caption,int fontSize)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),
            typeof(CanvasRenderer),typeof(Image),typeof(Button));
        RectTransform rt=go.GetComponent<RectTransform>();
        rt.SetParent(parent,false);
        Rect(rt,l,b,r,t);
        Image bg=go.GetComponent<Image>();
        bg.color=ButtonFill;
        Button button=go.GetComponent<Button>();
        button.targetGraphic=bg;
        ColorBlock colors=button.colors;
        colors.normalColor=Color.white;
        colors.highlightedColor=new Color(1.13f,1.09f,1.14f,1f);
        colors.pressedColor=new Color(.84f,.79f,.87f,1f);
        colors.disabledColor=new Color(.63f,.60f,.69f,.4f);
        button.colors=colors;
        caption=Label("Caption",rt,.03f,.05f,.97f,.95f,
            fontSize,Ink,TextAlignmentOptions.Center);
        return button;
    }

    private TMP_InputField SearchInput(Transform parent,
        float l,float b,float r,float t)
    {
        GameObject go=new GameObject("SearchBySongTitle",
            typeof(RectTransform),typeof(CanvasRenderer),
            typeof(Image),typeof(TMP_InputField));
        RectTransform rect=go.GetComponent<RectTransform>();
        rect.SetParent(parent,false);
        Rect(rect,l,b,r,t);
        Image bg=go.GetComponent<Image>();
        bg.color=Field;
        bg.raycastTarget=true;

        RectTransform viewport=Root("TextArea",rect,.045f,.06f,.955f,.94f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text typed=Label("TypedText",viewport,0f,0f,1f,1f,19,
            Ink,TextAlignmentOptions.MidlineLeft);
        TMP_Text placeholder=Label("Placeholder",viewport,0f,0f,1f,1f,
            19,SoftInk,TextAlignmentOptions.MidlineLeft);
        searchPlaceholder=placeholder;
        TMP_InputField field=go.GetComponent<TMP_InputField>();
        field.targetGraphic=bg;
        field.textViewport=viewport;
        field.textComponent=(TextMeshProUGUI)typed;
        field.placeholder=(TextMeshProUGUI)placeholder;
        field.lineType=TMP_InputField.LineType.SingleLine;
        field.characterLimit=80;
        field.caretColor=Ink;
        field.customCaretColor=true;
        field.onValueChanged.AddListener(owner.SetSearchQuery);
        return field;
    }
}
