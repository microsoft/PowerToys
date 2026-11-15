# Command Palette localization

Use the language selector to test packaged translations, and the pseudo-localization scripts to check that UI text comes from resources and displays accented characters correctly. The scripts run locally and require PowerShell 7 or later.

## Choose a language

Open Command Palette settings, go to **General > Language**, select a language, then use **Restart now** to apply it. **Windows display language** restores the default selection. Language names appear in their native language.

For pseudo-localization, select the `qps-PLOC` language, displayed as **qps (Ploc)**. This option is available in local builds and is hidden when built with `CIBuild=true`. Generate the resources and rebuild first; the selector alone does not create translations.

## Edit source strings

Keep changes in the English source resources. The generator reads neutral RESX files and RESW files directly inside an `en-US` directory (case-insensitive).

| Resource type | Example source path, relative to the module | Generated output with the default culture |
| --- | --- | --- |
| RESX | `Microsoft.CmdPal.UI.ViewModels/Properties/Resources.resx` | `Microsoft.CmdPal.UI.ViewModels/Properties/Resources.qps-ploc.resx` |
| RESW | `Microsoft.CmdPal.UI/Strings/en-us/Resources.resw` | `Microsoft.CmdPal.UI/Strings/qps-ploc/Resources.resw` |

Directory scans skip translated resources, existing pseudo-localized resources, and files below `bin` or `obj`. Source files remain unchanged. Existing output for the selected culture is overwritten, so regenerate after editing English strings instead of editing the generated copies.

## Generate test resources

From the repository root, enter the module directory and generate resources for the whole module:

```powershell
Set-Location .\src\modules\cmdpal
pwsh -NoProfile -File .\Invoke-PseudoLocalization.ps1 -Path .
```

Run the remaining commands from this directory. To limit generation, pass a project directory or a single source file:

```powershell
pwsh -NoProfile -File .\Invoke-PseudoLocalization.ps1 -Path .\Microsoft.CmdPal.UI\Strings\en-us\Resources.resw
```

The defaults are `-Mode diacritics` and `-Culture qps-ploc`. Use `-Mode` to choose a transformation:

| Mode | Example result for `Hello {0}` |
| --- | --- |
| `diacritics` | `Ĥēĺĺō {0}` |
| `brackets` | `[Hello {0}]` |
| `xs` | `Xxxxx {0}` |

Format placeholders such as `{0}` and escaped braces (`{{` and `}}`) are preserved. `-Culture` changes the output suffix or directory; it does not add a language to the selector or implement a different transformation.

After generation, [build and deploy Command Palette](../README.md#building-cmdpal), including the projects whose resources changed. Select the pseudo language and restart. Check the affected screens for text that stayed in English unexpectedly, broken formatting, or missing resources.

## Preserve text with resource comments

`{Locked}` follows the localization-comment convention described in [Microsoft's resource guidance](https://learn.microsoft.com/en-us/globalization/internationalization/externalize-resources#separate-localizable-and-nonlocalizable-resources). Microsoft's [VS Code localization guidance](https://github.com/microsoft/vscode-livepreview/issues/221) calls these tool-interpreted annotations "functional commenting". The pseudo-localizer explicitly interprets them when generating resources.

Add a directive to a resource entry's `<comment>` element. This local script supports the forms below, with double quotes required around literal fragments:

| Directive | Effect in the pseudo-localizer |
| --- | --- |
| `{Locked}` | Leaves the entire value unchanged. |
| `{Locked=qps-ploc,qps-plocm}` | Leaves the entire value unchanged when the output culture matches a listed culture. |
| `{Locked="Windows", "PowerToys"}` | Preserves those exact, case-sensitive fragments while transforming the surrounding text. |

For example:

```xml
<data name="OpenWindows" xml:space="preserve">
  <value>Open Windows {0}</value>
  <comment>{Locked="Windows"}</comment>
</data>
```

In `diacritics` mode, this becomes `Ōṗēń Windows {0}` with the locked name and placeholder preserved.

## Remove test resources

Switch back to **Windows display language** or another packaged language and restart. From the module directory, remove the generated source files:

```powershell
pwsh -NoProfile -File .\Clear-PseudoLocalization.ps1 -Path .
```

Cleanup removes matching RESX and RESW files for the selected culture; it does not track which invocation created them. Directory scans preserve `bin`, `obj`, and unrelated files. Use the same `-Culture` value if generation used a custom one.

For narrower cleanup, pass the original source file to delete its paired output, or a directory containing the generated files. An `en-US` directory does not contain the generated sibling `qps-ploc` directory. Cleanup leaves compiled and deployed resources untouched; rebuild and redeploy to refresh the app.

## Direct CLI and help

The shared script provides equivalent commands:

```powershell
pwsh -NoProfile -File .\pseudolocalizer.ps1 generate .
pwsh -NoProfile -File .\pseudolocalizer.ps1 clear .
Get-Help .\Invoke-PseudoLocalization.ps1 -Full
Get-Help .\Clear-PseudoLocalization.ps1 -Full
```

Both entry points return a nonzero exit code on failure. In PowerShell, check `$LASTEXITCODE` before continuing to a build.
