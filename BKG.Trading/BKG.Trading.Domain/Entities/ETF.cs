namespace BKG.Trading.Domain.Entities;

public sealed class ETF
{
    /// <summary>
    /// ETF ticker such as MAFANG, MON100, MONQ50.
    /// Primary Key.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Most recent peak tracked by the strategy.
    /// This is not necessarily the all-time high.
    /// </summary>
    public decimal RecentPeakPrice { get; set; }
}