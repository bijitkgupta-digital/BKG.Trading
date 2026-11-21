
namespace BKG.Trading.Domain.Contract
{
    public interface IBroker
    {
        Task<decimal> GetAvailableMarginAsync();
        Task<IEnumerable<string>> GetExistingHoldings();
        Task PlaceLimitBuyOrderAsync(string symbol, int unitsToBuy, decimal triggerPrice);
        Task PlaceMarketBuyOrderAsync(string symbol, int unitsToBuy);
    }
}
