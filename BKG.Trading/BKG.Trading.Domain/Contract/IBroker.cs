
namespace BKG.Trading.Domain.Contract
{
    public interface IBroker
    {
        Task<decimal> GetAvailableMarginAsync();
        Task<IEnumerable<string>> GetExistingHoldings();
        Task PlaceLimitBuyOrderAsync(string symbol, int unitsToBuy, decimal triggerPrice);
        Task PlaceMarketBuyOrderAsync(string symbol, int unitsToBuy);
        Task<CompletedOrderExecution> PlaceMarketBuyOrderAndWaitForCompletionAsync(
            string symbol,
            int unitsToBuy,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null,
            CancellationToken cancellationToken = default);
        Task<CompletedOrderExecution> PlaceLimitBuyOrderAndWaitForCompletionAsync(
            string symbol,
            int unitsToBuy,
            decimal triggerPrice,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null,
            CancellationToken cancellationToken = default);
        Task<string> PlaceSingleSellGttAfterCompletionAsync(
            string buyOrderId,
            decimal sellLimitPrice,
            decimal? triggerPrice = null,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null,
            CancellationToken cancellationToken = default);
    }
}
