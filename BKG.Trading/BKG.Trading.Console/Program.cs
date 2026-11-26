using BKG.Trading.Adapters;
using BKG.Trading.Applications.StockSwing;
using BKG.Trading.Domain.Contract;
using BKG.Trading.Repository.SQL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("     BKG.TRADING CORE QUANT SYSTEM ENGINE INITIALIZATION          ");
        Console.WriteLine("=================================================================");

        try
        {
            IServiceProvider serviceProvider = await BuildServiceProvider();

            // Future Parallel Execution
            await Task.WhenAll(
                //RunEtfAveragingAsync(serviceProvider),
                RunStockSwingAsync(serviceProvider));
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine($"❌ Critical System Pipeline Termination Exception: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task<IServiceProvider> BuildServiceProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string dbConnectionString =
            configuration.GetConnectionString("TradingDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'TradingDatabase' not found.");

        string kiteApiKey =
            configuration["KiteConfig:ApiKey"]
            ?? throw new InvalidOperationException(
                "Kite ApiKey configuration is missing.");

        string kiteAccessToken =
            configuration["KiteConfig:AccessToken"]
            ?? throw new InvalidOperationException(
                "Kite AccessToken configuration is missing.");

        string buyOrderType =
            configuration["Trading:DefaultBuyOrderType"]
            ?? "Market";

        decimal minimumFundAllotment =
            configuration.GetValue<decimal?>("Trading:MinimumFundAllotment")
            ?? 200_000m;

        int maximumStockPoolSize =
            configuration.GetValue<int?>("Trading:MaximumStockPoolSize")
            ?? 10;

        string marketOrderType =
            configuration["Trading:MarketOrderType"]
            ?? "Market";

        string limitOrderType =
            configuration["Trading:LimitOrderType"]
            ?? "Limit";

        IBroker kiteBroker = await KiteConnectClient.CreateAsync(kiteApiKey, kiteAccessToken);

        return new ServiceCollection()
            .AddHttpClient()
            .AddSingleton<ITradingRepository>(_ => new DapperTradingRepository(dbConnectionString))
            .AddSingleton<ILTPProvider>(_ => new YahooClient(new HttpClient()))
            .AddSingleton<IScreener>(_ => new TradingViewClient(new HttpClient()))
            .AddSingleton<IBroker>(kiteBroker)
            .AddTransient<ETFPurchaseService>()
            .AddTransient<StockPurchaseService>(sp => new StockPurchaseService(
                sp.GetRequiredService<IScreener>(),
                sp.GetRequiredService<ILTPProvider>(),
                sp.GetRequiredService<IBroker>(),
                minimumFundAllotment,
                maximumStockPoolSize,
                marketOrderType,
                limitOrderType,
                buyOrderType))
            .BuildServiceProvider();
    }

    private static async Task RunEtfAveragingAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine();
        Console.WriteLine("Running ETF Averaging Strategy...");

        var purchaseService =
            serviceProvider.GetRequiredService<ETFPurchaseService>();

        var result =
            await purchaseService.ExecuteAsync();

        if (!result.AnyPurchase)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No ETF purchases triggered today.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine();
        Console.WriteLine("ETF Purchase Summary");
        Console.WriteLine("--------------------");

        foreach (var purchase in result.Purchases)
        {
            Console.WriteLine(
                $"{purchase.Symbol,-10} " +
                $"Price: {purchase.Ltp:N2} " +
                $"Units: {purchase.UnitsPurchased}");
        }

        Console.ResetColor();

        await Task.CompletedTask;
    }

    private static async Task RunStockSwingAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine();
        Console.WriteLine("Running Stock Swing Strategy...");

        var purchaseService = serviceProvider.GetRequiredService<StockPurchaseService>();

        var result = await purchaseService.ExecuteAsync();

        if (!result.AnyPurchase)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No Stock purchases triggered today.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine();
        Console.WriteLine("Stock Purchase Summary");
        Console.WriteLine("--------------------");

        foreach (var purchase in result.Purchases)
        {
            Console.WriteLine(
                $"{purchase.Symbol,-10} " +
                $"Price: {purchase.Ltp:N2} " +
                $"Units: {purchase.UnitsPurchased}");
        }

        Console.ResetColor();

        await Task.CompletedTask;
    }
}