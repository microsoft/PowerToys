# ![cmdpal logo](./Microsoft.CmdPal.UI/Assets/Stable/StoreLogo.scale-100.png) Command Palette

Windows Command Palette ("CmdPal") is the next iteration of PowerToys Run. With extensibility at its core, the Command Palette is your one-stop launcher to start _anything_.

By default, CmdPal is bound to <kbd>Win+Alt+Space</kbd>.

## Creating an extension

The fastest way to get started is just to run the "Create extension" command in the palette itself. That'll prompt you for a project name and a Display Name, and where you want to place your project. Then just open the `sln` it produces. You should be ready to go 🙂.

The official API documentation can be found [on this docs site](https://learn.microsoft.com/windows/powertoys/command-palette/extensibility-overview).

We've also got samples, so that you can see how the APIs in-action.

* We've got [generic samples] in the repo
* We've got [real samples] in the repo too
* And we've even got [real extensions that we've "shipped" already]

> [!info]
> The Command Palette is currently in preview. Many features of the API are not yet fully implemented. We may introduce breaking API changes before CmdPal itself is v1.0.0

## Building CmdPal

### Install prerequisites

1. Install the prerequisites from the [PowerToys build documentation](https://github.com/microsoft/PowerToys/tree/main/doc/devdocs#compiling-powertoys). You do not need to build the full PowerToys solution to build CmdPal.

### Load, build, and deploy

1. Open `CommandPalette.slnf` in Visual Studio and select `Debug` with the platform matching the machine (`x64` or `ARM64`).
1. In Solution Explorer, confirm the CmdPal projects and their required shared-library projects are loaded. If a required project is marked `(unloaded)`, right-click it and select `Reload Project`.
1. Right-click `Microsoft.CmdPal.UI` and select `Build`, then `Deploy`.
1. Launch Command Palette from its normal Start menu entry.

For normal iteration, use `Build`, not `Rebuild`, and build the narrowest project needed instead of the whole solution filter. Visual Studio's `Deploy` registers the loose build output as a development package; it does not require generating a full MSIX, installing a signing certificate, or uninstalling the previous development package.

Projects of interest are:

* `Microsoft.CmdPal.UI`: This is the main project for CmdPal. Build and run this to get the CmdPal.
* `Microsoft.CommandPalette.Extensions`: This is the official extension interface. 
  * This is designed to be language-agnostic. Any programming language which supports implementing WinRT interfaces should be able to implement the WinRT interface. 
* `Microsoft.CommandPalette.Extensions.Toolkit`: This is a C# helper library for creating extensions. This makes writing extensions easier.
* Everything under "SampleExtensions": These are example plugins to demo how to author extensions. Deploy any number of these, to get a feel for how the extension API works.

### Footnotes and other links

* [Initial SDK Spec]

[^1]: you'll almost definitely want to do a `git init` in that directory, and set up a git repo to track your work. 


[Initial SDK Spec]: ./doc/initial-sdk-spec/initial-sdk-spec.md
[generic samples]: ./ext/SamplePagesExtension 
[real samples]: ./ext/ProcessMonitorExtension
[real extensions that we've "shipped" already]: https://github.com/zadjii/CmdPalExtensions/blob/main/src/extensions


