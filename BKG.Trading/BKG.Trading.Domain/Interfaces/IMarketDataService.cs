using System;
using System.Collections.Generic;
using BKG.Trading.Domain.Entities;

namespace BKG.Trading.Domain.Interfaces;

public interface IMarketDataService
{
    /// <summary>
    /// Fetches historical EOD candlestick bars for a given symbol from the external API feed.
    /// </summary>
    /// <param name="symbol">The master ticker symbol (e.g., "NSE:NIFTYBEES").</param>
    /// <param name="startDate">The start date of the requested historical window.</param>
    /// <param name="endDate">The end date of the requested historical window.</param>
    /// <returns>A collection of clean, domain-mapped OHLCV records.</returns>
    IEnumerable<OHLCVRecord> FetchEodCandles(string symbol, DateTime startDate, DateTime endDate);
    IEnumerable<OHLCVRecord> FetchHistoricalWindow(string symbol, int lookbackDays);
}
