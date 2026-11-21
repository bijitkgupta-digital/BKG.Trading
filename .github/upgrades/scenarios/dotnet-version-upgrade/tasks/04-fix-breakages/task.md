# 04-fix-breakages: Resolve compilation and behavioral/API impact from framework and package changes

Address post-upgrade build breaks and warning regressions introduced by .NET 10 and dependency updates, with focus on behavioral-change hotspots identified in assessment (Adapters and Console).

Update code/config usage where required by API surface or runtime behavior changes while preserving existing trading workflows and integration contracts.

## Scope Inventory

### Projects affected
- BKG.Trading.Adapters
- BKG.Trading.Console
- BKG.Trading.Repository (dependency update impact validation)

### Distinct concerns
- Resolve compile breaks introduced by package major/minor upgrades.
- Validate that behavioral/API hotspots from assessment are addressed in upgraded code paths.
- Confirm warning/error-free builds after remediation.

### Findings and remediation
- Primary compile break surfaced after `Tech.Zerodha.KiteConnect` upgrade to 5.2.2 in `KiteConnectClient.cs`.
- Remediation applied in prior package task and validated in this task:
  - `Constants.EXCHANGE_NSE` -> `Constants.Exchange.NSE`
  - `Constants.TRANSACTION_TYPE_BUY` -> `Constants.Transaction.Buy`
  - `Constants.PRODUCT_CNC` -> `Constants.Product.CNC`
  - `Constants.ORDER_TYPE_LIMIT` -> `Constants.OrderType.Limit`
  - `Constants.VARIETY_REGULAR` -> `Constants.Variety.Regular`
- No additional compile-time breakages surfaced for Console/Repository after upgrading Microsoft.Extensions and SQL/data access packages.

### Validation snapshots
- `BKG.Trading.Adapters.csproj`: build successful.
- `BKG.Trading.Console.csproj`: build successful.
- Full workspace build: successful.

**Done when**: Solution builds cleanly with zero errors and warnings in modified projects, and identified behavioral/API issues are remediated.
