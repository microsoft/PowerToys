# Command Palette Extension Toolkit

The C# toolkit for building your own extension for the Command Palette.

The quickest way to get started building an extension is to install the Command
Palette, and use the "Create a new extension" command. That will set up a
project for you, with the packaging, dependencies, and basic program structure
ready to go.

To view the full docs, you can head over to [our docs site](https://aka.ms/cmdpalextensions-devdocs)

There are samples of just about everything you can do in [the samples project].
Head over there to see basic usage of the APIs.

[the samples project]: https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal/ext/SamplePagesExtension

## Toolkit resource metadata

The Toolkit declares its neutral resource language in `Properties/AssemblyInfo.cs`.
Official builds use Microsoft.Windows.Terminal.Versioning to rewrite assembly
metadata instead of the .NET SDK's generator; the rewriter preserves this explicit
attribute. `GenerateNeutralResourcesLanguageAttribute` is disabled in the Toolkit
project to prevent duplicate attributes in ordinary SDK builds. Keep these settings
together so both build paths retain the resource language without suppressing CA1824.
