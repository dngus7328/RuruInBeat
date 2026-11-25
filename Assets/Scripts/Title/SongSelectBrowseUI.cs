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
    private Image searchBackground;
    private Outline searchFocusOutline;
    private TMP_Text sortCaption;
    private TMP_Text pageCaption;
    private TMP_Text emptyCaption;
    private Button prevPage, nextPage;
    private Button filterToggle;
    private TMP_Text filterToggleCaption;
    private RectTransform filterPanel;
    private CanvasGroup filterPanelGroup;
    private float filterEnter;
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
    private static readonly Color SearchFocusedFill =
        new Color(.18f, .15f, .235f, 1f);
    private static readonly Color SearchFocusedEdge =
        new Color(.78f, .64f, .83f, .9f);

    public bool SearchFocused => search != null && search.isFocused;

    private void SetSearchFocusAppearance(bool focused)
    {
        if (searchBackground != null)
            searchBackground.color = focused ? new Color(.94f,.90f,1f,1f) : Color.white;
        if (searchFocusOutline != null)
            searchFocusOutline.effectColor = focused
                ? SearchFocusedEdge : Color.clear;
    }

    public void Initialize(SongSelectMenuUI menu,
        TMP_FontAsset typeface, SongSelectLanguage initialLanguage)
    {
        owner=menu;
        font=RuruConsoleUI.Font();
        language=initialLanguage;

        RectTransform shelf = transform.Find("TrackListPanel") as RectTransform;
        if(shelf == null) return;
        if(shelf.Find("SongRow_01/BeatCartridgeArt")==null) AddShelfRails(shelf);
        RectTransform bar=Root("BrowseBar",shelf,.008f,.777f,.992f,.990f);
        Label("LibraryListTitle",bar,.015f,.57f,.97f,.98f,34,RuruConsoleUI.Ink,TextAlignmentOptions.MidlineLeft).text="TRACKS";
        // A real TextMeshPro input field supports typing, Korean IME,
        // selection, paste and keyboard editing.
        search=SearchInput(bar,.010f,.06f,.650f,.54f);

        // Small secondary actions. Filters are grouped into one popover
        // instead of permanently covering the cartridge shelf.
        filterToggle=ButtonAt("FilterToggle",bar,
            .665f,.06f,.820f,.54f,out filterToggleCaption,24);
        filterToggle.onClick.AddListener(ToggleFilterPanel);
        Button sortButton=ButtonAt("SortButton",bar,
            .832f,.06f,.990f,.54f,out sortCaption,24);
        sortButton.onClick.AddListener(owner.CycleSortFromUI);

        // Page arrows are compact navigation controls below the six cases.
        RectTransform pager=Root("BrowsePager",shelf,
            .688f,.012f,.968f,.088f);
        prevPage=ButtonAt("PreviousPage",pager,
            .015f,.13f,.220f,.87f,out TMP_Text leftText,18);
        leftText.text="〈";
        nextPage=ButtonAt("NextPage",pager,
            .780f,.13f,.985f,.87f,out TMP_Text rightText,18);
        rightText.text="〉";
        pageCaption=Label("PageNumber",pager,.245f,.10f,.755f,.90f,17,
            RuruConsoleUI.Ink,TextAlignmentOptions.Center);
        prevPage.onClick.AddListener(()=>owner.ChangePage(-1));
        nextPage.onClick.AddListener(()=>owner.ChangePage(1));

        emptyCaption=Label("NoSearchResults",shelf,.12f,.40f,.88f,.56f,
            23,RuruConsoleUI.Ink,TextAlignmentOptions.Center);
        emptyCaption.raycastTarget=false;
        BuildFilterPanel();
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
            sortCaption.text=Get(sort==1 ? "sortLevelShort" :
                sort==2 ? "sortBpmShort" : "sortTitleShort");
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
                background.sprite=levelActive||bpmActive ? RuruConsoleUI.MainKey : RuruConsoleUI.AltKey;
            if(background != null) background.color=Color.white;
            if(filterToggleCaption!=null) filterToggleCaption.color=levelActive||bpmActive ? RuruConsoleUI.Ink : RuruConsoleUI.Paper;
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
        // The panel is docked on the *right*, not floating above the
        // left-hand cartridges. Only clicks on it are consumed.
        return filterPanel != null && filterPanel.gameObject.activeSelf &&
            RectTransformUtility.RectangleContainsScreenPoint(
                filterPanel,screenPoint,null);
    }

    public bool TryCloseFilterPanel()
    {
        if (filterPanel == null || !filterPanel.gameObject.activeSelf)
            return false;
        filterPanel.gameObject.SetActive(false);
        return true;
    }

    private void ToggleFilterPanel()
    {
        if(filterPanel == null)return;
        bool open=!filterPanel.gameObject.activeSelf;
        filterPanel.gameObject.SetActive(open);
        if(open)
        {
            filterPanel.SetAsLastSibling();
            filterEnter=0f;
            if(filterPanelGroup!=null)filterPanelGroup.alpha=0f;
            filterPanel.anchoredPosition=new Vector2(22f,0f);
        }
    }

    private void BuildFilterPanel()
    {
        // A docked right-hand drawer temporarily replaces the selected
        // record sleeve. It never blocks the 2x3 cartridge browsing area.
        RectTransform detail = transform.Find("SelectedTrackPanel")
            as RectTransform;
        if (detail == null) return;
        filterPanel=Root("SongFilterPanel",detail,
            .026f,.055f,.974f,.958f);

        Image background=filterPanel.gameObject.AddComponent<Image>();
        background.sprite=RuruConsoleUI.Shell;
        background.type=Image.Type.Sliced;
        background.color=Color.white;
        background.raycastTarget=true;
        filterPanelGroup=filterPanel.gameObject.AddComponent<CanvasGroup>();
        filterPanelGroup.blocksRaycasts=true;

        // A narrow accent line is enough to indicate a separate mode.
        RectTransform edge=Root("TopAccent",filterPanel,
            .045f,.784f,.955f,.789f);
        Image line=edge.gameObject.AddComponent<Image>();
        line.color=RuruConsoleUI.Lilac;
        line.raycastTarget=false;

        filterTitle=Label("FilterTitle",filterPanel,
            .057f,.824f,.440f,.951f,25,Ink,
            TextAlignmentOptions.MidlineLeft);
        matchesCaption=Label("ResultCount",filterPanel,
            .480f,.835f,.940f,.935f,18,SoftInk,
            TextAlignmentOptions.MidlineRight);

        // The range labels occupy their own rows; values are at the far
        // right and the full-width handles sit below each caption.
        levelHeading=Label("LevelHeading",filterPanel,
            .059f,.668f,.305f,.750f,22,Ink,
            TextAlignmentOptions.MidlineLeft);
        levelValues=Label("LevelValues",filterPanel,
            .607f,.668f,.941f,.750f,21,Ink,
            TextAlignmentOptions.MidlineRight);
        RectTransform levelAnchor=Root("LevelRange",filterPanel,
            .071f,.518f,.929f,.650f);
        levelAnchor.gameObject.AddComponent<CanvasRenderer>();
        levelRange=levelAnchor.gameObject.AddComponent<SongSelectRangeBar>();
        levelRange.color=Color.white;
        levelRange.Changed=(low,high)=>owner.SetLevelRange(low,high);

        bpmHeading=Label("BpmHeading",filterPanel,
            .059f,.403f,.305f,.485f,22,Ink,
            TextAlignmentOptions.MidlineLeft);
        bpmValues=Label("BpmValues",filterPanel,
            .607f,.403f,.941f,.485f,21,Ink,
            TextAlignmentOptions.MidlineRight);
        RectTransform bpmAnchor=Root("BpmRange",filterPanel,
            .071f,.255f,.929f,.387f);
        bpmAnchor.gameObject.AddComponent<CanvasRenderer>();
        bpmRange=bpmAnchor.gameObject.AddComponent<SongSelectRangeBar>();
        bpmRange.color=Color.white;
        bpmRange.Changed=(low,high)=>owner.SetBpmRange(low,high);

        Button reset=ButtonAt("ResetFilter",filterPanel,
            .059f,.065f,.315f,.170f,out resetCaption,18);
        reset.onClick.AddListener(owner.ResetBrowseFilters);
        Button done=ButtonAt("DoneFilter",filterPanel,
            .723f,.065f,.941f,.170f,out doneCaption,19);
        done.onClick.AddListener(()=>TryCloseFilterPanel());
        var doneImage=done.targetGraphic as Image;
        if(doneImage!=null) doneImage.sprite=RuruConsoleUI.MainKey;
        doneCaption.color=RuruConsoleUI.Ink;

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
                TryCloseFilterPanel();
        }

        if(filterPanel != null && filterPanel.gameObject.activeSelf)
        {
            filterEnter=Mathf.Min(1f,filterEnter+
                Time.unscaledDeltaTime/.18f);
            float eased=1f-(1f-filterEnter)*(1f-filterEnter);
            filterPanel.anchoredPosition=
                new Vector2(22f*(1f-eased),0f);
            if(filterPanelGroup!=null)
                filterPanelGroup.alpha=eased;
        }

    }

    private void AddShelfRails(RectTransform shelf)
    {
        // Avoid heavy black boxes behind every cartridge. Thin shelves
        // indicate storage without adding another frame to each button.
        for (int i=0;i<6;i++)
        {
            int row=i/2, col=i%2;
            float y=.537f-row*.220f;
            float left=col==0?.042f:.519f;
            float right=col==0?.484f:.958f;
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
        bg.sprite=RuruConsoleUI.AltKey;
        bg.type=Image.Type.Sliced;
        bg.color=Color.white;
        Button button=go.GetComponent<Button>();
        button.targetGraphic=bg;
        ColorBlock colors=button.colors;
        colors.normalColor=Color.white;
        colors.highlightedColor=new Color(.92f,.93f,1f,1f);
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
        bg.sprite=RuruConsoleUI.MainKey;
        bg.type=Image.Type.Sliced;
        bg.color=Color.white;
        bg.raycastTarget=true;
        searchBackground=bg;
        searchFocusOutline=go.AddComponent<Outline>();
        searchFocusOutline.effectDistance=new Vector2(1.4f,-1.4f);
        searchFocusOutline.useGraphicAlpha=false;
        searchFocusOutline.effectColor=Color.clear;

        RectTransform viewport=Root("TextArea",rect,.045f,.06f,.955f,.94f);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text typed=Label("TypedText",viewport,0f,0f,1f,1f,19,
            RuruConsoleUI.Ink,TextAlignmentOptions.MidlineLeft);
        TMP_Text placeholder=Label("Placeholder",viewport,0f,0f,1f,1f,
            19,new Color32(85,69,108,255),TextAlignmentOptions.MidlineLeft);
        searchPlaceholder=placeholder;
        TMP_InputField field=go.GetComponent<TMP_InputField>();
        field.targetGraphic=bg;
        field.textViewport=viewport;
        field.textComponent=(TextMeshProUGUI)typed;
        field.placeholder=(TextMeshProUGUI)placeholder;
        field.lineType=TMP_InputField.LineType.SingleLine;
        field.characterLimit=80;
        // TMP's real input caret tracks insertion position, selection,
        // IME composition and keyboard navigation. Do not add fake '|'
        // characters to the user's search query.
        field.caretColor=RuruConsoleUI.Ink;
        field.customCaretColor=true;
        field.caretWidth=3;
        field.caretBlinkRate=.75f;
        field.selectionColor=new Color(.69f,.53f,.75f,.4f);
        field.onSelect.AddListener(_ => SetSearchFocusAppearance(true));
        field.onDeselect.AddListener(_ => SetSearchFocusAppearance(false));
        field.onValueChanged.AddListener(owner.SetSearchQuery);
        SetSearchFocusAppearance(false);
        return field;
    }
}
