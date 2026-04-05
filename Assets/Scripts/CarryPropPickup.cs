public class CarryPropPickup : CarryPickupBase
{
    protected override bool CanCollect(PlayerController.ControlType player)
    {
        return RoundManager.Instance == null || RoundManager.Instance.CanCollectCoin(player);
    }

    protected override void OnCollected(PlayerController.ControlType player)
    {
    }
}
