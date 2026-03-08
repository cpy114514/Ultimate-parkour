using UnityEngine;

public class FinishFlag : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            RoundManager.Instance.PlayerWin(player.controlType);
        }
    }
}