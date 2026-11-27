using BKG.Trading.Applications.Models;
using BKG.Trading.Domain.Constants;
using BKG.Trading.Domain.Contract;
using Microsoft.Extensions.Logging;

namespace BKG.Trading.Applications.StockSwing;

public sealed class StockPurchaseService
{
    private readonly IScreener _screener;
    private readonly ILTPProvider _ltpProvider;
    private readonly IBroker _broker;
    private readonly ILogger<StockPurchaseService> _logger;
    private readonly decimal _minimumFundAllotment;
    private readonly int _maximumStockPoolSize;
    private readonly string _marketOrderType;

    public StockPurchaseService(
        IScreener screener,
        ILTPProvider ltpProvider,
        IBroker broker,
        ILogger<StockPurchaseService> logger,
        decimal minimumFundAllotment,
        int maximumStockPoolSize,
        string marketOrderType)
    {
        _screener = screener;
        _ltpProvider = ltpProvider;
        _broker = broker;
        _logger = logger;

        _minimumFundAllotment = minimumFundAllotment > 0 ? minimumFundAllotment : TradingConstants.Defaults.MinimumFundAllotment;
        _maximumStockPoolSize = maximumStockPoolSize > 0 ? maximumStockPoolSize : TradingConstants.Defaults.MaximumStockPoolSize;
        _marketOrderType = string.IsNullOrWhiteSpace(marketOrderType) ? TradingConstants.Defaults.MarketOrderType : marketOrderType.Trim();
    }

    public async Task<StockPurchaseResult> ExecuteAsync()
    {
        var purchases = new List<StockPurchaseRecord>();
        decimal availableMargin = await _broker.GetAvailableMarginAsync();
        if (availableMargin < _minimumFundAllotment)
        {
            _logger.LogWarning(
                "Skipping stock swing due to insufficient margin. AvailableMargin={AvailableMargin}, MinimumRequired={MinimumRequired}",
                availableMargin,
                _minimumFundAllotment);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("Insufficient margin available.");
            Console.WriteLine("Skipping swing stock purchase process.");
            Console.ResetColor();

            return CreateResult(purchases);
        }

        HashSet<string> existingHoldings = (await _broker.GetExistingHoldings()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int maximumStocksToPurchase = Math.Min((int)(availableMargin / _minimumFundAllotment), (_maximumStockPoolSize - existingHoldings.Count));

        IEnumerable<string> selectedStocks = (await _screener.GetSwingStockListAsync())
            .Where(x => !existingHoldings.Contains(x.Key)).OrderBy(x => x.Value).Take(maximumStocksToPurchase).Select(x => x.Key);

        if (!selectedStocks.Any())
        {
            _logger.LogWarning("No eligible stocks available after excluding existing holdings.");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("No eligible stocks available after holding exclusion.");
            Console.ResetColor();

            return CreateResult(purchases);
        }

        decimal fundAllotmentPerStock = availableMargin / selectedStocks.Count();
        IDictionary<string, decimal> stockPrices = await _ltpProvider.FetchLTPAsync(selectedStocks);


        IDictionary<string, int> stockUnits = stockPrices
            .Where(x => x.Value > 0)
            .ToDictionary(x => x.Key, x => Math.Max(1, (int)(fundAllotmentPerStock / x.Value)));

        var orderPreview = stockUnits
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                Symbol = x.Key,
                Ltp = stockPrices[x.Key],
                Units = x.Value,
                CapitalAllocation = stockPrices[x.Key] * x.Value
            });

        decimal totalCapitalAllocation = orderPreview.Sum(x => x.CapitalAllocation);

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("ORDER PREVIEW");
        Console.WriteLine("========================================================");
        Console.WriteLine($"{"SYMBOL",-15} {"LTP",12} {"UNITS",10} {"CAPITAL ALLOCATION",20}");

        foreach (var stock in orderPreview)
        {
            Console.WriteLine(
                $"{stock.Symbol,-15} " +
                $"{stock.Ltp,12:N2} " +
                $"{stock.Units,10} " +
                $"{stock.CapitalAllocation,20:N2}");
        }

        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine($"Total Capital Allocation : ₹{totalCapitalAllocation:N2}");
        Console.WriteLine($"Total Available Capital  : ₹{availableMargin:N2}");
        Console.WriteLine("========================================================");
        Console.WriteLine($"Press ENTER to place {_marketOrderType.ToUpperInvariant()} orders...");
        Console.ReadLine();

        _logger.LogInformation("Step=OrderSubmissionStarting OrderCount={OrderCount}", stockUnits.Count);

        foreach (var stock in stockUnits.Where(x => x.Value > 0))
        {
            _logger.LogInformation("Submitting market buy order. Symbol={Symbol}, Quantity={Quantity}", stock.Key, stock.Value);

            await _broker.PlaceMarketBuyOrderAsync(stock.Key, stock.Value);
            await Task.Delay(TradingConstants.Defaults.BuyOrderPlacementDelay);

            purchases.Add(new StockPurchaseRecord
            {
                Symbol = stock.Key,
                Ltp = stockPrices[stock.Key],
                UnitsPurchased = stock.Value
            });
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("ORDERS SUBMITTED SUCCESSFULLY");
        Console.WriteLine("========================================================");
        Console.WriteLine($"Orders Placed : {purchases.Count}");
        Console.ResetColor();

        _logger.LogInformation("Stock swing execution finished. OrdersPlaced={OrdersPlaced}", purchases.Count);

        return CreateResult(purchases);
    }

    private static StockPurchaseResult CreateResult(IReadOnlyCollection<StockPurchaseRecord> purchases)
    {
        return new StockPurchaseResult
        {
            ExecutedAt = DateTime.UtcNow,
            Purchases = purchases
        };
    }
}