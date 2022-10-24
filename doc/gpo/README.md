# Group Policy Objects

Since version 0.64, PowerToys is released on GitHub with GroupPolicyObject files. You can check these releases on https://github.com/microsoft/PowerToys/releases .

## How to install

- Copy "PowerToys.admx" to "C:\Windows\PolicyDefinitions" or equivalent path if Windows is installed in another path.
- Copy "PowerToys.adml" to "C:\Windows\PolicyDefinitions\en-US" or equivalent path if Windows is installed in another path. If you have a different language installed, copy into your language folder as well.

The "Local Group Policy Editor" (gpedit.msc) will now show PowerToys policies under "Local Computer Policy" > "Computer Configuration" > "Administrative Templates" > "Windows Components" > "Microsoft PowerToys".

## Policies

### Configure enabled state

For each utility shipped with PowerToys, there's a "Configure enabled state" policy, which forces and Enabled state for the utility.

If you enable this setting, the utility will be always enabled and the user won't be able to disable it.

If you disable this setting, the utility will be always disabled and the user won't be able to enable it.

If you don't configure this setting, users are able to disable or enable the utility.
