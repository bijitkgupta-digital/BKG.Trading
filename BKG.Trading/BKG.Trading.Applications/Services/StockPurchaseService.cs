using BKG.Trading.Applications.Models;
using BKG.Trading.Domain.Contract;

namespace BKG.Trading.Applications.StockSwing;

public sealed class StockPurchaseService
{
    private readonly IScreener _screener;
    private readonly ILTPProvider _ltpProvider;
    private readonly IBroker _broker;
    private readonly string _buyOrderType;
    private readonly decimal _minimumFundAllotment;
    private readonly int _maximumStockPoolSize;
    private readonly string _marketOrderType;
    private readonly string _limitOrderType;

    public StockPurchaseService(
        IScreener screener,
        ILTPProvider ltpProvider,
        IBroker broker,
        decimal minimumFundAllotment,
        int maximumStockPoolSize,
        string marketOrderType,
        string limitOrderType,
        string buyOrderType)
    {
        _screener = screener;
        _ltpProvider = ltpProvider;
        _broker = broker;

        _minimumFundAllotment = minimumFundAllotment > 0 ? minimumFundAllotment : 200_000m;
        _maximumStockPoolSize = maximumStockPoolSize > 0 ? maximumStockPoolSize : 10;

        _marketOrderType = string.IsNullOrWhiteSpace(marketOrderType)
            ? "Market"
            : marketOrderType.Trim();

        _limitOrderType = string.IsNullOrWhiteSpace(limitOrderType)
            ? "Limit"
            : limitOrderType.Trim();

        _buyOrderType = string.IsNullOrWhiteSpace(buyOrderType)
            ? _marketOrderType
            : buyOrderType.Trim();
    }
    public async Task<StockPurchaseResult> ExecuteAsync()
    {
        var purchases = new List<StockPurchaseRecord>();

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("              STOCK SWING STRATEGY ENGINE");
        Console.WriteLine("========================================================");

        decimal availableMargin = await _broker.GetAvailableMarginAsync();

        Console.WriteLine($"Available Margin         : ₹{availableMargin:N2}");
        Console.WriteLine($"Minimum Allocation/Stock : ₹{_minimumFundAllotment:N2}");
        Console.WriteLine("========================================================");

        if (availableMargin < _minimumFundAllotment)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("Insufficient margin available.");
            Console.WriteLine("Skipping swing stock purchase process.");
            Console.ResetColor();

            return CreateResult(purchases);
        }

        int maximumStocksToPurchase = Math.Min((int)(availableMargin / _minimumFundAllotment), _maximumStockPoolSize);

        Console.WriteLine();
        Console.WriteLine($"Maximum Stocks Allowed   : {maximumStocksToPurchase}");

        IDictionary<string, decimal> filteredStockListForSwing = await _screener.GetSwingStockListAsync();

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("STEP 1 : TRADINGVIEW SCREENER OUTPUT");
        Console.WriteLine("========================================================");
        Console.WriteLine($"{"SYMBOL",-15} {"CHANGE %",10}");

        foreach (var stock in filteredStockListForSwing.OrderBy(x => x.Value))
        {
            Console.WriteLine($"{stock.Key,-15} {stock.Value,10:N2}");
        }

        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine($"Candidates Found : {filteredStockListForSwing.Count}");

        HashSet<string> existingHoldings = (await _broker.GetExistingHoldings()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("STEP 2 : EXISTING HOLDINGS");
        Console.WriteLine("========================================================");

        if (existingHoldings.Count == 0)
        {
            Console.WriteLine("No existing holdings found.");
        }
        else
        {
            foreach (string holding in existingHoldings.OrderBy(x => x))
            {
                Console.WriteLine(holding);
            }
        }

        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine($"Holdings Count : {existingHoldings.Count}");

        List<string> selectedStocks =
            filteredStockListForSwing
                .Where(x => !existingHoldings.Contains(x.Key))
                .OrderBy(x => x.Value)
                .Take(maximumStocksToPurchase)
                .Select(x => x.Key)
                .ToList();

        if (selectedStocks.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("No eligible stocks available after holding exclusion.");
            Console.ResetColor();

            return CreateResult(purchases);
        }
        
        IDictionary<string, decimal> stockPrices = await _ltpProvider.FetchLTPAsync(selectedStocks);

        decimal fundAllotmentPerStock = availableMargin / selectedStocks.Count;

        IDictionary<string, int> stockUnits =
            stockPrices
                .Where(x => x.Value > 0)
                .ToDictionary(
                    x => x.Key,
                    x => Math.Max(
                        1,
                        (int)(fundAllotmentPerStock / x.Value)));

        var orderPreview =
            stockUnits
                .OrderBy(x => x.Key)
                .Select(x => new
                {
                    Symbol = x.Key,
                    Ltp = stockPrices[x.Key],
                    Units = x.Value,
                    CapitalAllocation = stockPrices[x.Key] * x.Value
                })
                .ToList();

        decimal totalCapitalAllocation = orderPreview.Sum(x => x.CapitalAllocation);

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("STEP 3 : ORDER PREVIEW");
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
        Console.WriteLine($"Press ENTER to place {_buyOrderType.ToUpperInvariant()} orders...");
        Console.ReadLine();

        foreach (var stock in stockUnits.Where(x => x.Value > 0))
        {
            if (string.Equals(_buyOrderType, _limitOrderType, StringComparison.OrdinalIgnoreCase))
            {
                await _broker.PlaceLimitBuyOrderAsync(stock.Key, stock.Value, stockPrices[stock.Key]);
            }
            else
            {
                await _broker.PlaceMarketBuyOrderAsync(stock.Key, stock.Value);
            }
            
            await Task.Delay(TimeSpan.FromSeconds(1));

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