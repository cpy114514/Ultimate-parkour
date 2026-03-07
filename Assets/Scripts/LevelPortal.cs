using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;
public class LevelPortal : MonoBehaviour
{
    public string levelSceneName;
    public float countdownTime = 3f;
    public TextMeshProUGUI countdownText;
    HashSet<PlayerController> playersOnPortal =
        new HashSet<PlayerController>();

    float timer;
    bool countingDown = false;

    void Update()
    {
        int requiredPlayers =
            FindObjectsOfType<PlayerController>().Length;

        if (requiredPlayers == 0)
            return;

        if (playersOnPortal.Count >= requiredPlayers)
        {
            timer += Time.deltaTime;

            float timeLeft = countdownTime - timer;
            countdownText.text = Mathf.Ceil(timeLeft).ToString();

            if (timer >= countdownTime)
            {
                Debug.Log("Scene loading now!");

                // ⭐ 重新写入当前真实玩家
                var currentPlayers = FindObjectsOfType<PlayerController>();

                PlayerSessionManager.Instance.activePlayers.Clear();

                foreach (var p in currentPlayers)
                {
                    PlayerSessionManager.Instance.activePlayers.Add(p.controlType);
                }

                SceneManager.LoadScene(levelSceneName);
            }
        }
        else
        {
            timer = 0f;
            countdownText.text = "";
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController p = other.GetComponentInParent<PlayerController>();

        if (p == null)
            return;

        if (!playersOnPortal.Contains(p))
        {
            playersOnPortal.Add(p);
            Debug.Log(p.name + " entered portal. Count: " + playersOnPortal.Count);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerController p = other.GetComponentInParent<PlayerController>();

        if (p == null)
            return;

        if (playersOnPortal.Contains(p))
        {
            playersOnPortal.Remove(p);
            Debug.Log(p.name + " left portal. Count: " + playersOnPortal.Count);
        }
    }
}