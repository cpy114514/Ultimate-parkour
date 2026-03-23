public class KeyPickup : CarryPickupBase
{
    protected override bool CanCollect(PlayerController.ControlType player)
    {
        return RoundManager.Instance != null && RoundManager.Instance.CanCollectKey(player);
    }

    protected override void OnCollected(PlayerController.ControlType player)
    {
    }

    public void ConsumeAtChest()
    {
        ConsumeHeld();
    }
}
