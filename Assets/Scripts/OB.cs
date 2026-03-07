using UnityEngine;

public class KillBlock : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            Destroy(player.gameObject);
        }
    }
}