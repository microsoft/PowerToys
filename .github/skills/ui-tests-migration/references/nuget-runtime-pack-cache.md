# NuGet runtime-pack cache misses

## Symptom

CI reports `NU1102` for an implicit `Microsoft.*.Runtime.*` or `Microsoft.*.Host.*` package even
though the PR did not change package configuration.

## Cause

PowerToys CI uses a floating .NET SDK. A new SDK patch can request an exact framework-pack version
that `PowerToysPublicDependencies` has not cached from its upstream source yet.

Do not pin the SDK, add explicit framework-pack references, or downgrade unrelated packages to fix
this feed-state problem.

## Resolution

1. Confirm the CI SDK version and that the PR did not change `Directory.Packages.props`,
   `nuget.config`, or SDK selection.
2. Use that SDK locally and authenticate through the Azure Artifacts credential provider. The
   identity needs **Feed and Upstream Reader (Collaborator)** permission or higher.
3. Restore the failing self-contained projects for both architectures so the feed caches the packs:

   ```pwsh
   dotnet restore <project.csproj> -p:Platform=x64 --interactive --no-cache
   dotnet restore <project.csproj> -p:Platform=ARM64 --interactive --no-cache
   ```

4. Rerun NuGet verification:

   ```pwsh
   .\.pipelines\verifyNugetPackages.ps1 -solution .\PowerToys.slnx
   ```

## Follow-on dependency audit

Once the packs are cached, CI may expose that centrally managed .NET packages are still on the
previous patch. If the deps audit groups framework assemblies under both patch versions, advance the
repository's managed .NET package set together, then rerun NuGet verification and the deps audit.

Never place a PAT in a command or checked-in NuGet configuration.