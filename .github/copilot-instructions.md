# Copilot Instructions

## Project Guidelines
- User prefers upgrading Zerodha Kite from 4.3.0 to the latest compatible version and wants similar latest-compatible upgrades applied to other dependencies as part of the .NET 10 migration.
- Execute upgrades one by one and remove unused dependencies from every project as part of the .NET 10 migration.
- Order placement mode should be configuration-driven via appsettings, with market order as the default.
- For stock sell GTT placement, use a limit price at least 1.57% above the completed average buy price, with rounding.

## Code Style
- User prefers classic indentation style with one statement per line.
- Remove redundant methods/statements during cleanup.

## Logging and Refactoring
- User prefers hybrid logging: structured step logs via ILogger plus key console output.
- Refactor code across all projects with a focus on cleanup and logging-only changes.