using UnityEngine;

public class TouchHideTileTrigger : MonoBehaviour
{
    [HideInInspector]
    public TouchHideTile owner;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null)
        {
            owner.NotifyTriggerEnter(other);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (owner != null)
        {
            owner.NotifyTriggerExit(other);
        }
    }
}
