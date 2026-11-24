using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Bind the author's actual scene UI in the Inspector. Does not create buttons,
// invent dialogue, or select character artwork on its own.
public sealed class RuruStoryPlayer : MonoBehaviour
{
    [SerializeField] private RuruStorySequence sequence;
    [SerializeField] private RectTransform artworkRoot;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text dialogueLabel;
    [SerializeField] private Button nextButton;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField, Min(0f)] private float secondsPerCharacter = .025f;
    [SerializeField] private UnityEvent onFinished = new UnityEvent();

    private RuruStoryStage stage;
    private Coroutine reveal;
    private int index = -1;
    private bool running;

    private void OnEnable()
    {
        if (nextButton != null) nextButton.onClick.AddListener(Advance);
        if (playOnEnable && sequence != null) Play(sequence);
    }

    private void OnDisable()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(Advance);
        StopReveal();
        running = false;
    }

    public void Play(RuruStorySequence story)
    {
        StopReveal();
        if (artworkRoot == null || speakerLabel == null || dialogueLabel == null)
        {
            Debug.LogWarning("StoryPlayer: connect the artwork root, speaker and dialogue labels.",this);
            return;
        }
        sequence = story;
        if (stage == null) stage = RuruStoryStage.Create(artworkRoot);
        stage.Clear();
        index = 0;
        running = sequence != null && sequence.Count > 0;
        if (running) ShowLine();
        else { speakerLabel.text = ""; dialogueLabel.text = ""; }
    }

    public void Advance()
    {
        if (!running) return;
        if (reveal != null)
        {
            StopReveal(); dialogueLabel.maxVisibleCharacters = int.MaxValue;
            return;
        }
        index++;
        if (index < sequence.Count) ShowLine();
        else { running = false; onFinished.Invoke(); }
    }

    private void ShowLine()
    {
        RuruStorySequence.Line line = sequence.lines[index];
        speakerLabel.text = line != null ? line.speaker : "";
        dialogueLabel.text = line != null ? line.dialogue : "";
        stage.Show(sequence,index);
        dialogueLabel.maxVisibleCharacters = int.MaxValue;
        if (secondsPerCharacter > 0f && !string.IsNullOrEmpty(dialogueLabel.text))
        {
            dialogueLabel.maxVisibleCharacters = 0;
            reveal = StartCoroutine(Reveal());
        }
    }

    private IEnumerator Reveal()
    {
        dialogueLabel.ForceMeshUpdate();
        int count = dialogueLabel.textInfo.characterCount;
        float elapsed = 0f;
        while (dialogueLabel.maxVisibleCharacters < count)
        {
            elapsed += Time.unscaledDeltaTime;
            dialogueLabel.maxVisibleCharacters = Mathf.Min(count,
                Mathf.FloorToInt(elapsed / Mathf.Max(.001f,secondsPerCharacter)));
            yield return null;
        }
        dialogueLabel.maxVisibleCharacters = int.MaxValue;
        reveal = null;
    }

    private void StopReveal()
    {
        if (reveal != null) StopCoroutine(reveal);
        reveal = null;
    }
}
