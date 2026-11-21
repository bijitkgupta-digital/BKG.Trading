using BKG.Trading.Applications.Contracts;
using BKG.Trading.Domain.Contract;

public sealed class ETFPurchaseService : IETFPurchaseService
{
    private readonly ITradingRepository _repository;
    private readonly ILTPProvider _ltpProvider;
    private readonly IBroker _broker;

    public ETFPurchaseService(ITradingRepository repository, ILTPProvider ltpProvider, IBroker broker)
    {
        _repository = repository;
        _ltpProvider = ltpProvider;
        _broker = broker;
    }

    public async Task<ETFPurchaseResult> ExecuteAsync()
    {
        var purchases = new List<ETFPurchaseRecord>();

        decimal availableMargin = await _broker.GetAvailableMarginAsync();

        if (availableMargin <= 100m)
        {
            return CreateResult(purchases);
        }

        var watchlist = (await _repository.GetETFWatchlistAsync()).ToList();

        var prices = await _ltpProvider.FetchLTPAsync(watchlist.Select(x => x.Symbol));

        foreach (var etf in watchlist)
        {
            if (!prices.TryGetValue(etf.Symbol, out decimal ltp))
                continue;

            if (ltp > etf.RecentPeakPrice)
            {
                await _repository.UpdateRecentPeakPriceAsync(etf.Symbol, ltp);

                continue;
            }

            int unitsToBuy = CalculateUnitsToPurchase(ltp, etf.RecentPeakPrice);

            if (unitsToBuy <= 0)
                continue;

            await _broker.PlaceMarketBuyOrderAsync(etf.Symbol, unitsToBuy);

            purchases.Add(new ETFPurchaseRecord
            {
                Symbol = etf.Symbol,
                Ltp = ltp,
                UnitsPurchased = unitsToBuy
            });
        }

        return CreateResult(purchases);
    }

    private static int CalculateUnitsToPurchase(decimal ltp, decimal? recentPeakPrice)
    {
        if (!recentPeakPrice.HasValue || recentPeakPrice.Value <= 0)
        {
            return 1;
        }

        decimal dropPercentage = ((recentPeakPrice.Value - ltp) / recentPeakPrice.Value) * 100m;

        int integerDropPercentage = (int)dropPercentage;

        return (int)Math.Pow(2, integerDropPercentage);
    }

    private static ETFPurchaseResult CreateResult(IReadOnlyCollection<ETFPurchaseRecord> purchases)
    {
        return new ETFPurchaseResult
        {
            ExecutedAt = DateTime.UtcNow,
            Purchases = purchases
        };
    }
}