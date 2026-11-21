public sealed class ETFPurchaseRecord
{
    public string Symbol { get; init; } = string.Empty;

    public decimal Ltp { get; init; }

    public int UnitsPurchased { get; init; }
}