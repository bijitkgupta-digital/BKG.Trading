# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade the full BKG.Trading solution from .NET 8 to .NET 10 and modernize direct NuGet dependencies to latest stable versions compatible with .NET 10.
**Scope**: 6 SDK-style projects, ~1.2k LOC, low migration difficulty with package/version and behavioral-change validation.

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: The solution has 6 projects, all already SDK-style and on modern .NET, with low-risk package/API findings and no .NET Framework migration constraints.

## Tasks

### 01-prerequisites: Validate SDK/tooling and establish upgrade baseline

Confirm .NET 10 SDK availability, solution restore/build baseline, and repository readiness for upgrade execution. Capture current direct package versions and note where centralized versus project-level package references are defined so upgrade edits are deterministic.

This task also verifies that migration assumptions from assessment remain valid before making framework/package changes (modern .NET-only, SDK-style projects, no legacy conversion tasks).

**Done when**: .NET 10 SDK is validated, baseline build/test state is captured, and upgrade inputs (projects + direct package references) are documented for execution.

---

### 02-upgrade-tfms: Update all projects from net8.0 to net10.0

Update TargetFramework values for all solution projects in one atomic pass, keeping project references and build configuration consistent across Domain, Applications, Adapters, Repository, Infrastructure, and Console projects.

Ensure the solution remains internally aligned on a single TFM to avoid partial-upgrade restore/build drift.

**Done when**: All 6 projects target net10.0 and solution restore succeeds without TFM mismatch/conflict errors.

---

### 03-upgrade-packages: Upgrade direct dependencies to latest stable .NET 10-compatible versions

Apply dependency modernization policy across direct package references, including replacing Tech.Zerodha.KiteConnect 4.3.0 with the latest stable compatible release and upgrading other direct dependencies to latest stable compatible versions.

Use assessment signals as starting guidance (Microsoft.Extensions.Configuration.Binder/Json/Http upgrades) and include additional direct references in Adapters/Repository/Console where newer compatible versions exist.

**Done when**: Direct package references are upgraded per policy, package restore succeeds, and no unresolved package compatibility conflicts remain.

---

### 04-fix-breakages: Resolve compilation and behavioral/API impact from framework and package changes

Address post-upgrade build breaks and warning regressions introduced by .NET 10 and dependency updates, with focus on behavioral-change hotspots identified in assessment (Adapters and Console).

Update code/config usage where required by API surface or runtime behavior changes while preserving existing trading workflows and integration contracts.

**Done when**: Solution builds cleanly with zero errors and warnings in modified projects, and identified behavioral/API issues are remediated.

---

### 05-validate-upgrade: Run final validation and document upgrade outcomes

Run full solution validation (build + tests + smoke checks), confirm the upgraded dependency graph and .NET 10 targeting are stable, and document any deferred non-blocking recommendations.

This closes the migration with a verifiable readiness checkpoint for ongoing development and CI usage.

**Done when**: Full build/test validation passes, upgrade results are documented, and solution is ready for routine development on .NET 10.
