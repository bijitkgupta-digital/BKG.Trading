namespace BKG.Trading.Applications.Models;

public sealed class StockPurchaseResult
{
    public DateTime ExecutedAt { get; init; }

    public IReadOnlyCollection<StockPurchaseRecord> Purchases { get; init; }
        = Array.Empty<StockPurchaseRecord>();

    public bool AnyPurchase => Purchases.Count > 0;
}