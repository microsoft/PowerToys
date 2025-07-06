# Group Policy Integration

PowerToys settings can be controlled and enforced via Group Policy. This document describes how Group Policy integration is implemented in the settings system.

## Overview

Group Policy settings for PowerToys allow administrators to:

- Enable or disable PowerToys entirely
- Control which modules are available
- Configure specific settings for individual modules
- Enforce settings across an organization

## Implementation Details

When a setting is controlled by Group Policy:

1. The UI shows the setting as locked (disabled)
2. The module checks GPO settings before applying user settings
3. GPO settings take precedence over user settings

## Group Policy Settings Detection

The settings UI checks for Group Policy settings during initialization:

```csharp
// Example code for checking if a setting is controlled by GPO
bool isControlledByPolicy = RegistryHelper.GetGPOValue("PolicyKeyPath", "PolicyValueName", out object value);
if (isControlledByPolicy)
{
    // Use the policy value and disable UI controls
    setting.IsEnabled = false;
    setting.Value = (bool)value;
}
```

## UI Indication for Managed Settings

When a setting is managed by Group Policy, the UI indicates this to the user:

- Controls are disabled (grayed out)
- A tooltip indicates the setting is managed by policy
- The actual policy value is displayed

## Testing Group Policy Settings

To test Group Policy integration:

1. Create a test GPO using the PowerToys ADMX template
2. Apply settings in the Group Policy Editor
3. Verify that the settings UI correctly reflects the policy settings
4. Verify that the modules honor the policy settings

## GPO Settings vs. User Settings

The precedence order for settings is:

1. Group Policy settings (highest priority)
2. User settings (lower priority)
3. Default settings (lowest priority)

When a setting is controlled by Group Policy, attempts to modify it through the settings UI or programmatically will not persist, as the policy value will always take precedence.

For more information on PowerToys Group Policy implementation, see the [GPO Implementation](/doc/devdocs/processes/gpo.md) documentation.
