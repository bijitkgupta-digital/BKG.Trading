# 02-upgrade-tfms: Update all projects from net8.0 to net10.0

Update TargetFramework values for all solution projects in one atomic pass, keeping project references and build configuration consistent across Domain, Applications, Adapters, Repository, Infrastructure, and Console projects.

Ensure the solution remains internally aligned on a single TFM to avoid partial-upgrade restore/build drift.

## Scope Inventory

### Projects affected
- BKG.Trading.Adapters
- BKG.Trading.Applications
- BKG.Trading.Console
- BKG.Trading.Domain
- BKG.Trading.Infrastructure
- BKG.Trading.Repository

### Distinct concerns
- Replace `TargetFramework` from `net8.0` to `net10.0` in all SDK-style project files.
- Ensure no inherited `TargetFramework` from `Directory.Build.props` overrides project-level settings.
- Validate restore/build after TFM bump.

### Findings
- `TargetFramework` is defined directly in each project `.csproj` file.
- No `Directory.Build.props` or centralized property file was found in repo scope for TFM override.
- All projects are single-target SDK-style projects and currently aligned on `net8.0`.

### Planned file changes
- `BKG.Trading/BKG.Trading.Adapters/BKG.Trading.Adapters.csproj`
- `BKG.Trading/BKG.Trading.Applications/BKG.Trading.Applications.csproj`
- `BKG.Trading/BKG.Trading.Console/BKG.Trading.Console.csproj`
- `BKG.Trading/BKG.Trading.Domain/BKG.Trading.Domain.csproj`
- `BKG.Trading/BKG.Trading.Infrastructure/BKG.Trading.Infrastructure.csproj`
- `BKG.Trading/BKG.Trading.Repository/BKG.Trading.Repository.csproj`
