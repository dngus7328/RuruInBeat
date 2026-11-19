using UnityEngine;

/// <summary>
/// A cartridge is lifted VERTICALLY at its own shelf position when first
/// clicked. It never travels to the jacket panel. A second click starts
/// gameplay directly through SongSelectMenuUI.
/// </summary>
[DisallowMultipleComponent]
public sealed class SongSelectArcadeMotion : MonoBehaviour
{
    [SerializeField] private bool enableMotion = true;
    [SerializeField, Range(15f, 65f)] private float selectedLiftPixels = 42f;
    [SerializeField, Range(5f, 30f)] private float selectedFollowSpeed = 18f;
    [SerializeField, Range(1f, 1.2f)] private float selectedScale = 1.075f;

    private SongSelectMenuUI menu;
    private RectTransform[] cards;
    private Vector2[] restPositions;
    private Vector3[] restScales;
    private Quaternion[] restRotations;
    private int[] originalSiblingIndices;
    private int liftedIndex = -1;
    private int frontSlot = -1;
    private bool ready;

    public bool CanAnimate => ready && isActiveAndEnabled && enableMotion;

    // No insertion animation exists. Retained solely for older callers.
    public float InsertionSeconds => 0f;
    public void BeginInsertion() { }

    public void PulseSelection()
    {
        if (menu != null) LiftCartridge(menu.ArcadeSelectedIndex);
    }

    public void LiftCartridge(int index)
    {
        if (index < 0) return;
        liftedIndex = index;
        frontSlot = -1;
    }

    public void PulsePage(int direction)
    {
        liftedIndex = -1;
        frontSlot = -1;
        // No horizontal page-flying effect. The shelf remains stationary.
    }

    private void Start()
    {
        menu = GetComponent<SongSelectMenuUI>();
        RectTransform shelf = transform.Find("TrackListPanel") as RectTransform;
        if (menu == null || shelf == null) return;

        cards = new RectTransform[6];
        restPositions = new Vector2[cards.Length];
        restScales = new Vector3[cards.Length];
        restRotations = new Quaternion[cards.Length];
        originalSiblingIndices = new int[cards.Length];

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = shelf.Find("SongRow_" + (i + 1).ToString("00"))
                as RectTransform;
            if (cards[i] == null) continue;
            restPositions[i] = cards[i].anchoredPosition;
            restScales[i] = cards[i].localScale;
            restRotations[i] = cards[i].localRotation;
            originalSiblingIndices[i] = cards[i].GetSiblingIndex();
        }

        ready = true;
    }

    private void LateUpdate()
    {
        if (!CanAnimate || menu == null) return;

        int current = menu.ArcadeSelectedIndex;
        bool clicked = menu.ArcadeCartridgeRaised &&
            current >= 0 && current == liftedIndex;
        int activeSlot = clicked ? current % cards.Length : -1;

        if (activeSlot != frontSlot)
        {
            RestoreOriginalSiblingOrder();

            // Bring the selected cartridge above the other cartridges only.
            // Never put it above the search, filter, pager or preview controls.
            if (activeSlot >= 0 && cards[activeSlot] != null)
            {
                int lastCartridgeIndex = -1;
                for (int i = 0; i < cards.Length; i++)
                    if (cards[i] != null)
                        lastCartridgeIndex = Mathf.Max(
                            lastCartridgeIndex, cards[i].GetSiblingIndex());
                cards[activeSlot].SetSiblingIndex(lastCartridgeIndex);
            }
            frontSlot = activeSlot;
        }

        float follow = 1f - Mathf.Exp(
            -selectedFollowSpeed * Mathf.Min(Time.unscaledDeltaTime, .05f));
        for (int i = 0; i < cards.Length; i++)
        {
            RectTransform card = cards[i];
            if (card == null) continue;

            bool raised = i == activeSlot;
            Vector2 target = restPositions[i] +
                Vector2.up * (raised ? selectedLiftPixels : 0f);
            card.anchoredPosition = Vector2.Lerp(
                card.anchoredPosition, target, follow);
            card.localScale = Vector3.Lerp(
                card.localScale,
                restScales[i] * (raised ? selectedScale : 1f), follow);
            // Keep its original cartridge tilt. No side shift, flying
            // copy, case recoil or destination movement is performed.
            card.localRotation = restRotations[i];
        }
    }

    private void RestoreOriginalSiblingOrder()
    {
        if (cards == null || originalSiblingIndices == null) return;
        for (int i = 0; i < cards.Length; i++)
            if (cards[i] != null)
                cards[i].SetSiblingIndex(originalSiblingIndices[i]);
    }

    private void OnDisable()
    {
        if (cards == null) return;
        RestoreOriginalSiblingOrder();
        for (int i = 0; i < cards.Length; i++)
        {
            RectTransform card = cards[i];
            if (card == null) continue;
            card.anchoredPosition = restPositions[i];
            card.localScale = restScales[i];
            card.localRotation = restRotations[i];
        }
        liftedIndex = -1;
        frontSlot = -1;
    }
}
