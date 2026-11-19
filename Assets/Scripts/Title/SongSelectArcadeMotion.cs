using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restrained, unscaled-time UI feedback for the dedicated Arcade scene:
/// stepped cartridge entry, selected cartridge sliding forward, case swap,
/// jacket recoil and a small difficulty readout pulse.
/// The ordinary SongSelectScene is unaffected.
/// </summary>
[DisallowMultipleComponent]
public sealed class SongSelectArcadeMotion : MonoBehaviour
{
    [SerializeField] private bool enableMotion = true;
    [SerializeField, Range(.1f, 1f)] private float entranceSeconds = .38f;
    [SerializeField, Range(4f, 30f)] private float selectedFollowSpeed = 15f;
    [SerializeField, Range(4f, 30f)] private float caseFollowSpeed = 14f;

    private SongSelectMenuUI menu;
    private RectTransform[] cards;
    private Vector2[] restPositions;
    private float[] restAngles;
    private RectTransform jacket;
    private RectTransform caseBack;
    private RectTransform title;
    private RectTransform level;
    private Vector2 jacketRest, caseRest, titleRest;
    private float jacketAngle, caseAngle;
    private int selected = -999, difficulty = -999;
    private float caseImpulse;
    private float levelImpulse;
    private float clickPulse;
    private bool inserting;
    private float insertionTime;
    public float InsertionSeconds => enableMotion ? .46f : 0f;

    public void PulseSelection()
    {
        clickPulse = 1f;
        caseImpulse = 1f;
        levelImpulse = .65f;
    }

    public void BeginInsertion()
    {
        inserting = true;
        insertionTime = 0f;
        clickPulse = 1f;
        caseImpulse = 1f;
    }

    private float elapsed;
    private bool ready;

    private void Start()
    {
        menu = GetComponent<SongSelectMenuUI>();
        Transform shelf = transform.Find("TrackListPanel");
        if (menu == null || shelf == null) return;

        cards = new RectTransform[6];
        restPositions = new Vector2[cards.Length];
        restAngles = new float[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = shelf.Find("SongRow_" + (i + 1).ToString("00"))
                as RectTransform;
            if (cards[i] == null) continue;
            restPositions[i] = cards[i].anchoredPosition;
            restAngles[i] = cards[i].localEulerAngles.z;
        }

        Transform panel = transform.Find("SelectedTrackPanel");
        if (panel != null)
        {
            jacket = panel.Find("JacketImage_Replaceable") as RectTransform;
            caseBack = panel.Find("Arcade_CartridgeBack") as RectTransform;
            title = panel.Find("SelectedTitle") as RectTransform;
            level = panel.Find("SelectedLevel") as RectTransform;
        }
        if(jacket != null)
        {
            jacketRest = jacket.anchoredPosition;
            jacketAngle = jacket.localEulerAngles.z;
        }
        if(caseBack != null)
        {
            caseRest = caseBack.anchoredPosition;
            caseAngle = caseBack.localEulerAngles.z;
        }
        if(title != null)titleRest = title.anchoredPosition;
        selected = menu.ArcadeSelectedIndex;
        difficulty = menu.ArcadeDifficultyIndex;
        ready = true;
        if(!enableMotion) return;

        for (int i = 0; i < cards.Length; i++)
            if (cards[i] != null)
                cards[i].anchoredPosition = restPositions[i] +
                    new Vector2(-70f, 0f);
    }

    private void LateUpdate()
    {
        if(!ready || !enableMotion)return;
        float dt = Mathf.Min(Time.unscaledDeltaTime,.05f);
        elapsed += dt;
        clickPulse = Mathf.Max(0f, clickPulse - dt * 4.3f);
        if (inserting)
            insertionTime = Mathf.Min(InsertionSeconds,
                insertionTime + dt);
        float insertion = inserting
            ? Mathf.Clamp01(insertionTime /
                Mathf.Max(.01f, InsertionSeconds)) : 0f;
        insertion = insertion * insertion * (3f - 2f * insertion);
        int current = menu.ArcadeSelectedIndex;
        int currentDifficulty = menu.ArcadeDifficultyIndex;
        if (current != selected)
        {
            selected = current;
            caseImpulse = 1f;
            levelImpulse = 1f;
        }
        if(currentDifficulty != difficulty)
        {
            difficulty = currentDifficulty;
            levelImpulse = 1f;
        }

        float slide = 1f - Mathf.Exp(-selectedFollowSpeed*dt);
        float ease = 1f - Mathf.Exp(-caseFollowSpeed*dt);
        for(int i=0;i<cards.Length;i++)
        {
            if(cards[i] == null)continue;
            float phase = Mathf.Clamp01((elapsed-i*.07f) /
                Mathf.Max(.1f,entranceSeconds));
            phase = 1f-(1f-phase)*(1f-phase)*(1f-phase);
            bool active = current >= 0 && i == current % cards.Length;
            bool hover = !active && RectTransformUtility
                .RectangleContainsScreenPoint(cards[i],
                    Input.mousePosition);
            float forward = active ? 22f + clickPulse * 8f :
                (hover ? 7f : 0f);
            if (active && inserting) forward += 66f * insertion;
            Vector2 target = restPositions[i] +
                new Vector2(-70f*(1f-phase)+forward*phase,0f);
            cards[i].anchoredPosition = Vector2.Lerp(
                cards[i].anchoredPosition,target,slide);
            float scale = active
                ? 1.025f + .045f * clickPulse -
                  (inserting ? .11f * insertion : 0f)
                : (hover ? 1.01f : 1f);
            cards[i].localScale = Vector3.Lerp(
                cards[i].localScale,Vector3.one*scale,slide);
            float targetAngle=restAngles[i] + (active ? -.45f : 0f);
            Quaternion rotation=Quaternion.Euler(0f,0f,targetAngle);
            cards[i].localRotation=Quaternion.Slerp(
                cards[i].localRotation,rotation,slide);
        }

        // Every track replacement makes the visible game package leave the
        // slot by a few pixels and settle back, without glowing or shaking.
        float nudge = Mathf.Sin(caseImpulse*Mathf.PI)*caseImpulse;
        if (inserting)
            nudge += Mathf.Sin(insertion * Mathf.PI) * .68f;
        if(jacket != null)
        {
            Vector2 target=jacketRest+new Vector2(12f*nudge,3f*nudge);
            jacket.anchoredPosition=Vector2.Lerp(
                jacket.anchoredPosition,target,ease);
            jacket.localRotation=Quaternion.Slerp(
                jacket.localRotation,Quaternion.Euler(0f,0f,
                    jacketAngle+1.4f*nudge),ease);
        }
        if(caseBack != null)
        {
            Vector2 target=caseRest+new Vector2(9f*nudge,2.5f*nudge);
            caseBack.anchoredPosition=Vector2.Lerp(
                caseBack.anchoredPosition,target,ease);
            caseBack.localRotation=Quaternion.Slerp(
                caseBack.localRotation,Quaternion.Euler(0f,0f,
                    caseAngle+1.4f*nudge),ease);
        }
        if(title != null)
            title.anchoredPosition=Vector2.Lerp(
                title.anchoredPosition,
                titleRest+new Vector2(5f*nudge,0f),ease);

        if(level != null)
            level.localScale=Vector3.Lerp(level.localScale,
                Vector3.one*(1f+.035f*levelImpulse),ease);
        caseImpulse=Mathf.Max(0f,caseImpulse-dt*3.2f);
        levelImpulse=Mathf.Max(0f,levelImpulse-dt*3.8f);
    }

    private void OnDisable()
    {
        if(cards != null)
            for(int i=0;i<cards.Length;i++)
                if(cards[i] != null)
                {
                    cards[i].anchoredPosition=restPositions[i];
                    cards[i].localScale=Vector3.one;
                    cards[i].localRotation=Quaternion.Euler(
                        0f,0f,restAngles[i]);
                }
        if(jacket != null)
        {
            jacket.anchoredPosition=jacketRest;
            jacket.localRotation=Quaternion.Euler(0f,0f,jacketAngle);
        }
        if(caseBack != null)
        {
            caseBack.anchoredPosition=caseRest;
            caseBack.localRotation=Quaternion.Euler(0f,0f,caseAngle);
        }
        if(title != null)title.anchoredPosition=titleRest;
        if(level != null)level.localScale=Vector3.one;
    }
}
