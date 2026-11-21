namespace BKG.Trading.Applications.Models;

public sealed class StockPurchaseRecord
{
    public string Symbol { get; init; } = string.Empty;

    public decimal Ltp { get; init; }

    public int UnitsPurchased { get; init; }
}