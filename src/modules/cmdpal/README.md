# Windows Command Palette

The next version of PT Run.

## Building

First things first: make sure you've opened up the root PowerToys.sln, and restored its NuGet packages. You will get nuget errors if you don't.

Then, to build the Windows Command Palette, you can open up `WindowsCommandPalette.sln`. Projects of interest are:
* `Microsoft.CmdPal.UI.Poc`: This is the main project for the Windows Command Palette. Build and run this to get the command palette..
* `Microsoft.CommandPalette.Extensions`: This is the official extension interface.
* `Microsoft.CommandPalette.Extensions.Toolkit`: This is a C# helper library for creating extensions. This makes writing extensions easier.
* Everything under "SampleExtensions": These are example plugins to demo how to author extensions. Deploy any number of these.
