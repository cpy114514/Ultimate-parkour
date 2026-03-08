using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance;

    public float nextRoundDelay = 3f;

    bool roundEnding = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayerWin(PlayerController.ControlType player)
    {
        if (roundEnding) return;

        roundEnding = true;

        ScoreManager.Instance.AddScore(player);

        ScoreboardUI board = FindObjectOfType<ScoreboardUI>();
        if (board != null)
        {
            board.panel.SetActive(true);
            board.UpdateScores();
        }

        StartCoroutine(NextRound());
    }

    System.Collections.IEnumerator NextRound()
    {
        yield return new WaitForSecondsRealtime(nextRoundDelay);

        Time.timeScale = 1f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}