// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using PowerDisplay.Cli.Options;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Commands;

/// <summary>
/// Builds the <c>powerdisplay</c> root command and its subcommands. <see cref="Program"/>
/// dispatches on <c>parseResult.CommandResult.Command.Name</c> against the
/// <see cref="CliCommandNames"/> constants.
/// </summary>
// 'partial' is required by the CsWinRT analyzer (CsWinRT1028) for AOT/WinRT-ABI compatibility,
// even though there is only one declaration.
public sealed partial class PowerDisplayRootCommand : RootCommand
{
    public PowerDisplayRootCommand()
        : base("PowerToys PowerDisplay - control monitor settings from the command line.")
    {
        AddGlobalOption(CliOptions.Quiet);
        AddGlobalOption(CliOptions.TimeoutSeconds);

        AddCommand(BuildList());
        AddCommand(BuildCapabilities());
        AddCommand(BuildGet());
        AddCommand(BuildSet());
        AddCommand(BuildProfiles());
        AddCommand(BuildApplyProfile());
    }

    private static Command BuildList()
    {
        return new Command(CliCommandNames.List, "Discover attached monitors and print their number, stable id, name, and transport.");
    }

    private static Command BuildCapabilities()
    {
        var cmd = new Command(CliCommandNames.Capabilities, "Print the VCP capabilities advertised by the monitor. Use --setting to restrict to one discrete setting (color-temperature, input-source, power-state).");
        cmd.AddOption(CliOptions.MonitorNumber);
        cmd.AddOption(CliOptions.MonitorId);
        cmd.AddOption(CliOptions.SettingFilter);
        return cmd;
    }

    private static Command BuildGet()
    {
        var cmd = new Command(CliCommandNames.Get, "Read the current value of one or all settings for a monitor.");
        cmd.AddOption(CliOptions.MonitorNumber);
        cmd.AddOption(CliOptions.MonitorId);
        cmd.AddOption(CliOptions.SettingFilter);
        return cmd;
    }

    private static Command BuildSet()
    {
        var cmd = new Command(CliCommandNames.Set, "Apply a single setting to a monitor. Exactly one --<setting> flag must be provided.");
        cmd.AddOption(CliOptions.MonitorNumber);
        cmd.AddOption(CliOptions.MonitorId);
        cmd.AddOption(CliOptions.Brightness);
        cmd.AddOption(CliOptions.Contrast);
        cmd.AddOption(CliOptions.Volume);
        cmd.AddOption(CliOptions.ColorTemperature);
        cmd.AddOption(CliOptions.InputSource);
        cmd.AddOption(CliOptions.PowerState);
        cmd.AddOption(CliOptions.Orientation);
        cmd.AddOption(CliOptions.ConfirmPowerOff);
        return cmd;
    }

    private static Command BuildProfiles()
    {
        return new Command(CliCommandNames.Profiles, "List the saved PowerDisplay profiles (name, monitor count, last modified).");
    }

    private static Command BuildApplyProfile()
    {
        var cmd = new Command(CliCommandNames.ApplyProfile, "Apply a saved profile's per-monitor settings to the connected monitors.");
        cmd.AddArgument(CliOptions.ProfileName);
        return cmd;
    }
}
