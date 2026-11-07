namespace BKG.Trading.Domain.Entities;

public class Instrument
{
    /// <summary>
    /// The unique market ticker symbol prefixed with its exchange (e.g., "NSE:MAFANG").
    /// Primary key matching dbo.Instruments.
    /// </summary>
    public string SYMBOL { get; set; } = string.Empty;

    /// <summary>
    /// The pre-defined gross profit threshold percentage for the asset.
    /// Maps to 0.0470 (4.7%) for domestic/commodities or 0.0640 (6.4%) for high-beta US Tech.
    /// </summary>
    public decimal SellTarget { get; set; }
}
