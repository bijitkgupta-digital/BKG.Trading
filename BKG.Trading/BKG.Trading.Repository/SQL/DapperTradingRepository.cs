using BKG.Trading.Domain.Entities;
using BKG.Trading.Domain.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.SqlClient;


namespace BKG.Trading.Repository.SQL;

// Using modern C# 14 Primary Constructor to directly inject the connection string
public class DapperTradingRepository(string connectionString) : ITradingRepository
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    // =========================================================================
    // OPERATION 1: Fetch Master Ticker Rules
    // =========================================================================
    public IEnumerable<Instrument> GetTrackedInstruments()
    {
        const string sql = "SELECT SYMBOL, SellTarget FROM dbo.Instruments;";
        using var db = CreateConnection();
        return db.Query<Instrument>(sql);
    }

    // =========================================================================
    // OPERATION 2: High-Performance Parameterized Bulk Upsert
    // =========================================================================
    public void BulkUpsertCandles(IEnumerable<OHLCVRecord> records)
    {
        // Protected database keyword [Close] via explicit square brackets
        const string mergeSql = """
            MERGE INTO dbo.OHLCV_Data AS Target
            USING (VALUES (@SYMBOL, @Date, @Open, @High, @Low, @Close, @Volume)) 
                   AS Source (SYMBOL, Date, [Open], High, Low, [Close], Volume)
            ON Target.SYMBOL = Source.SYMBOL AND Target.Date = Source.Date
            WHEN MATCHED THEN
                UPDATE SET Target.[Open] = Source.[Open], Target.High = Source.High, 
                           Target.Low = Source.Low, Target.[Close] = Source.[Close], Target.Volume = Source.Volume
            WHEN NOT MATCHED THEN
                INSERT (SYMBOL, Date, [Open], High, Low, [Close], Volume)
                VALUES (Source.SYMBOL, Source.Date, Source.[Open], Source.High, Source.Low, Source.[Close], Source.Volume);
            """;

        using var db = CreateConnection();
        db.Open();
        using var transaction = db.BeginTransaction();
        try
        {
            // Dapper automatically unrolls and batches the enumerable collection efficiently
            db.Execute(mergeSql, records, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =========================================================================
    // OPERATION 3: Get Chronological History Context (Eliminate RSI Warm-up Errors)
    // =========================================================================
    public IEnumerable<OHLCVRecord> GetHistoricalRecords(string symbol, int lookbackDays)
    {
        const string sql = """
            SELECT TOP (@lookbackDays) SYMBOL, Date, [Open], High, Low, [Close], Volume 
            FROM dbo.OHLCV_Data 
            WHERE SYMBOL = @symbol 
            ORDER BY Date DESC;
            """;

        using var db = CreateConnection();
        // Retrieve newest items first, then convert to chronological order (oldest to newest)
        var records = db.Query<OHLCVRecord>(sql, new { symbol, lookbackDays }).ToList();
        records.Reverse();
        return records;
    }

    // =========================================================================
    // OPERATION 4: Combined Transactional Indicators & RSI Commits
    // =========================================================================
    public void UpdateIndicators(string symbol, DateTime date, decimal rsi, decimal dma20, decimal nextLowPrediction)
    {
        const string updateRsiSql = """
            UPDATE dbo.OHLCV_Data 
            SET RSI14 = @rsi, Volume_SMA20 = @dma20, OpenMinusLow = @nextLowPrediction
            WHERE SYMBOL = @symbol AND Date = @date;
            """;

        using var db = CreateConnection();
        db.Open();
        using var transaction = db.BeginTransaction();
        try
        {
            db.Execute(updateRsiSql, new { rsi, symbol, date, dma20, nextLowPrediction }, transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // =========================================================================
    // OPERATION 5: Read Strategy Blueprint Output Matrix
    // =========================================================================
    public IEnumerable<TradingSignal> GetTop5DailySignals()
    {
        const string sql = "SELECT * FROM dbo.v_Top5_Momentum_Pivots;";
        using var db = CreateConnection();
        return db.Query<TradingSignal>(sql);
    }
}
