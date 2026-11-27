using System;
using UnityEngine;

[Serializable]
public sealed class StoryLine
{
    [Tooltip("화자 이름. 루루/피노 외의 이름도 입력할 수 있습니다.")]
    public string speaker = "루루";
    [TextArea(3, 8)] public string text;
    [Tooltip("공용 표정 번호. -1 = 자동/기본. 루루 0 생각, 1 기본, 2 불만, 3 격려; 피노 0 걱정, 1 기본, 2 자신감, 3 소개, 4 질문.")]
    public int expression = -1;
    [Tooltip("직접 지정하면 공용 표정보다 우선합니다.")]
    public Sprite leftPortrait;
    public Sprite rightPortrait;
    public bool hideLeft;
    public bool hideRight;
    [Tooltip("배경 이미지. 비워 두면 스토리 기본 배경을 사용합니다.")]
    public Sprite background;
    [Tooltip("독립 스토리 씬에서 이 대사가 표시될 때 재생되는 효과음.")]
    public AudioClip sound;
}

[CreateAssetMenu(menuName = "Ruru In Beat/Story/스토리 데이터", fileName = "NewStory")]
public sealed class StoryData : ScriptableObject
{
    [Tooltip("저장 기록용 고유 ID. 다른 스토리와 겹치지 않게 설정하세요. 비워 두면 에셋 이름을 사용합니다.")]
    public string storyId;
    public string title;
    public Sprite background;
    public AudioClip music;
    [Min(0)] public float secondsPerCharacter = .028f;
    [Tooltip("위에서 아래로 재생됩니다. Size 또는 + / - 로 추가·삭제하고 순서를 바꿀 수 있습니다.")]
    public StoryLine[] lines = new StoryLine[0];

    public string Id => string.IsNullOrWhiteSpace(storyId) ? name : storyId.Trim();
    public bool HasLines => lines != null && Array.Exists(lines, line => line != null);
}
