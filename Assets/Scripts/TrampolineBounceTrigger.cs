using UnityEngine;

public class TrampolineBounceTrigger : MonoBehaviour
{
    [HideInInspector]
    public Trampoline owner;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null)
        {
            owner.NotifyBounceZone(other);
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (owner != null)
        {
            owner.NotifyBounceZone(other);
        }
    }
}
