public class DiamondPickup : CarryPickupBase
{
    public override bool ConsumeOnFinish
    {
        get { return true; }
    }

    public override float BonusValue
    {
        get { return 1f; }
    }

    protected override bool CanCollect(PlayerController.ControlType player)
    {
        return RoundManager.Instance != null && RoundManager.Instance.CanCollectDiamond(player);
    }

    protected override void OnCollected(PlayerController.ControlType player)
    {
    }

    public void ConsumeAtFinish()
    {
        ConsumeHeld();
    }

    protected override UnityEngine.Color GetBurstStartColor()
    {
        return new UnityEngine.Color(0.42f, 0.82f, 1f, 1f);
    }

    protected override UnityEngine.Gradient BuildBurstGradient()
    {
        UnityEngine.Gradient gradient = new UnityEngine.Gradient();
        gradient.SetKeys(
            new[]
            {
                new UnityEngine.GradientColorKey(new UnityEngine.Color(0.78f, 0.94f, 1f), 0f),
                new UnityEngine.GradientColorKey(new UnityEngine.Color(0.35f, 0.72f, 1f), 0.6f),
                new UnityEngine.GradientColorKey(new UnityEngine.Color(0.14f, 0.42f, 0.95f), 1f)
            },
            new[]
            {
                new UnityEngine.GradientAlphaKey(1f, 0f),
                new UnityEngine.GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }
}
