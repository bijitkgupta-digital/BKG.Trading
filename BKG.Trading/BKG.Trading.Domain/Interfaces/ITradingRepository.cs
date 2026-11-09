using BKG.Trading.Domain.Entities;

namespace BKG.Trading.Domain.Interfaces;

public interface ITradingRepository
{
    /// <summary>
    /// Fetches the master list of tracked ETFs and their respective profit targets.
    /// </summary>
    IEnumerable<Instrument> GetTrackedInstruments();

    /// <summary>
    /// Performs a high-performance bulk upsert of daily candlestick records.
    /// </summary>
    void BulkUpsertCandles(IEnumerable<OHLCVRecord> records);

    /// <summary>
    /// Retrieves a chronological historical horizon of records to eliminate indicator warm-up errors.
    /// </summary>
    IEnumerable<OHLCVRecord> GetHistoricalRecords(string symbol, int lookbackDays);

    /// <summary>
    /// Updates the calculated RSI and triggers database-level set operations for Volume and Gap averages.
    /// </summary>
    void UpdateIndicators(string symbol, DateTime date, decimal rsi, decimal dma20, decimal nextLowPrediction);

    /// <summary>
    /// Streams the finalized top 5 momentum reversal signals from the database production view.
    /// </summary>
    IEnumerable<TradingSignal> GetTop5DailySignals();
}
