# 01-prerequisites: Validate SDK/tooling and establish upgrade baseline

Confirm .NET 10 SDK availability, solution restore/build baseline, and repository readiness for upgrade execution. Capture current direct package versions and note where centralized versus project-level package references are defined so upgrade edits are deterministic.

This task also verifies that migration assumptions from assessment remain valid before making framework/package changes (modern .NET-only, SDK-style projects, no legacy conversion tasks).

## Scope Inventory

### Projects affected
- BKG.Trading.Adapters
- BKG.Trading.Repository
- BKG.Trading.Console
- BKG.Trading.Applications
- BKG.Trading.Domain
- BKG.Trading.Infrastructure

### Distinct concerns
- Validate .NET 10 SDK availability for target framework migration.
- Capture baseline solution build state before edits.
- Inventory direct package references and package-definition locations.
- Confirm package management mode (project-level references vs centralized package management).
- Confirm test project availability for later validation planning.

### Change signals and baseline findings
- Target SDK check: .NET 10 SDK is available (`validate_dotnet_sdk_installation` returned compatible).
- Baseline build: whole solution build succeeds before migration (`run_build` succeeded).
- Package management mode: no CPM/Directory.Packages.props in use; direct package versions are defined in project `.csproj` files.
- Direct package baseline:
  - `BKG.Trading.Adapters.csproj`: `fyers-api-v3 1.6.0`, `Tech.Zerodha.KiteConnect 4.3.0`
  - `BKG.Trading.Repository.csproj`: `Dapper 2.1.89`, `Microsoft.Data.SqlClient 7.1.1`
  - `BKG.Trading.Console.csproj`: `Microsoft.Extensions.Configuration.Binder 9.0.22`, `Microsoft.Extensions.Configuration.Json 9.0.22`, `Microsoft.Extensions.Http 9.0.22`
- Test projects: none discovered in the current solution (`discover_test_projects` returned no test projects).

### Execution assumptions confirmed
- All projects are SDK-style and currently on `net8.0`.
- Migration will proceed one-by-one per user preference while maintaining task ordering.
- Unused direct dependencies must be identified and removed per project during package-upgrade execution.

**Done when**: .NET 10 SDK is validated, baseline build/test state is captured, and upgrade inputs (projects + direct package references) are documented for execution.
