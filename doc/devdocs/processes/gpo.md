# PowerToys GPO (Group Policy Objects) Implementation

Group Policy Objects (GPOs) allow system administrators to control PowerToys settings across an organization. This document describes how GPOs are implemented in PowerToys.

## GPO Overview

GPO policies allow system administrators to control PowerToys settings. PowerToys ships GPO files as part of the release zip, not installed directly.

## GPO File Structure

### ADMX File
- Contains policy definitions
- Defines which versions support each policy
- Sets up folder structure
- Defines each policy with:
  - Name
  - Class (user scope or machine scope)
  - Description
  - Registry location where policy is stored
  - Enabled/disabled values

### ADML File
- Contains localized strings for the ADMX file
- Contains revision number that must be updated when changes are made
- Stores strings for:
  - Folder names
  - Version definitions
  - Policy descriptions and titles
- Currently only ships English US version (no localization story yet)

## Installation Process

- Files need to be placed in: `C:\Windows\PolicyDefinitions\`
- ADMX file goes in the root folder
- ADML file goes in the language subfolder (e.g., en-US)
- After installation, policies appear in the Group Policy Editor (gpedit.msc)

## Registry Implementation

- Policies are stored as registry values
- Location: `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\PowerToys` or `HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\PowerToys`
- Machine scope takes precedence over user scope
- Policy states:
  - Enabled: Registry value set to 1
  - Disabled: Registry value set to 0
  - Not Configured: Registry value does not exist

## Code Integration

### Common Files
- Policy keys defined in `common\utils\GPO.h`
- Contains functions to read registry values and get configured values
- WinRT C++ adapter created for C# applications to access GPO settings

### WPF Applications
- WPF applications cannot directly load WinRT C++ projects
- Additional library created to allow WPF applications to access GPO values

### Module Interface
- Each module must implement policy checking in its interface
- Runner checks this to determine if module should be started or not

## UI Implementation

- When a policy disables a utility:
  - UI is locked (cannot be enabled)
  - Settings page shows a lock icon
  - Dashboard hides the module button
  - If user tries to start the executable directly, it exits and logs a message

## Types of GPO Policies

### Basic Module Enable/Disable Policy
- Most common type
- Controls whether a module can be enabled or disabled
- Shared description text for these policies

### Configuration Policies
- Example: Run at startup setting
- Controls specific settings rather than enabling/disabling modules
- Custom description text explaining what happens when enabled/disabled/not configured

### Machine-Scope Only Policies
- Example: Mouse Without Borders service mode
- Only makes sense at machine level (not user level)
- Restricts functionality that requires elevated permissions

## Steps to Add a New Policy

1. Update ADMX file:
   - Increase revision number
   - Add supported version definition
   - Define the policy with registry location

2. Update ADML file:
   - Increase revision number
   - Add strings for version, title, description

3. Update code:
   - Add to GPO.h
   - Add to GPO wrapper for C# access
   - Update module interface
   - Modify settings UI to show lock when policy applied
   - Add checks in executable to prevent direct launching
   - Update dashboard helper to respect policy

4. Add to bug report tool to capture policy state

## Update-Related GPO Settings

- `disable automatic update download` - Prevents automatic downloading
- `disable new update toast` - Controls if toast notifications are shown
- `suspend new update toast` - Suspends toast notifications for 2 minor releases

## Testing GPO Settings

To test GPO settings locally:

1. Run `regedit` as administrator
2. Navigate to `HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\PowerToys`
3. Create a new DWORD value with the name of the policy
4. Set the value to 0 (disabled) or 1 (enabled)
5. Restart PowerToys to see the effect

For user-scope policies, use `HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\PowerToys` instead.
