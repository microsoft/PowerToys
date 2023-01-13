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
