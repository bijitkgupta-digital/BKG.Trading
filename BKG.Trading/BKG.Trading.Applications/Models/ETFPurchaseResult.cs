public sealed class ETFPurchaseResult
{
    public DateTime ExecutedAt { get; init; }

    public IReadOnlyCollection<ETFPurchaseRecord> Purchases { get; init; }
        = Array.Empty<ETFPurchaseRecord>();

    public bool AnyPurchase => Purchases.Count > 0;
}