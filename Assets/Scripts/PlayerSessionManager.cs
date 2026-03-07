using UnityEngine;
using System.Collections.Generic;

public class PlayerSessionManager : MonoBehaviour
{
    public static PlayerSessionManager Instance;

    public List<PlayerController.ControlType> activePlayers =
        new List<PlayerController.ControlType>();

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
}