# .NET Version Upgrade

## Strategy
**Selected**: All-At-Once
**Rationale**: 6 projects, all SDK-style and already on modern .NET, with low-risk upgrade profile and no .NET Framework migration requirements.

### Execution Constraints
- Perform framework and package upgrades across all projects in a single coordinated pass.
- Apply dependency modernization policy to direct references, including replacing Tech.Zerodha.KiteConnect 4.3.0 with the latest stable compatible version.
- Complete restore/build fix pass after upgrades before running final test validation.
- Keep warnings at zero for modified projects at task completion.

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)
- **Note**: Git CLI is not available in this environment, so branch and pending-change automation could not be executed by the agent.

## User Preferences
### Technical Preferences
- **Tech.Zerodha.KiteConnect**: Remove 4.3.0 and upgrade to latest stable version compatible with .NET 10.
- **Other dependencies**: Apply similar latest-stable compatible upgrades during the .NET 10 migration.
- **Dependency cleanup**: Remove unused direct dependencies from every project during upgrade.

### Execution Style
- **Upgrade sequencing**: Execute upgrades one-by-one with validation after each step.

## Key Decisions Log
- 2026-11-21: User requested dependency modernization as part of .NET 10 upgrade, including upgrading Zerodha KiteConnect from 4.3.0 to latest compatible and applying the same policy to other direct dependencies.
- 2026-11-21: Selected All-At-Once strategy for the .NET 10 migration plan due to low-complexity modern-to-modern upgrade profile.
- 2026-11-21: User requested one-by-one execution and unused dependency removal across all projects.

