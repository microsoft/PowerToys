# Windows Command Palette

Also known as "Developer Search"

Also known as "WSB" <!-- 🚀💎🚀 -->

Also known as "devcmdpal"

Also known as "Dev Pal"

## Building

First things first: make sure you've opened up the root PowerToys.sln, and restored its NuGet packages. You will get nuget errors if you don't.

Then, to build the Windows Command Palette, you can open up `WindowsCommandPalette.sln`. Projects of interest are:
* `Microsoft.CmdPal.UI.Poc`: This is the main project for the Windows Command Palette. Build and run this to get the command palette..
* `Microsoft.CmdPal.Extensions`: This is the official extension interface.
* `Microsoft.CmdPal.Extensions.Helpers`: This is a C# helper library for creating extensions. This makes writing extensions easier.
* Everything under "Sample Plugins": These are example plugins to demo how to author extensions. Deploy any number of these.
