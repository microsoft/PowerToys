# PowerToys Development Guidelines

## Using Open Source Packages and Libraries

### License Considerations
- MIT license is generally acceptable for inclusion in the project
- For any license other than MIT, double check with the PM team
- All external packages or projects must be mentioned in the `notice.md` file
- Even if a license permits free use, it's better to verify with the team

### Safety and Quality Considerations
- Ensure the code being included is safe to use
- Avoid repositories or packages that are not widely used
- Check for packages with significant downloads/usage and good ratings
- Important because our pipeline signs external DLLs with Microsoft certificate
- Unsafe code signed with Microsoft certificate can cause serious issues

## Code Signing

### Signing JSON File
- Modifications to the signing JSON file are typically done manually
- When adding new DLLs (internal PowerToys modules or external libraries)
- When the release pipeline fails with a list of unsigned DLLs/executables:
  - For PowerToys DLLs, manually add them to the list
  - For external DLLs, verify they're safe to sign before including

### File Signing Requirements
- All DLLs and executables must be signed
- New files need to be added to the signing configuration
- CI checks if all files are signed
- Even Microsoft-sourced dependencies are signed if they aren't already

## Performance Measurement

- Currently no built-in timers to measure PowerToys startup time
- Startup measurement could be added in the runner:
  - At the start of the main method
  - After all module interface DLLs are loaded
- Alternative: use profilers or Visual Studio profiler
- Startup currently takes some time due to:
  - Approximately 20 module interface DLLs that need to be loaded
  - Modules that are started during loading
- No dashboards or dedicated tools for performance measurement
- Uses System.Diagnostics.Stopwatch in code
- Performance data is logged to default PowerToys logs
- Can search logs for stopwatch-related messages to diagnose performance issues
- Some telemetry events contain performance information

## Dependency Management

### WinRT SDK and CS/WinRT
- Updates to WinRT SDK and CS/WinRT are done periodically
- WinRT SDK often requires higher versions of CS/WinRT or vice versa
- Check for new versions in NuGet.org or Visual Studio's NuGet Package Explorer
- Prefer stable versions over preview versions
- Best practice: Update early in the release cycle to catch potential regressions

### WebView2
- Used for components like monotone file preview
- WebView2 version is related to the WebView runtime in Windows
- Previous issues with Windows Update installing new WebView runtime versions
- WebView team now includes PowerToys testing in their release cycle
- When updating WebView2:
  - Update the version
  - Open a PR
  - Perform sanity checks on components that use WebView2

### General Dependency Update Process
- When updating via Visual Studio, it will automatically update dependencies
- After updates, perform:
  - Clean build
  - Sanity check that all modules still work
  - Open PR with changes

## Testing Requirements

### Multiple Computers
- **Mouse Without Borders**: Requires multiple physical computers for proper testing
  - Testing with VMs is not recommended as it may cause confusion between host and guest mouse input
  - At least 2 computers are needed, sometimes testing with 3 is done
  - Testing is usually assigned to team members known to have multiple computers

### Multiple Monitors
- Some utilities require multiple monitors for testing
- At least 2 monitors are recommended
- One monitor should be able to use different DPI settings

### Fuzzing Testing
- Security team requires fuzzing testing for modules that handle file I/O or user input
- Helps identify vulnerabilities and bugs by feeding random, invalid, or unexpected data
- PowerToys integrates with Microsoft's OneFuzz service for automated testing
- Both .NET (C#) and C++ modules have different fuzzing implementation approaches
- New modules handling file I/O or user input should implement fuzzing tests
- For detailed setup instructions, see [Fuzzing Testing in PowerToys](../tools/fuzzingtesting.md)

### Testing Process
- For reporting bugs during the release candidate testing:
  1. Discuss in team chat
  2. Determine if it's a regression (check if bug exists in previous version)
  3. Check if an issue is already open
  4. Open a new issue if needed
  5. Decide on criticality for the release (if regression)

### Release Testing
- Team follows a release checklist
- Includes testing for WinGet configuration
- Sign-off process:
  - Teams sign off on modules independently
  - Regressions found in first release candidates lead to PRs
  - Second release candidate verified fixes
  - Command Palette needs separate sign-off
  - Final verification ensures modules don't crash with Command Palette integration

## PR Management and Release Process

### PR Review Process
- PM team typically tags PRs with "need review" 
- Small fixes from community that don't change much are usually accepted
- PM team adds tags like "need review" to highlight PRs
- PMs set priorities (sometimes using "info.90" tags)
- PMs decide which PRs to prioritize
- Team members can help get PRs merged when there's flexibility

### PR Approval Requirements
- PRs need approval from code owners before merging
- New team members can approve PRs but final approval comes from code owners

### PR Priority Handling
- Old PRs sometimes "slip through the cracks" if not high priority
- PMs tag important PRs with "priority one" to indicate they should be included in release
- Draft PRs are generally not prioritized

### Specific PR Types
- CI-related PRs need review and code owner approval
- Feature additions (like GPO support) need PM decision on whether the feature is wanted
- Bug fixes related to Watson errors sometimes don't have corresponding issue links
- Command Palette is considered high priority for the upcoming release

## Project Management Notes

- Be careful about not merging incomplete features into main
- Feature branches should be used for work in progress
- PRs touching installer files should be carefully reviewed
- Incomplete features should not be merged into main
- Use feature branches (feature/name-of-feature) for work-in-progress features
- Only merge to main when complete or behind experimentation flags
