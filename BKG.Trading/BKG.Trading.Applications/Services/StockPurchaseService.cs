using BKG.Trading.Applications.Models;
using BKG.Trading.Domain.Contract;

namespace BKG.Trading.Applications.StockSwing;

public sealed class StockPurchaseService
{
    private readonly IScreener _screener;
    private readonly ILTPProvider _ltpProvider;
    private readonly IBroker _broker;

    private const decimal MinimumFundAllotment = 200_000m;

    public StockPurchaseService(IScreener screener, ILTPProvider ltpProvider, IBroker broker)
    {
        _screener = screener;
        _ltpProvider = ltpProvider;
        _broker = broker;
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
        Console.WriteLine($"Minimum Allocation/Stock : ₹{MinimumFundAllotment:N2}");
        Console.WriteLine("========================================================");

        if (availableMargin < MinimumFundAllotment)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("Insufficient margin available.");
            Console.WriteLine("Skipping swing stock purchase process.");
            Console.ResetColor();

            return CreateResult(purchases);
        }

        int maximumStocksToPurchase = (int)(availableMargin / MinimumFundAllotment);

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

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("STEP 3 : FINAL STOCKS FOR PURCHASE");
        Console.WriteLine("========================================================");

        foreach (string stock in selectedStocks)
        {
            Console.WriteLine(stock);
        }

        Console.WriteLine("--------------------------------------------------------");
        Console.WriteLine($"Selected Count : {selectedStocks.Count}");

        if (selectedStocks.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("No eligible stocks available after holding exclusion.");
            Console.ResetColor();

            return CreateResult(purchases);
        }

        IDictionary<string, decimal> stockPrices = await _ltpProvider.FetchLTPAsync(selectedStocks);

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("STEP 4 : LIVE MARKET PRICES");
        Console.WriteLine("========================================================");
        Console.WriteLine($"{"SYMBOL",-15} {"LTP",12}");

        foreach (var stock in stockPrices.OrderBy(x => x.Key))
        {
            Console.WriteLine($"{stock.Key,-15} {stock.Value,12:N2}");
        }

        decimal fundAllotmentPerStock = availableMargin / selectedStocks.Count;

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("STEP 5 : CAPITAL ALLOCATION");
        Console.WriteLine("========================================================");
        Console.WriteLine($"Fund Per Stock : ₹{fundAllotmentPerStock:N2}");

        IDictionary<string, int> stockUnits =
            stockPrices
                .Where(x => x.Value > 0)
                .ToDictionary(
                    x => x.Key,
                    x => Math.Max(
                        1,
                        (int)(fundAllotmentPerStock / x.Value)));

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("STEP 6 : ORDER PREVIEW");
        Console.WriteLine("========================================================");
        Console.WriteLine($"{"SYMBOL",-15} {"LTP",12} {"UNITS",10}");

        foreach (var stock in stockUnits.OrderBy(x => x.Key))
        {
            Console.WriteLine(
                $"{stock.Key,-15} " +
                $"{stockPrices[stock.Key],12:N2} " +
                $"{stock.Value,10}");
        }

        Console.WriteLine("========================================================");
        Console.WriteLine("Press ENTER to place MARKET orders...");
        Console.ReadLine();

        foreach (var stock in stockUnits.Where(x => x.Value > 0))
        {
            await _broker.PlaceLimitBuyOrderAsync(stock.Key, stock.Value, stockPrices[stock.Key]);
            //await _broker.PlaceMarketBuyOrderAsync(stock.Key, stock.Value);

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