using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

        RefreshSelected();
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