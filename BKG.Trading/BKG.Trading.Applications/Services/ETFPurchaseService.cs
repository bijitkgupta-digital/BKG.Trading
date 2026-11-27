using BKG.Trading.Applications.Contracts;
using BKG.Trading.Domain.Contract;
using Microsoft.Extensions.Logging;

public sealed class ETFPurchaseService : IETFPurchaseService
{
    private readonly ITradingRepository _repository;
    private readonly ILTPProvider _ltpProvider;
    private readonly IBroker _broker;
    private readonly ILogger<ETFPurchaseService> _logger;

    public ETFPurchaseService(ITradingRepository repository, ILTPProvider ltpProvider, IBroker broker, ILogger<ETFPurchaseService> logger)
    {
        _repository = repository;
        _ltpProvider = ltpProvider;
        _broker = broker;
        _logger = logger;
    }

    public async Task<ETFPurchaseResult> ExecuteAsync()
    {
        var purchases = new List<ETFPurchaseRecord>();

        _logger.LogInformation("ETF averaging execution started.");

        decimal availableMargin = await _broker.GetAvailableMarginAsync();

        _logger.LogInformation("Step=MarginCheck AvailableMargin={AvailableMargin}", availableMargin);

        if (availableMargin <= 100m)
        {
            _logger.LogWarning("ETF averaging skipped due to insufficient margin. AvailableMargin={AvailableMargin}", availableMargin);
            return CreateResult(purchases);
        }

        var watchlist = (await _repository.GetETFWatchlistAsync()).ToList();
        _logger.LogInformation("Step=WatchlistLoad WatchlistCount={WatchlistCount}", watchlist.Count);

        var prices = await _ltpProvider.FetchLTPAsync(watchlist.Select(x => x.Symbol));
        _logger.LogInformation("Step=LtpFetch PriceCount={PriceCount}", prices.Count);

        foreach (var etf in watchlist)
        {
            if (!prices.TryGetValue(etf.Symbol, out decimal ltp))
                continue;

            if (ltp > etf.RecentPeakPrice)
            {
                _logger.LogInformation("Updating ETF peak price. Symbol={Symbol}, PreviousPeak={PreviousPeak}, NewPeak={NewPeak}", etf.Symbol, etf.RecentPeakPrice, ltp);
                await _repository.UpdateRecentPeakPriceAsync(etf.Symbol, ltp);

                continue;
            }

            int unitsToBuy = CalculateUnitsToPurchase(ltp, etf.RecentPeakPrice);

            if (unitsToBuy <= 0)
                continue;

            _logger.LogInformation("Placing ETF market buy order. Symbol={Symbol}, Units={Units}, Ltp={Ltp}", etf.Symbol, unitsToBuy, ltp);

            await _broker.PlaceMarketBuyOrderAsync(etf.Symbol, unitsToBuy);

            purchases.Add(new ETFPurchaseRecord
            {
                Symbol = etf.Symbol,
                Ltp = ltp,
                UnitsPurchased = unitsToBuy
            });
        }

        _logger.LogInformation("ETF averaging execution completed. Purchases={PurchaseCount}", purchases.Count);

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