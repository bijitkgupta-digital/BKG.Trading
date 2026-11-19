using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BKG.Trading.Repository.SQL;
using BKG.Trading.Domain.Contract;
using BKG.Trading.Adapters;

namespace BKG.Trading.ConsoleApp;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("     BKG.TRADING CORE QUANT SYSTEM ENGINE INITIALIZATION         ");
        Console.WriteLine("=================================================================");

        // 1. Build the Configuration Engine Root
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // 2. Extract Values Securely from JSON Sections
        string dbConnectionString = configuration.GetConnectionString("TradingDatabase")
            ?? throw new InvalidOperationException("Connection string 'TradingDatabase' not found in config.");

        string fyersClientId = configuration["FyersConfig:ClientId"]
            ?? throw new InvalidOperationException("Fyers ClientId configuration is missing.");

        string fyersAccessToken = configuration["FyersConfig:AccessToken"]
            ?? throw new InvalidOperationException("Fyers AccessToken configuration is missing.");

        string fyersRedirectUri = configuration["FyersConfig:RedirectUri"]
            ?? throw new InvalidOperationException("Fyers RedirectUri configuration is missing.");

        //var fyersClient = await FyersDataClient.CreateAsync(new HttpClient(), fyersClientId, fyersAccessToken, fyersRedirectUri);
        var yahooClient = new YahooClient(new HttpClient());

        // 3. Setup Dependency Injection IoC Container
        var serviceProvider = new ServiceCollection()
            .AddHttpClient()
            .AddTransient<ITradingRepository>(sp => new DapperTradingRepository(dbConnectionString))
            //.AddSingleton<IMarketDataProvider>(fyersClient)
            .AddSingleton<ILTPProvider>(yahooClient)
            .BuildServiceProvider();

        try
        {
            var marketDataProvider =
                serviceProvider.GetRequiredService<ILTPProvider>();

            var symbols = new[]
            {
                "MAFANG", "MON100", "MONQ50"
            };

            Console.WriteLine();
            Console.WriteLine("============== LIVE MARKET PRICES ==============");

            foreach (var symbol in symbols)
            {
                Console.WriteLine($"{symbol,-10} : {await marketDataProvider.FetchLTPAsync(symbol):N2}");
            }

            Console.WriteLine("================================================");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Critical System Pipeline Termination Exception: {ex.Message}");
            Console.ResetColor();
        }
    }
}
