using System;

namespace BKG.Trading.Domain.Entities;

public class TradingSignal
{
    /// <summary>
    /// The market ticker symbol with exchange prefix (e.g., "NSE:MAFANG").
    /// </summary>
    public string SYMBOL { get; set; } = string.Empty;

    /// <summary>
    /// The exact evaluation date when the signal pivot was triggered.
    /// </summary>
    public DateTime Signal_Date { get; set; }

    /// <summary>
    /// Today's final EOD closing market price.
    /// </summary>
    public decimal Today_Close { get; set; }

    /// <summary>
    /// Today's calculated Wilder's Exponentially Smoothed RSI (RSI_T).
    /// </summary>
    public decimal RSI_Today { get; set; }

    /// <summary>
    /// Yesterday's calculated Wilder's Exponentially Smoothed RSI (RSI_T-1).
    /// </summary>
    public decimal RSI_Yesterday { get; set; }

    /// <summary>
    /// Today's cumulative institutional volume session print.
    /// </summary>
    public long Volume_Today { get; set; }

    /// <summary>
    /// The 20-day simple moving average of daily volume (Institutional Baseline).
    /// </summary>
    public long Volume_SMA20 { get; set; }

    /// <summary>
    /// The pre-defined profit target threshold (0.0470 for India, 0.0640 for US Tech).
    /// </summary>
    public decimal SellTarget { get; set; }

    /// <summary>
    /// The exact target buy entry limit price calculated based on your 14-day volatility footprint.
    /// Formula: Close - (14-Day Average Gap / 2)
    /// </summary>
    public decimal LimitBuyPrice { get; set; }
}
