using UnityEngine;
using TMPro;

public class ScoreboardUI : MonoBehaviour
{
    public GameObject panel;

    public TextMeshProUGUI wasdText;
    public TextMeshProUGUI arrowText;
    public TextMeshProUGUI ijklText;

    void OnEnable()
    {
        UpdateScores();
    }

    public void UpdateScores()
    {
        var scores = ScoreManager.Instance.scores;

        wasdText.text = "WASD : " + scores[PlayerController.ControlType.WASD];
        arrowText.text = "ARROWS : " + scores[PlayerController.ControlType.ArrowKeys];
        ijklText.text = "IJKL : " + scores[PlayerController.ControlType.IJKL];
    }
}