using UnityEngine;

public class FinishFlag : MonoBehaviour
{
    public GameObject scoreboardUI;

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            Debug.Log(player.name + " wins!");

            ScoreManager.Instance.AddScore(player.controlType);

            scoreboardUI.SetActive(true);

            Time.timeScale = 0f;
        }
    }
}