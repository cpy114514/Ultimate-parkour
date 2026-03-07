using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    [Header("Player Prefabs")]
    public GameObject wasdPrefab;
    public GameObject arrowPrefab;
    public GameObject ijklPrefab;

    public Transform[] spawnPoints;

    public float holdDuration = 1.2f;

    Dictionary<PlayerController.ControlType, GameObject> players =
        new Dictionary<PlayerController.ControlType, GameObject>();

    Dictionary<PlayerController.ControlType, float> holdTimers =
        new Dictionary<PlayerController.ControlType, float>();

    private void Start()
    {
        TryJoin(PlayerController.ControlType.WASD);
    }

    void Update()
    {
        HandleJoin();
        HandleHoldLeave();

        // 至少一个玩家才能开始
        if (Input.GetKeyDown(KeyCode.Space) && players.Count > 0)
        {
            PlayerSessionManager.Instance.activePlayers.Clear();

            foreach (var p in players.Keys)
                PlayerSessionManager.Instance.activePlayers.Add(p);

            SceneManager.LoadScene("Game");
        }
    }

    void HandleJoin()
    {
        if (Input.GetKeyDown(KeyCode.E))
            TryJoin(PlayerController.ControlType.WASD);

        if (Input.GetKeyDown(KeyCode.Return))
            TryJoin(PlayerController.ControlType.ArrowKeys);

        if (Input.GetKeyDown(KeyCode.U))
            TryJoin(PlayerController.ControlType.IJKL);
    }

    void HandleHoldLeave()
    {
        HandleSingleHoldLeave(PlayerController.ControlType.WASD, KeyCode.Q);
        HandleSingleHoldLeave(PlayerController.ControlType.ArrowKeys, KeyCode.RightShift);
        HandleSingleHoldLeave(PlayerController.ControlType.IJKL, KeyCode.O);
    }

    void HandleSingleHoldLeave(PlayerController.ControlType type, KeyCode key)
    {
        if (!players.ContainsKey(type)) return;

        if (Input.GetKey(key))
        {
            if (!holdTimers.ContainsKey(type))
                holdTimers[type] = 0f;

            holdTimers[type] += Time.deltaTime;

            if (holdTimers[type] >= holdDuration)
            {
                Destroy(players[type]);
                players.Remove(type);
                holdTimers.Remove(type);
            }
        }
        else
        {
            if (holdTimers.ContainsKey(type))
                holdTimers[type] = 0f;
        }
    }

    void TryJoin(PlayerController.ControlType type)
    {
        if (players.ContainsKey(type)) return;

        int index = players.Count;
        if (index >= spawnPoints.Length) return;

        GameObject prefab = GetPrefab(type);
        GameObject p = Instantiate(prefab, spawnPoints[index].position, Quaternion.identity);

        p.GetComponent<PlayerController>().controlType = type;

        players.Add(type, p);
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