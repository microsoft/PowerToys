# Group Policy Objects

Since version 0.64, PowerToys is released on GitHub with GroupPolicyObject files. You can check these releases on https://github.com/microsoft/PowerToys/releases .

## How to install

### Add the administrative template to an individual computer

1. Copy the "PowerToys.admx" file to your Policy Definition template folder. (Example: C:\Windows\PolicyDefinitions)
2. Copy the "PowerToys.adml" file to the matching language folder in your Policy Definition folder. (Example: C:\Windows\PolicyDefinitions\en-US)

### Add the administrative template to Active Directory

1. On a domain controller or workstation with RSAT, go to the **PolicyDefinition** folder (also known as the *Central Store*) on any domain controller for your domain. For older versions of Windows Server, you might need to create the **PolicyDefinition** folder. For more information, see [How to create and manage the Central Store for Group Policy Administrative Templates in Windows](https://support.microsoft.com/help/3087759/how-to-create-and-manage-the-central-store-for-group-policy-administra).
2. Copy the "PowerToys.admx" file to the PolicyDefinition folder. (Example: %systemroot%\sysvol\domain\policies\PolicyDefinitions)
3. Copy the "PowerToys.adml" file to the matching language folder in the PolicyDefinition folder. Create the folder if it doesn't already exist. (Example: %systemroot%\sysvol\domain\policies\PolicyDefinitions\EN-US)
4. If your domain has more than one domain controller, the new ADMX files will be replicated to them at the next domain replication interval.

### Scope

You will find the policies under "Administrative Templates/Microsoft PowerToys" in both the Computer Configuration and User Configuration folders. If both settings are configured, the setting in Computer Configuration takes precedence over the setting in User Configuration.

## Policies

### Configure enabled state

For each utility shipped with PowerToys, there's a "Configure enabled state" policy, which forces and Enabled state for the utility.

If you enable this setting, the utility will be always enabled and the user won't be able to disable it.

If you disable this setting, the utility will be always disabled and the user won't be able to enable it.

If you don't configure this setting, users are able to disable or enable the utility.

### Allow experimentation

This policy configures whether PowerToys experimentation is allowed. With experimentation allowed the user sees the new features being experimented if it gets selected as part of the test group. (Experimentation will only happen on Windows Insider builds.)

If this setting is not configured or enabled, the user can control experimentation in the PowerToys settings menu.

If this setting is disabled, experimentation is not allowed.

If this setting is not configured, experimentation is allowed.

### Installer and Updates

#### Disable per-user installation

This policy configures whether PowerToys per-user installation is allowed or not.

If enabled, per-user installation is not allowed.

If disabled or not configured, per-user installation is allowed.

You can set this policy only as Computer policy.
#### Disable automatic downloads

This policy configures whether automatic downloads of available updates are disabled or not. (On metered connections updates are never downloaded.)

If enabled, automatic downloads are disabled.

If disabled or not configured, the user is in control of automatic downloads setting.

#### Suspend Action Center notification for new updates

This policy configures whether the action center notification for new updates is suspended for 2 minor releases. (Example: if the installed version is v0.60.0, then the next notification is shown for the v0.63.* release.)

If enabled, the notification is suspended.

If disabled or not configured, the notification is shown.

Note: The notification about new major versions is always displayed.

<!-- This policy is implemented for later usage (PT v1.0 and later) and therefore inactive. (To make it working please update `src/runner/UpdateUtils.cpp`)
#### Disable automatic update checks

This policy allows you to disable automatic update checks running in the background. (The manual check in PT Settings is not affected by this policy.)

If enabled, the automatic update checks are disabled.

If disabled or not configured, the automatic update checks are enabled.
-->
