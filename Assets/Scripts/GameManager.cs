using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject wasdPrefab;
    public GameObject arrowPrefab;
    public GameObject ijklPrefab;

    public Transform[] spawnPoints;

    void Start()
    {
        if (PlayerSessionManager.Instance == null)
        {
            Debug.Log("SessionManager is NULL!");
            return;
        }

        var players = PlayerSessionManager.Instance.activePlayers;

        Debug.Log("Players in session: " + players.Count);
        Debug.Log("SpawnPoints: " + spawnPoints.Length);

        for (int i = 0; i < players.Count; i++)
        {
            Debug.Log("Spawning: " + players[i]);

            GameObject prefab = GetPrefab(players[i]);

            Instantiate(
                prefab,
                spawnPoints[i].position,
                Quaternion.identity
            );
        }
    }

    GameObject GetPrefab(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return wasdPrefab;
            case PlayerController.ControlType.ArrowKeys:
                return arrowPrefab;
            case PlayerController.ControlType.IJKL:
                return ijklPrefab;
        }
        return null;
    }
}