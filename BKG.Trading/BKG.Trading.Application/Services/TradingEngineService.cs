using System;
using System.Collections.Generic;
using System.Linq;
using BKG.Trading.Domain.Entities;
using BKG.Trading.Domain.Interfaces;

namespace BKG.Trading.Application.Services;

public class TradingEngineService(ITradingRepository repository, IMarketDataService marketDataService)
{
    private readonly ITradingRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly IMarketDataService _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));

    // =========================================================================
    // PIPELINE STAGE 1: MARKET DATA MIRROR SYNCHRONIZATION
    // =========================================================================
    /// <summary>
    /// Connects to the external market feed adapter to fetch and store a specific day's candles.
    /// Automatically detects an empty database and pulls a 100-day historical window to prime the system.
    /// Expose this via a POST /api/etf/sync endpoint.
    /// </summary>
    public int SyncCandleData(DateTime targetDate)
    {
        var instruments = _repository.GetTrackedInstruments().ToList();
        int successfullySyncedCount = 0;

        foreach (var inst in instruments)
        {
            // Check if this specific asset has any existing history rows inside the database
            var existingDbRecords = _repository.GetHistoricalRecords(inst.SYMBOL, 5).ToList();

            IEnumerable<OHLCVRecord> incomingCandles;

            if (!existingDbRecords.Any())
            {
                // SCENARIO A: Database is fresh/empty -> Trigger the self-contained 100-day lookback API back-fill
                Console.WriteLine($"📥 [INITIAL LOAD DETECTED] Priming 100-day data ledger for {inst.SYMBOL}...");
                incomingCandles = _marketDataService.FetchHistoricalWindow(inst.SYMBOL, 100);
            }
            else
            {
                // SCENARIO B: Normal Operational State -> Pull only today's single data row
                Console.WriteLine($"📥 Fetching daily EOD candle for {inst.SYMBOL}...");
                incomingCandles = _marketDataService.FetchEodCandles(inst.SYMBOL, targetDate, targetDate);
            }

            if (incomingCandles.Any())
            {
                _repository.BulkUpsertCandles(incomingCandles);
                successfullySyncedCount++;
            }
        }

        return successfullySyncedCount;
    }

    // =========================================================================
    // PIPELINE STAGE 2: MATHEMATICAL MODEL COMPUTATION
    // =========================================================================
    /// <summary>
    /// Processes rolling price memory context up to today's session, computes 
    /// the singular target date's indicators, and commits them cleanly to the database.
    /// Expose this via a POST /api/etf/calculate endpoint.
    /// </summary>
    public void CalculateIndicators(DateTime targetDate)
    {
        var instruments = _repository.GetTrackedInstruments().ToList();

        foreach (var inst in instruments)
        {
            // Fetch the last 100 days of price records out of SQL Server to ensure 
            // sufficient chronological history context for indicators and smoothing
            var priceContext = _repository.GetHistoricalRecords(inst.SYMBOL, 100)
                                         .Where(p => p.Date <= targetDate) // Safe gate: ensure lookback cuts off at target date
                                         .OrderBy(p => p.Date)
                                         .ToList();

            if (priceContext.Count < 20) // Guard clause: DMA20 requires at least 20 historical sessions
            {
                Console.WriteLine($"⚠️ Insufficient data context history in DB for {inst.SYMBOL} up to {targetDate:yyyy-MM-dd}. Skipping indicator calculations.");
                continue;
            }

            // STANDARD SINGLE-DAY OPERATION: Compute indicators strictly for the active target date
            decimal computedRsi = CalculateWildersRsi(priceContext, 14);
            decimal computedDMA20 = CalculateDMA(priceContext, 20);
            decimal nextLowPrediction = CalculateNextLowPrediction(priceContext); // Strategy specification parameters: 14 periods

            // Persist the single day's calculated data row back to the repository ledger
            _repository.UpdateIndicators(inst.SYMBOL, targetDate, computedRsi, computedDMA20, nextLowPrediction);
        }
    }


    // =========================================================================
    // PIPELINE STAGE 3: BLUEPRINT SIGNAL STREAMING
    // =========================================================================
    /// <summary>
    /// Extracts the finalized next-day active trading signals natively out of the database production view.
    /// Expose this via a GET /api/etf/signals endpoint.
    /// </summary>
    public IEnumerable<TradingSignal> GetTop5DailySignals()
    {
        return _repository.GetTop5DailySignals();
    }

    // =========================================================================
    // COGNITIVE COMPUTE NODE: WILDER'S RSI EXPONENTIAL SMOOTHING MULTIPLIER
    // =========================================================================
    private static decimal CalculateWildersRsi(List<OHLCVRecord> records, int period = 14)
    {
        var orderedCloses = records.OrderBy(r => r.Date).Select(r => r.Close).ToList();
        if (orderedCloses.Count <= period) return 50.0m;

        decimal avgGain = 0;
        decimal avgLoss = 0;

        for (int i = 1; i <= period; i++)
        {
            decimal difference = orderedCloses[i] - orderedCloses[i - 1];
            if (difference > 0) avgGain += difference;
            else avgLoss += Math.Abs(difference);
        }

        avgGain /= period;
        avgLoss /= period;

        for (int i = period + 1; i < orderedCloses.Count; i++)
        {
            decimal difference = orderedCloses[i] - orderedCloses[i - 1];
            decimal currentGain = difference > 0 ? difference : 0;
            decimal currentLoss = difference < 0 ? Math.Abs(difference) : 0;

            avgGain = ((avgGain * (period - 1)) + currentGain) / period;
            avgLoss = ((avgLoss * (period - 1)) + currentLoss) / period;
        }

        if (avgLoss == 0) return 100.0m;

        decimal rs = avgGain / avgLoss;
        return Math.Round(100.0m - (100.0m / (1.0m + rs)), 2);
    }

    // =========================================================================
    // CALCULATE VOLUME DMA (Simple Moving Average of Volume)
    // =========================================================================
    private static decimal CalculateDMA(List<OHLCVRecord> records, int period)
    {
        var orderedRecords = records.OrderBy(r => r.Date).ToList();

        if (orderedRecords.Count < period)
            return 0.0m;

        long volumeSum = 0;
        int startIndex = orderedRecords.Count - period;

        for (int i = startIndex; i < orderedRecords.Count; i++)
        {
            volumeSum += orderedRecords[i].Volume;
        }

        return (decimal)volumeSum / period;
    }

    /// <summary>
    /// Calculates the static next-day Low target (Limit Buy Price) by explicitly finding 
    /// the most recent date record, entirely independent of list order.
    /// </summary>
    public decimal CalculateNextLowPrediction(List<OHLCVRecord> records)
    {
        if (records == null || !records.Any())
        {
            throw new ArgumentException("Historical records cannot be empty.");
        }

        // Explicitly locate the single record with the maximum (most recent) date
        var latestDay = records.OrderByDescending(r => r.Date).First();

        decimal H = latestDay.High;
        decimal L = latestDay.Low;
        decimal C = latestDay.Close;

        // 1. Calculate the core Pivot Point (P)
        decimal pivot = (H + L + C) / 3m;

        // 2. Calculate Support 1 (S1) 
        decimal predictedS1Low = (2m * pivot) - H;

        // 3. Indian Exchange Tick Size Alignment (Round to nearest 0.05 paise)
        return RoundToTickSize((latestDay.Open - latestDay.Low)/3, 0.05m);
    }

    private decimal RoundToTickSize(decimal value, decimal tickSize)
    {
        return Math.Round(value / tickSize) * tickSize;
    }
}
