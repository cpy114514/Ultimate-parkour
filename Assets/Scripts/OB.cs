using UnityEngine;

public class KillBlock : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        BlueBeetleEnemy beetle = other.GetComponentInParent<BlueBeetleEnemy>();
        FlyingPatrolEnemy flyingEnemy = other.GetComponentInParent<FlyingPatrolEnemy>();

        if (player != null)
        {
            if (StoryModeManager.TryApplyDamage(player, StoryModeManager.DamageAmount.FullHeart))
            {
                return;
            }

            if (RoundManager.Instance != null &&
                RoundManager.Instance.IsPlayerResolved(player.controlType))
            {
                return;
            }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.PlayerDied(player.controlType);
            }

            Destroy(player.gameObject);
        }

        if (beetle != null)
        {
            beetle.HitByHazard();
        }

        if (flyingEnemy != null)
        {
            flyingEnemy.HitByHazard();
        }
    }
}
