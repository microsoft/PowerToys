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

### Install & Build PowerToys

1. Follow the install and build instructions for [PowerToys](https://github.com/microsoft/PowerToys/tree/main/doc/devdocs#compiling-powertoys)

### Load & Build

1. In Visual Studio, in the Solution Explorer Pane, confirm that all of the files/projects in `src\modules\CommandPalette` and `src\common\CalculatorEngineCommon` do not have `(unloaded)` on the right side
    1. If any file has `(unloaded)`, right click on file and select `Reload Project`
1. Now you can right click on one of the project below to `Build` and then `Deploy`:

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


