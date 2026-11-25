using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SettingsCategoryController : MonoBehaviour
{
    [System.Serializable]
    public class CategoryItem
    {
        [Header("버튼")]
        public Button button;

        [Header("선택 상태")]
        public GameObject selectedBG;

        [Header("선택 전 미리보기")]
        public GameObject focusBG;

        [Header("미리보기 애니메이션")]
        public SettingsFocusAnimation focusAnimation;

        [Header("오른쪽 페이지")]
        public GameObject page;
    }

    [Header("카테고리")]
    [SerializeField] private CategoryItem[] categories;

    [Header("처음 선택")]
    [SerializeField] private int defaultIndex = 0;

    private int selectedIndex;
    private int focusIndex;

    private void Start()
    {
        if (categories == null || categories.Length == 0)
        {
            return;
        }

        selectedIndex = Mathf.Clamp(
            defaultIndex,
            0,
            categories.Length - 1
        );

        focusIndex = selectedIndex;

        for (int i = 0; i < categories.Length; i++)
        {
            int index = i;

            if (categories[i].button != null)
            {
                categories[i].button.onClick.AddListener(
                    () => SelectCategory(index)
                );

                AddPointerEvents(
                    categories[i].button,
                    index
                );
                StyleCategory(categories[i]);
            }

            // 시작할 때 Focus 전부 숨김
            if (categories[i].focusAnimation != null)
            {
                categories[i].focusAnimation.HideImmediate();
            }
            else if (categories[i].focusBG != null)
            {
                categories[i].focusBG.SetActive(false);
            }
        }

        foreach (CategoryItem category in categories)
            if (category.page != null && category.page.name == "ControlPage")
                BuildMouseControlGuide(category.page.transform);
        RefreshSelected();
    }

    // Keep the authored window and its controls. Replace only the category
    // chrome so selected, hovered and idle states use the shared deck art.
    private static void StyleCategory(CategoryItem item)
    {
        if(item.button==null) return;
        var baseImage=item.button.GetComponent<Image>();
        if(baseImage!=null)
        {
            baseImage.sprite=RuruConsoleUI.AltKey;
            baseImage.type=Image.Type.Sliced;
            baseImage.color=Color.white;
        }
        if(item.selectedBG!=null)
        {
            var art=item.selectedBG.GetComponent<Image>();
            if(art!=null)
            {
                art.sprite=RuruConsoleUI.MainKey;
                art.type=Image.Type.Sliced;
                art.color=Color.white;
                art.raycastTarget=false;
            }
            var rt=item.selectedBG.transform as RectTransform;
            if(rt!=null)
            {
                rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
                rt.offsetMin=rt.offsetMax=Vector2.zero;
                rt.SetAsFirstSibling();
            }
        }
        if(item.focusBG!=null)
        {
            var art=item.focusBG.GetComponent<Image>();
            if(art!=null)
            {
                art.sprite=RuruConsoleUI.AltKey;
                art.type=Image.Type.Sliced;
                art.color=new Color(.82f,.76f,.94f,.95f);
                art.raycastTarget=false;
                art.transform.SetAsFirstSibling();
            }
        }
    }

    private static void BuildMouseControlGuide(Transform page)
    {
        if (page.Find("MouseControlGuide") != null) return;
        // Keep the authored category heading/icon and preserve all existing
        // legacy objects for the old disabled keyboard gameplay scene.
        for (int i=0;i<page.childCount;i++)
        {
            Transform child=page.GetChild(i);
            var rect=child as RectTransform;
            bool heading=(child.name=="Icon" || child.name=="Label") &&
                rect!=null && rect.anchoredPosition.y>350f;
            if(!heading) child.gameObject.SetActive(false);
        }

        var area=RuruConsoleUI.Frame(page,"MouseControlGuide",.055f,.055f,.93f,.94f,RuruConsoleUI.Shell).rectTransform;
        RuruConsoleUI.Text(area,"Heading","MOUSE PLAY / CONTROL",.04f,.86f,.96f,.96f,30,RuruConsoleUI.Paper,display:true);
        string[] icons={"Guide_MouseMove","Guide_LeftClick","Guide_RightClick","Guide_Wheel"};
        string[] names={"이동 / 회피","왼쪽 클릭","오른쪽 클릭","마우스 휠"};
        string[] hints={"피노를 움직여 붉은 탄막을 피해요.","L 노트의 두 원이 겹칠 때 클릭해요.","R 노트의 두 원이 겹칠 때 클릭해요.","W 노트를 따라 끝까지 휠을 돌려요."};
        string[] keys={"MOVE","L","R","W"};
        for(int i=0;i<4;i++)
        {
            int col=i%2,row=i/2;
            float left=.04f+col*.47f,bottom=.52f-row*.29f;
            var tile=RuruConsoleUI.Frame(area,"MouseGuide_"+keys[i],left,bottom,left+.45f,bottom+.265f,RuruConsoleUI.Inset).rectTransform;
            var art=RuruConsoleUI.Region(tile,"GuideIcon",.035f,.16f,.25f,.84f).gameObject.AddComponent<Image>();
            art.sprite=Resources.Load<Sprite>("RuruArt/Tutorial/"+icons[i]);
            art.preserveAspect=true; art.raycastTarget=false;
            art.enabled=art.sprite!=null;
            RuruConsoleUI.Text(tile,"Input",names[i],.28f,.52f,.96f,.91f,28,RuruConsoleUI.Paper);
            var hint=RuruConsoleUI.Text(tile,"Instruction",hints[i],.28f,.10f,.96f,.52f,21,RuruConsoleUI.Muted);
            hint.enableWordWrapping=true;
        }
        RuruConsoleUI.Text(area,"PauseHelp","ESC  /  일시정지 및 재개",.04f,.055f,.50f,.18f,24,RuruConsoleUI.Muted);
        RuruConsoleUI.Key(area,"ReplayTutorial","튜토리얼 다시 하기",.55f,.055f,.96f,.18f,true,()=>{
            if(SceneLoadingTransition.IsLoading) return;
            MouseModeSongSelection.Choose(null);
            SceneLoadingTransition.Load("Tutorial");
        });
    }

    private void Update()
    {
        if (categories == null || categories.Length == 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveFocus(-1);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveFocus(1);
        }

        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SelectCategory(focusIndex);
        }
    }

    // =====================================================
    // 키보드 Focus 이동
    // =====================================================

    private void MoveFocus(int direction)
    {
        int oldIndex = focusIndex;

        focusIndex += direction;

        if (focusIndex < 0)
        {
            focusIndex = categories.Length - 1;
        }
        else if (focusIndex >= categories.Length)
        {
            focusIndex = 0;
        }

        ChangeFocus(
            oldIndex,
            focusIndex,
            direction
        );

        if (categories[focusIndex].button != null &&
            EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                categories[focusIndex].button.gameObject
            );
        }
    }

    // =====================================================
    // Focus 변경
    // =====================================================

    private void ChangeFocus(
        int oldIndex,
        int newIndex,
        int direction)
    {
        if (oldIndex >= 0 &&
            oldIndex < categories.Length &&
            oldIndex != selectedIndex)
        {
            HideFocus(oldIndex);
        }

        // 실제 선택된 버튼이면 FocusBG는 필요 없음
        if (newIndex == selectedIndex)
        {
            HideFocus(newIndex);
            return;
        }

        ShowFocus(
            newIndex,
            direction
        );
    }

    private void ShowFocus(
        int index,
        int direction)
    {
        if (index < 0 || index >= categories.Length)
        {
            return;
        }

        CategoryItem item = categories[index];

        if (item.focusAnimation != null)
        {
            item.focusAnimation.Show(direction);
        }
        else if (item.focusBG != null)
        {
            item.focusBG.SetActive(true);
        }
    }

    private void HideFocus(int index)
    {
        if (index < 0 || index >= categories.Length)
        {
            return;
        }

        CategoryItem item = categories[index];

        if (item.focusAnimation != null)
        {
            item.focusAnimation.Hide();
        }
        else if (item.focusBG != null)
        {
            item.focusBG.SetActive(false);
        }
    }

    // =====================================================
    // 실제 선택
    // =====================================================

    public void SelectCategory(int index)
    {
        if (index < 0 || index >= categories.Length)
        {
            return;
        }

        // 현재 미리보기 제거
        HideFocus(focusIndex);

        selectedIndex = index;
        focusIndex = index;

        RefreshSelected();

        if (categories[index].button != null &&
            EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                categories[index].button.gameObject
            );
        }
    }

    private void RefreshSelected()
    {
        for (int i = 0; i < categories.Length; i++)
        {
            bool selected =
                i == selectedIndex;

            if (categories[i].selectedBG != null)
            {
                categories[i].selectedBG.SetActive(selected);
            }

            if(categories[i].button!=null)
            {
                var label=categories[i].button.transform.Find("Label")?.GetComponent<TMP_Text>();
                if(label!=null) label.color=selected?RuruConsoleUI.Ink:RuruConsoleUI.Paper;
                var icon=categories[i].button.transform.Find("Icon")?.GetComponent<Image>();
                if(icon!=null) icon.color=selected?RuruConsoleUI.Ink:RuruConsoleUI.Paper;
            }
            if (categories[i].page != null)
            {
                categories[i].page.SetActive(selected);
            }
        }
    }

    // =====================================================
    // 마우스 Hover
    // =====================================================

    private void AddPointerEvents(
        Button button,
        int index)
    {
        EventTrigger trigger =
            button.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger =
                button.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers =
                new List<EventTrigger.Entry>();
        }

        EventTrigger.Entry enterEntry =
            new EventTrigger.Entry();

        enterEntry.eventID =
            EventTriggerType.PointerEnter;

        enterEntry.callback.AddListener((data) =>
        {
            int oldIndex = focusIndex;

            int direction = 0;

            if (index > oldIndex)
            {
                direction = 1;
            }
            else if (index < oldIndex)
            {
                direction = -1;
            }

            focusIndex = index;

            ChangeFocus(
                oldIndex,
                focusIndex,
                direction
            );

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(
                    button.gameObject
                );
            }
        });

        trigger.triggers.Add(enterEntry);
    }

    // =====================================================
    // 확인용
    // =====================================================

    public int GetSelectedIndex()
    {
        return selectedIndex;
    }

    public int GetFocusIndex()
    {
        return focusIndex;
    }
}