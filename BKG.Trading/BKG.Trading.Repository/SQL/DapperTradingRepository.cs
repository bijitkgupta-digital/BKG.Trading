using BKG.Trading.Domain.Contract;
using BKG.Trading.Domain.Entities;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BKG.Trading.Repository.SQL;

// Using modern C# 14 Primary Constructor to directly inject the connection string
public class DapperTradingRepository(string connectionString) : ITradingRepository
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<IEnumerable<ETF>> GetETFWatchlistAsync()
    {
        const string sql = @"SELECT Symbol, RecentPeakPrice FROM dbo.ETF;";

        using var db = CreateConnection();

        return await db.QueryAsync<ETF>(sql);
    }

    public async Task UpdateRecentPeakPriceAsync(string symbol, decimal recentPeakPrice)
    {
        const string sql = @"
        UPDATE dbo.ETF
        SET RecentPeakPrice = @RecentPeakPrice
        WHERE Symbol = @Symbol";

        using var db = CreateConnection();

        await db.ExecuteAsync(
            sql,
            new
            {
                Symbol = symbol,
                RecentPeakPrice = recentPeakPrice
            });
    }
}
