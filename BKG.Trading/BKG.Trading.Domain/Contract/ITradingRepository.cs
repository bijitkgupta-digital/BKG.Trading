using BKG.Trading.Domain.Entities;

namespace BKG.Trading.Domain.Contract;

public interface ITradingRepository
{
    Task<IEnumerable<ETF>> GetETFWatchlistAsync();
    Task UpdateRecentPeakPriceAsync(string symbol, decimal ltp);
}
