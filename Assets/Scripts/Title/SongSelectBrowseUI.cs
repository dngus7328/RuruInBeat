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
    private Button filterToggle;
    private TMP_Text filterToggleCaption;
    private RectTransform filterPanel;
    private SongSelectRangeBar levelRange;
    private SongSelectRangeBar bpmRange;
    private TMP_Text filterTitle;
    private TMP_Text levelHeading, bpmHeading;
    private TMP_Text levelValues, bpmValues;
    private TMP_Text resetCaption, doneCaption, matchesCaption;
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
        search=SearchInput(bar,.030f,.565f,.687f,.945f);

        // Small secondary actions. Filters are grouped into one popover
        // instead of permanently covering the cartridge shelf.
        filterToggle=ButtonAt("FilterToggle",bar,
            .708f,.615f,.833f,.887f,out filterToggleCaption,16);
        filterToggle.onClick.AddListener(ToggleFilterPanel);
        Button sortButton=ButtonAt("SortButton",bar,
            .851f,.615f,.974f,.887f,out sortCaption,16);
        sortButton.onClick.AddListener(owner.CycleSortFromUI);

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
        BuildFilterPanel(shelf);
        SetLanguage(initialLanguage);
        Refresh(1,15,60,240,60,240,0,0,1,0);
    }

    public void SetLanguage(SongSelectLanguage next)
    {
        language=next;
        if(searchPlaceholder != null)
            searchPlaceholder.text=Get("searchPlaceholder");
        if(filterTitle != null)
            filterTitle.text=Get("filter");
        if(levelHeading != null)
            levelHeading.text=Get("filterLevelHeading");
        if(bpmHeading != null)
            bpmHeading.text=Get("filterBpmHeading");
        if(resetCaption != null)
            resetCaption.text=Get("filterReset");
        if(doneCaption != null)
            doneCaption.text=Get("filterDone");
        if(emptyCaption != null)
            emptyCaption.text=Get("noMatches");

        Refresh(owner != null ? owner.LevelMin : 1,
            owner != null ? owner.LevelMax : 15,
            owner != null ? owner.BpmMin : 60,
            owner != null ? owner.BpmMax : 240,
            owner != null ? owner.BpmBoundMin : 60,
            owner != null ? owner.BpmBoundMax : 240,
            owner != null ? owner.CurrentSortIndex : 0,
            owner != null ? owner.CurrentPage : 0,
            owner != null ? owner.PageCount : 1,
            owner != null ? owner.MatchCount : 0);
    }

    public void Refresh(int levelMin,int levelMax,int bpmMin,int bpmMax,
        int bpmBoundMin,int bpmBoundMax,int sort,int page,
        int pageCount,int matches)
    {
        if(sortCaption != null)
        {
            string key=sort==1?"sortLevel":sort==2?"sortBpm":"sortTitle";
            sortCaption.text=Get("sort")+" "+Get(key);
        }

        bool levelActive=levelMin>1||levelMax<15;
        bool bpmActive=bpmMin>bpmBoundMin||bpmMax<bpmBoundMax;
        if(filterToggleCaption != null)
            filterToggleCaption.text=Get("filter")+
                (levelActive||bpmActive?" •":"");
        if(filterToggle != null)
        {
            Image background=filterToggle.targetGraphic as Image;
            if(background != null)
                background.color=levelActive||bpmActive
                    ? Selected : ButtonFill;
        }

        if(levelRange != null)
            levelRange.Configure(1,15,levelMin,levelMax);
        if(bpmRange != null)
            bpmRange.Configure(bpmBoundMin,bpmBoundMax,bpmMin,bpmMax);
        if(levelValues != null)
            levelValues.text=levelMin.ToString("00")+" — "+
                levelMax.ToString("00");
        if(bpmValues != null)
            bpmValues.text=bpmMin.ToString()+" — "+bpmMax.ToString();
        if(matchesCaption != null)
            matchesCaption.text=Get("filterMatches")+"  "+matches;

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

    // The menu intentionally hit-tests cartridges directly. Never allow
    // a click on the filter panel to pass through to a cartridge below.
    public bool BlocksCartridgeClick(Vector2 screenPoint)
    {
        return filterPanel != null && filterPanel.gameObject.activeSelf &&
            RectTransformUtility.RectangleContainsScreenPoint(
                filterPanel,screenPoint,null);
    }

    private void ToggleFilterPanel()
    {
        if(filterPanel == null)return;
        bool open=!filterPanel.gameObject.activeSelf;
        filterPanel.gameObject.SetActive(open);
        if(open)filterPanel.SetAsLastSibling();
    }

    private void BuildFilterPanel(RectTransform shelf)
    {
        // A compact game selector, not a settings dialog with 8 buttons.
        filterPanel=Root("SongFilterPanel",shelf,
            .135f,.250f,.982f,.750f);
        Image background=filterPanel.gameObject.AddComponent<Image>();
        background.color=new Color(.112f,.095f,.147f,1f);
        background.raycastTarget=true;

        filterTitle=Label("FilterTitle",filterPanel,
            .040f,.817f,.42f,.950f,20,Ink,
            TextAlignmentOptions.MidlineLeft);
        matchesCaption=Label("ResultCount",filterPanel,
            .47f,.824f,.965f,.956f,15,SoftInk,
            TextAlignmentOptions.MidlineRight);

        levelHeading=Label("LevelHeading",filterPanel,
            .045f,.568f,.206f,.738f,17,Ink,
            TextAlignmentOptions.MidlineLeft);
        levelValues=Label("LevelValues",filterPanel,
            .653f,.568f,.952f,.738f,17,Ink,
            TextAlignmentOptions.MidlineRight);
        RectTransform levelAnchor=Root("LevelRange",filterPanel,
            .216f,.560f,.642f,.758f);
        levelRange=levelAnchor.gameObject.AddComponent<SongSelectRangeBar>();
        levelRange.color=Color.white;
        levelRange.Changed=(low,high)=>owner.SetLevelRange(low,high);

        bpmHeading=Label("BpmHeading",filterPanel,
            .045f,.321f,.206f,.485f,17,Ink,
            TextAlignmentOptions.MidlineLeft);
        bpmValues=Label("BpmValues",filterPanel,
            .653f,.321f,.952f,.485f,17,Ink,
            TextAlignmentOptions.MidlineRight);
        RectTransform bpmAnchor=Root("BpmRange",filterPanel,
            .216f,.303f,.642f,.514f);
        bpmRange=bpmAnchor.gameObject.AddComponent<SongSelectRangeBar>();
        bpmRange.color=Color.white;
        bpmRange.Changed=(low,high)=>owner.SetBpmRange(low,high);

        Button reset=ButtonAt("ResetFilter",filterPanel,
            .045f,.061f,.282f,.235f,out resetCaption,15);
        reset.onClick.AddListener(owner.ResetBrowseFilters);
        Button done=ButtonAt("DoneFilter",filterPanel,
            .746f,.061f,.952f,.235f,out doneCaption,15);
        done.onClick.AddListener(()=>filterPanel.gameObject.SetActive(false));
        filterPanel.SetAsLastSibling();
        filterPanel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if(filterPanel != null && filterPanel.gameObject.activeSelf &&
            Input.GetMouseButtonDown(0))
        {
            Vector2 pointer=Input.mousePosition;
            RectTransform button=filterToggle != null
                ? filterToggle.transform as RectTransform : null;
            bool overPanel=RectTransformUtility.RectangleContainsScreenPoint(
                filterPanel,pointer,null);
            bool overToggle=button != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    button,pointer,null);
            if(!overPanel && !overToggle)
                filterPanel.gameObject.SetActive(false);
        }

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
