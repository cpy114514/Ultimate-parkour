using UnityEngine;
using System.Collections.Generic;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public Dictionary<PlayerController.ControlType, int> scores =
        new Dictionary<PlayerController.ControlType, int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            foreach (PlayerController.ControlType type in
                     System.Enum.GetValues(typeof(PlayerController.ControlType)))
            {
                scores[type] = 0;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddScore(PlayerController.ControlType player)
    {
        scores[player]++;
    }
}