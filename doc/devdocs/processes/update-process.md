# PowerToys Update Process

This document describes how the PowerToys update mechanism works.

## Key Files

- `updating.h` and `updating.cpp` in common - Contains code for handling updates and helper functions
- `update_state.h` and `update_state.cpp` - Handles loading and saving of update state

## Update Process

### Version Detection
- Uses GitHub API to get the latest version information
- API returns JSON with release information including version and assets
- Checks asset names to find the correct installer based on:
  - Architecture (ARM64 or X64)
  - Installation scope (user or machine)

### Installation Scope
- Differentiates between user installer and machine installer
- Different patterns are defined to distinguish between the two scopes
- Both have different upgrade codes

### Update State
- State is stored in a local file
- Contains information like:
  - Current update state
  - Release page URL
  - Last time check was performed
  - Whether a new version is available
  - Whether installer is already downloaded

### Update Checking
- Manual check: When user clicks "Check for Updates" in settings
- Automatic check: Periodic update worker runs periodically to check for updates
- Update state is saved to: `%LOCALAPPDATA%\Microsoft\PowerToys\update_state.json`

### Update Process Flow
1. Check current version against latest version from GitHub
2. If newer version exists:
   - Check metered connection settings
   - Check if automatic updates are enabled
   - Check GPO settings
3. Process new version:
   - Check if installer is already downloaded
   - Clean up old installer files
   - Download new installer if needed
4. Notify user via toast notification

### PowerToys Updater
- `PowerToysUpdate.exe` - Executable shipped with installer
- Handles downloading and running the installer
- Called when user clicks the update toast notification
- Downloads the installer if not already downloaded

### Version Numbering
- Semantic versioning: `MAJOR.MINOR.PATCH`
- MINOR version increases with regular releases (e.g., 0.89.0)
- PATCH version increases for hotfixes (e.g., 0.87.0 → 0.87.1)

### Installer Details
- Uses WiX bootstrapper
- Defines upgrade codes for per-user and per-machine installations
- These codes must remain consistent for proper updating

## GPO Update Settings

PowerToys respects Group Policy settings for controlling updates:

- `disable automatic update download` - Prevents automatic downloading
- `disable new update toast` - Controls if toast notifications are shown
- `suspend new update toast` - Suspends toast notifications for 2 minor releases

## User Settings

Users can control update behavior through the PowerToys settings:

- Automatic update downloads can be enabled/disabled
- Download and install updates automatically on metered connections

## Update Notification

When a new update is available:
1. Toast notification appears in the Windows Action Center
2. Clicking the notification starts the update process
3. The updater downloads the installer (if not already downloaded)
4. The installer runs with appropriate command-line arguments

## Debugging Tips

### Testing Update Detection
- To force an update check, modify the timestamp in the update state file to an earlier date
- Exit PowerToys, modify the file, then restart PowerToys

### Common Issues
- Permission issues can prevent downloading updates
- Network connectivity problems may interrupt downloads
- Group Policy settings may block updates
- Installer may fail if the application is running

### Update Logs
- Check PowerToys logs for update-related messages
- `%LOCALAPPDATA%\Microsoft\PowerToys\Logs\PowerToys-*.log`
- Look for entries related to update checking and downloading

## Rollout Considerations

- Updates are made available to all users simultaneously
- No staged rollout mechanism is currently implemented
- Critical issues discovered after release require a hotfix
- See [Release Process](release-process.md) for details on creating hotfixes
