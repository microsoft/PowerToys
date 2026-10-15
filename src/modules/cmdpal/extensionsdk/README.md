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

## CI build toolchain

The SDK job installs the exact .NET SDK selected by `dotNetSdkVersion` in
`.pipelines/v2/templates/job-build-sdk.yml`. Keep that version aligned with the
product job's `exactVersion` and `DotNetRuntimePackageVersion` in
`Directory.Packages.props`. Official SDK builds use the same VS18 image selection
as the product build instead of the agent pool's default image.

`BuildSDKHelper.ps1 -ExpectedDotNetSdkVersion <version>` checks the SDK actually
resolved by MSBuild before restoring packages. A mismatched SDK or failed restore,
architecture build, or package command stops the script immediately. Omitting the
version check preserves local SDK selection; it does not change the output
package version specified by `VersionOfSDK`.
