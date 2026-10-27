using System.Collections;
using TMPro;
using UnityEngine;

public class ScoreDisplay : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Format")]
    [SerializeField] private int minimumDigits = 7;

    [Header("Count Animation")]
    [SerializeField] private float countDuration = 0.14f;

    private int displayedScore;
    private int targetScore;

    private Coroutine countRoutine;

    private void Awake()
    {
        if (scoreText == null)
        {
            scoreText = GetComponent<TMP_Text>();
        }

        displayedScore = 0;
        targetScore = 0;

        UpdateText(0);
    }

    public void SetScore(int score)
    {
        targetScore =
            Mathf.Max(
                0,
                score
            );

        if (countRoutine != null)
        {
            StopCoroutine(
                countRoutine
            );
        }

        countRoutine =
            StartCoroutine(
                CountToTarget()
            );
    }

    private IEnumerator CountToTarget()
    {
        int startScore =
            displayedScore;

        if (startScore == targetScore)
        {
            UpdateText(
                targetScore
            );

            countRoutine = null;

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < countDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    countDuration
                );

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            displayedScore =
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        startScore,
                        targetScore,
                        eased
                    )
                );

            UpdateText(
                displayedScore
            );

            yield return null;
        }

        displayedScore =
            targetScore;

        UpdateText(
            displayedScore
        );

        countRoutine = null;
    }

    private void UpdateText(int score)
    {
        if (scoreText == null)
            return;

        scoreText.text =
            score.ToString(
                new string(
                    '0',
                    minimumDigits
                )
            );
    }
}