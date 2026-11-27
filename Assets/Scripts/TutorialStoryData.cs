using UnityEngine;

[CreateAssetMenu(menuName = "Ruru In Beat/Story/튜토리얼 대사", fileName = "TutorialStory")]
public sealed class TutorialStoryData : ScriptableObject
{
    [Header("도입부")]
    public string openingTitle = "게임 속 이상 현상";
    public Sprite openingBackground;
    public StoryLine[] opening;
    [Header("연습 종료")]
    public StoryLine[] closing;
    [TextArea(2, 5)] public string incompleteClosing;
    [Header("실습 6단계 (순서 유지)")]
    public string[] chapterNames;
    [TextArea(2, 5)] public string[] chapterGuides;
    [TextArea(2, 5)] public string[] chapterSuccess;
    [TextArea(2, 5)] public string[] chapterLead;
    [TextArea(2, 5)] public string checkpointFailure;
}
