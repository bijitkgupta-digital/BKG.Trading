# 03-upgrade-packages: Upgrade direct dependencies to latest stable .NET 10-compatible versions

Apply dependency modernization policy across direct package references, including replacing Tech.Zerodha.KiteConnect 4.3.0 with the latest stable compatible release and upgrading other direct dependencies to latest stable compatible versions.

Use assessment signals as starting guidance (Microsoft.Extensions.Configuration.Binder/Json/Http upgrades) and include additional direct references in Adapters/Repository/Console where newer compatible versions exist.

## Scope Inventory

### Projects affected
- BKG.Trading.Adapters
- BKG.Trading.Repository
- BKG.Trading.Console

### Distinct concerns
- Upgrade direct package references one-by-one to latest stable versions compatible with `net10.0`.
- Validate restore/build after each dependency bump (user-requested sequential execution style).
- Remove unused direct dependencies from every project if usage analysis confirms they are not required.

### Current direct package baseline
- `BKG.Trading.Adapters.csproj`
  - `fyers-api-v3` 1.6.0 -> target 1.7.0
  - `Tech.Zerodha.KiteConnect` 4.3.0 -> target 5.2.2
- `BKG.Trading.Repository.csproj`
  - `Dapper` 2.1.89 -> target 2.1.100
  - `Microsoft.Data.SqlClient` 7.1.1 -> target 7.1.2
- `BKG.Trading.Console.csproj`
  - `Microsoft.Extensions.Configuration.Binder` 9.0.22 -> target 11.0.0
  - `Microsoft.Extensions.Configuration.Json` 9.0.22 -> target 11.0.0
  - `Microsoft.Extensions.Http` 9.0.22 -> target 11.0.0

### Unused dependency analysis (direct references)
- `fyers-api-v3`: used in `BKG.Trading.Adapters/FyersClient.cs` (`using FyersCSharpSDK;`, `FyersClass`, `StockModel`).
- `Tech.Zerodha.KiteConnect`: used in `BKG.Trading.Adapters/KiteConnectClient.cs` (`using KiteConnect;`, `Kite`, `User`, `UserMargin`, `Constants`).
- `Dapper`: used in `BKG.Trading.Repository/SQL/DapperTradingRepository.cs` (`using Dapper;`, `QueryAsync`, `ExecuteAsync`).
- `Microsoft.Data.SqlClient`: used in `BKG.Trading.Repository/SQL/DapperTradingRepository.cs` (`SqlConnection`).
- `Microsoft.Extensions.Configuration.Binder` / `Json` / `Http`: used in `BKG.Trading.Console/Program.cs` (`ConfigurationBuilder.AddJsonFile`, `GetConnectionString`, `ServiceCollection.AddHttpClient`).

No currently declared direct package reference is removable as unused based on source usage.

**Done when**: Direct package references are upgraded per policy, package restore succeeds, and no unresolved package compatibility conflicts remain.
