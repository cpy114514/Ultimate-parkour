using UnityEngine;

public class RotatingSawTrap : MonoBehaviour
{
    public Transform rotatingArmRoot;
    public Transform sawBladeVisual;
    public float armRotationSpeed = 90f;
    public bool rotateClockwise = true;
    public float bladeSpinSpeed = 720f;
    public bool bladeSpinClockwise = false;
    public bool affectBeetles = true;

    void Update()
    {
        if (!IsTrapActive())
        {
            return;
        }

        float armStep = (rotateClockwise ? -1f : 1f) * armRotationSpeed * Time.deltaTime;
        if (rotatingArmRoot != null && Mathf.Abs(armStep) > 0.0001f)
        {
            rotatingArmRoot.Rotate(0f, 0f, armStep, Space.Self);
        }

        float bladeStep =
            (bladeSpinClockwise ? -1f : 1f) * bladeSpinSpeed * Time.deltaTime;
        if (sawBladeVisual != null && Mathf.Abs(bladeStep) > 0.0001f)
        {
            sawBladeVisual.Rotate(0f, 0f, bladeStep, Space.Self);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    void TryDamage(Collider2D other)
    {
        if (!IsTrapActive() || other == null || other.transform.IsChildOf(transform))
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            if (RoundManager.Instance != null &&
                RoundManager.Instance.IsPlayerResolved(player.controlType))
            {
                return;
            }

            RoundManager.Instance?.PlayerDied(player.controlType);
            Destroy(player.gameObject);
            return;
        }

        if (!affectBeetles)
        {
            return;
        }

        BlueBeetleEnemy beetle = other.GetComponentInParent<BlueBeetleEnemy>();
        if (beetle != null)
        {
            beetle.HitByHazard();
        }
    }

    bool IsTrapActive()
    {
        return BuildPhaseManager.Instance == null || BuildPhaseManager.Instance.IsRaceActive;
    }
}
