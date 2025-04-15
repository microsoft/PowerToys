# What is it

We would like to enable our users to use [`winget configure`](https://learn.microsoft.com/en-us/windows/package-manager/winget/configure) command to install PowerToys and configure its settings with a [Winget  configuration file](https://learn.microsoft.com/en-us/windows/package-manager/configuration/create). For example:

```yaml
properties:
  resources:
    - resource: Microsoft.WinGet.DSC/WinGetPackage
      directives:
        description: Install PowerToys
        allowPrerelease: true
      settings:
        id: PowerToys (Preview)
        source: winget

    - resource: PowerToysConfigure
      directives:
        description: Configure PowerToys
      settings:
        ShortcutGuide:
          Enabled: false
          OverlayOpacity: 1
        FancyZones:
          Enabled: true
          FancyzonesEditorHotkey: "Shift+Ctrl+Alt+F"
  configurationVersion: 0.2.0
```

This should install PowerToys and make `PowerToysConfigure` resource available. We can use it in the same file.

# How it works

`PowerToysConfigure` is a [class-based DSC resource](https://learn.microsoft.com/en-us/powershell/dsc/concepts/class-based-resources?view=dsc-2.0). It looks up whether each setting was specified or not by checking whether it's `$null` or `0` for `enum`s and invokes `PowerToys.Settings.exe` with the updated value like so:
```
PowerToys.Settings.exe set <ModuleName>.<SettingName> <SettingValue>
```

So for the example the config above should perform 3 following invocations:
```
PowerToys.Settings.exe set ShortcutGuide.Enabled false
PowerToys.Settings.exe set FancyZones.Enabled true
PowerToys.Settings.exe set FancyZones.FancyzonesEditorHotkey "Shift+Ctrl+Alt+F"
```

`PowerToys.Settings` uses dotnet reflection capabilities to determine `SettingName` type and tries to convert the supplied `SettingValue` string accordingly. We use `ICmdReprParsable` for custom setting types.


# How DSC is implemented

We use `PowerToys.Settings.DSC.Schema.Generator` to generate the bulk of `PowerToysConfigure.psm1` and `PowerToysConfigure.psd1` files. It also uses dotnet reflection capabilities to inspect `PowerToys.Settings.UI.Lib.dll` assembly and generate properties for the modules we have. The actual generation is done as a `PowerToys.Settings.DSC.Schema.Generator.csproj` post-build action.

# Debugging DSC resources

First, make sure that PowerShell 7.4+ is installed. Then make sure that you have DSC installed:

```ps
Install-Module -Name PSDesiredStateConfiguration -RequiredVersion 2.0.7
```

After that, start a new `pwsh` session and `cd` to `src\dsc\Microsoft.PowerToys.Configure\Generated` directory. From there, you should execute:
```ps
$env:PSModulePath += ";$pwd"
```

You should have the generated `Microsoft.PowerToys.Configure.psm1` and `Microsoft.PowerToys.Configure.psd1` files inside the `src\dsc\Microsoft.PowerToys.Configure\Generated\Microsoft.PowerToys.Configure\0.0.1\` folder.

This will allow DSC to discover our DSC Resource module. See [PSModulePath](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_psmodulepath?view=powershell-7.4#long-description) for more info.

If everything works, you should see that your module is discovered by executing the following command:

```ps
Get-Module -ListAvailable | grep PowerToys
```

The resource itself should also be available:
```ps
Get-DSCResource | grep PowerToys
```

Otherwise, you can force-import the module to diagnose issues:

```
Import-Module .\Microsoft.PowerToys.Configure.psd1 
```

If it's imported successfully, you could also try to invoke it directly:

```ps
Invoke-DscResource -Name PowerToysConfigure -Method Set -ModuleName Microsoft.PowerToys.Configure -Property @{ Debug = $true; Awake = @{ Enabled = $false; Mode = "TIMED"; IntervalMinutes = "10" } }
```

Note that we've supplied `Debug` option, so a `%TEMP\PowerToys.DSC.TestConfigure.txt` is created with the supplied properties, a current timestamp, and other debug output.

Finally, you can test it with winget by invoking it as such:

```ps
winget configure .\configuration.winget --accept-configuration-agreements --disable-interactivity
```