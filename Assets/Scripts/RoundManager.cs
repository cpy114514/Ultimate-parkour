using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance;

    public float nextRoundDelay = 3f;
    public float finishScore = 1f;
    public float firstFinishBonus = 0.75f;

    bool roundEnding;

    readonly HashSet<PlayerController.ControlType> finishedPlayers =
        new HashSet<PlayerController.ControlType>();

    readonly HashSet<PlayerController.ControlType> deadPlayers =
        new HashSet<PlayerController.ControlType>();

    readonly List<PlayerController.ControlType> finishOrder =
        new List<PlayerController.ControlType>();

    readonly Dictionary<PlayerController.ControlType, float> bankedBonusScores =
        new Dictionary<PlayerController.ControlType, float>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
        ResetRoundState();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Time.timeScale = 1f;
            Instance = null;
        }
    }

    public void PlayerReachedFlag(PlayerController player, FinishFlag finishFlag)
    {
        if (roundEnding || player == null)
        {
            return;
        }

        PlayerController.ControlType controlType = player.controlType;

        if (finishedPlayers.Contains(controlType) || deadPlayers.Contains(controlType))
        {
            return;
        }

        finishedPlayers.Add(controlType);
        finishOrder.Add(controlType);
        player.SetControlEnabled(false);

        if (finishFlag != null)
        {
            finishFlag.MovePlayerToWaitingArea(player, finishOrder.Count - 1);
        }

        BankHeldBonusScores(controlType);
        ConsumeHeldCoins(controlType);
        TryFinishRound();
    }

    public void PlayerDied(PlayerController.ControlType player)
    {
        if (roundEnding || GameManager.Instance == null ||
            finishedPlayers.Contains(player) || deadPlayers.Contains(player))
        {
            return;
        }

        deadPlayers.Add(player);
        ClearHeldCoins(player);
        GameManager.Instance.MarkPlayerDead(player);
        TryFinishRound();
    }

    public bool IsPlayerResolved(PlayerController.ControlType player)
    {
        return finishedPlayers.Contains(player) || deadPlayers.Contains(player);
    }

    public bool CanCollectCoin(PlayerController.ControlType player)
    {
        return CanCollectPickup(player);
    }

    public bool CanCollectDiamond(PlayerController.ControlType player)
    {
        return CanCollectPickup(player);
    }

    public bool CanCollectKey(PlayerController.ControlType player)
    {
        return CanCollectPickup(player);
    }

    public void PlayerCollectedCoin(PlayerController.ControlType player)
    {
    }

    public void PlayerCollectedDiamond(PlayerController.ControlType player)
    {
    }

    bool CanCollectPickup(PlayerController.ControlType player)
    {
        return !roundEnding &&
               !finishedPlayers.Contains(player) &&
               !deadPlayers.Contains(player);
    }

    void TryFinishRound()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        int resolvedPlayers = finishedPlayers.Count + deadPlayers.Count;

        if (resolvedPlayers < GameManager.Instance.GetSessionPlayerCount())
        {
            return;
        }

        FinishRound();
    }

    void FinishRound()
    {
        roundEnding = true;

        bool noWinner = finishOrder.Count == 0;
        bool matchWon = false;
        PlayerController.ControlType? scoreboardWinner = noWinner
            ? null
            : finishOrder[0];

        if (!noWinner && ScoreManager.Instance != null)
        {
            AwardRoundPoints();

            if (ScoreManager.Instance.TryGetMatchWinner(GetScorePriorityOrder(), out var matchWinner))
            {
                scoreboardWinner = matchWinner;
                matchWon = true;
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetAllPlayerControl(false);
        }

        Time.timeScale = 0f;

        ScoreboardUI board = FindObjectOfType<ScoreboardUI>();
        if (board != null)
        {
            if (noWinner)
            {
                board.ShowNoWinnerResults();
            }
            else
            {
                board.ShowRoundResults(scoreboardWinner, matchWon);
            }
        }

        StartCoroutine(NextRound(matchWon));
    }

    IEnumerator NextRound(bool matchWon)
    {
        yield return new WaitForSecondsRealtime(nextRoundDelay);

        ScoreboardUI board = FindObjectOfType<ScoreboardUI>();
        if (board != null)
        {
            board.Hide();
        }

        if (matchWon)
        {
            ScoreManager.ResetScores();
        }

        Time.timeScale = 1f;
        ResetRoundState();
        ResetScenePickups(matchWon);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetRound();
        }

        if (BuildPhaseManager.Instance != null)
        {
            BuildPhaseManager.Instance.BeginRoundSetup(matchWon);
        }
    }

    void AwardRoundPoints()
    {
        for (int i = 0; i < finishOrder.Count; i++)
        {
            PlayerController.ControlType player = finishOrder[i];
            float awardedScore = finishScore + GetCollectedBonus(player);

            if (i == 0)
            {
                awardedScore += firstFinishBonus;
            }

            ScoreManager.Instance.AddScore(player, awardedScore);
        }
    }

    float GetCollectedBonus(PlayerController.ControlType player)
    {
        if (!bankedBonusScores.TryGetValue(player, out float bonus))
        {
            return 0f;
        }

        return bonus;
    }

    List<PlayerController.ControlType> GetScorePriorityOrder()
    {
        List<PlayerController.ControlType> priorityOrder =
            new List<PlayerController.ControlType>(finishOrder);

        if (GameManager.Instance != null)
        {
            foreach (PlayerController.ControlType player in GameManager.Instance.GetSessionPlayers())
            {
                if (!priorityOrder.Contains(player))
                {
                    priorityOrder.Add(player);
                }
            }
        }

        return priorityOrder;
    }

    void ResetRoundState()
    {
        roundEnding = false;
        finishedPlayers.Clear();
        deadPlayers.Clear();
        finishOrder.Clear();
        bankedBonusScores.Clear();

        foreach (PlayerController.ControlType type in
                 System.Enum.GetValues(typeof(PlayerController.ControlType)))
        {
            bankedBonusScores[type] = 0f;
        }
    }

    void ResetScenePickups(bool forceFullReset)
    {
        LockedChest[] chests = FindObjectsOfType<LockedChest>(true);
        foreach (LockedChest chest in chests)
        {
            chest.ResetChest(forceFullReset);
        }

        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);
        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup != null)
            {
                pickup.ResetPickup(forceFullReset);
            }
        }
    }

    void BankHeldBonusScores(PlayerController.ControlType player)
    {
        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);

        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup == null || !pickup.IsHeldBy(player) || pickup.BonusValue <= 0f)
            {
                continue;
            }

            bankedBonusScores[player] += pickup.BonusValue;
        }
    }

    void ConsumeHeldCoins(PlayerController.ControlType player)
    {
        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);

        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup != null && pickup.ConsumeOnFinish && pickup.IsHeldBy(player))
            {
                pickup.ConsumeHeld();
            }
        }
    }

    void ClearHeldCoins(PlayerController.ControlType player)
    {
        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);

        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup != null)
            {
                pickup.ClearHeldState(player);
            }
        }
    }
}
