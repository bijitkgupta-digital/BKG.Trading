using System;

namespace BKG.Trading.Domain.Entities;

public class OHLCVRecord
{
    /// <summary>
    /// The market ticker symbol with exchange prefix (e.g., "NSE:NIFTYBEES").
    /// </summary>
    public string SYMBOL { get; set; } = string.Empty;

    /// <summary>
    /// The date of the historical trading session session.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// The market opening price of the session.
    /// </summary>
    public decimal Open { get; set; }

    /// <summary>
    /// The maximum traded price reached during the session.
    /// </summary>
    public decimal High { get; set; }

    /// <summary>
    /// The lowest traded price reached during the session.
    /// </summary>
    public decimal Low { get; set; }

    /// <summary>
    /// The final closing price of the session.
    /// </summary>
    public decimal Close { get; set; }

    /// <summary>
    /// The total cumulative traded volume during the session.
    /// </summary>
    public long Volume { get; set; }
}
