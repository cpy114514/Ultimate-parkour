public class CoinPickup : CarryPickupBase
{
    public override bool ConsumeOnFinish
    {
        get { return true; }
    }

    public override float BonusValue
    {
        get { return 0.5f; }
    }

    protected override bool CanCollect(PlayerController.ControlType player)
    {
        if (StoryModeManager.Instance != null)
        {
            return StoryModeManager.Instance.CanCollectCoin(player);
        }

        return RoundManager.Instance != null && RoundManager.Instance.CanCollectCoin(player);
    }

    protected override void OnCollected(PlayerController.ControlType player)
    {
        if (StoryModeManager.Instance != null &&
            StoryModeManager.Instance.TryCollectCoin(player))
        {
            ConsumeHeld(true);
        }
    }

    public void ConsumeAtFinish()
    {
        ConsumeHeld();
    }
}
