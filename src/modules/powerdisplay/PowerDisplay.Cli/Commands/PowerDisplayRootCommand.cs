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
        // Program handles help/version itself so "-h" stays a valid option value (see InvokeWithDefaultsAsync).
        Options.Clear();

        Options.Add(CliOptions.Quiet);
        Options.Add(CliOptions.Json);

        Subcommands.Add(BuildList());
        Subcommands.Add(BuildCapabilities());
        Subcommands.Add(BuildGet());
        Subcommands.Add(BuildSet());
        Subcommands.Add(BuildProfiles());
        Subcommands.Add(BuildApplyProfile());
        Subcommands.Add(BuildUp());
        Subcommands.Add(BuildDown());
    }

    private static Command BuildList()
    {
        return new Command(CliCommandNames.List, "Discover attached monitors and print their number, stable id, name, and transport.");
    }

    private static Command BuildCapabilities()
    {
        var cmd = new Command(CliCommandNames.Capabilities, "Print the VCP capabilities advertised by the monitor. Use --setting to restrict to one discrete setting (color-temperature, input-source, power-state).");
        cmd.Options.Add(CliOptions.MonitorNumber);
        cmd.Options.Add(CliOptions.MonitorId);
        cmd.Options.Add(CliOptions.SettingFilter);
        return cmd;
    }

    private static Command BuildGet()
    {
        var cmd = new Command(CliCommandNames.Get, "Read the current value of one or all settings for a monitor.");
        cmd.Options.Add(CliOptions.MonitorNumber);
        cmd.Options.Add(CliOptions.MonitorId);
        cmd.Options.Add(CliOptions.SettingFilter);
        return cmd;
    }

    private static Command BuildSet()
    {
        var cmd = new Command(CliCommandNames.Set, "Apply a single setting to a monitor. Exactly one --<setting> flag must be provided.");
        cmd.Options.Add(CliOptions.MonitorNumber);
        cmd.Options.Add(CliOptions.MonitorId);
        cmd.Options.Add(CliOptions.Brightness);
        cmd.Options.Add(CliOptions.Contrast);
        cmd.Options.Add(CliOptions.Volume);
        cmd.Options.Add(CliOptions.ColorTemperature);
        cmd.Options.Add(CliOptions.InputSource);
        cmd.Options.Add(CliOptions.PowerState);
        cmd.Options.Add(CliOptions.Orientation);
        cmd.Options.Add(CliOptions.ConfirmPowerOff);
        return cmd;
    }

    private static Command BuildProfiles()
    {
        return new Command(CliCommandNames.Profiles, "List the saved PowerDisplay profiles (name, monitor count, last modified).");
    }

    private static Command BuildApplyProfile()
    {
        var cmd = new Command(CliCommandNames.ApplyProfile, "Apply a saved profile's per-monitor settings to the connected monitors.");
        cmd.Arguments.Add(CliOptions.ProfileId);
        return cmd;
    }

    private static Command BuildUp()
    {
        var cmd = new Command(CliCommandNames.Up, "Raise a continuous setting (brightness, contrast, or volume) relative to its current value. Exactly one --<setting> flag must be provided.");
        AddAdjustOptions(cmd);
        return cmd;
    }

    private static Command BuildDown()
    {
        var cmd = new Command(CliCommandNames.Down, "Lower a continuous setting (brightness, contrast, or volume) relative to its current value. Exactly one --<setting> flag must be provided.");
        AddAdjustOptions(cmd);
        return cmd;
    }

    private static void AddAdjustOptions(Command cmd)
    {
        cmd.Options.Add(CliOptions.MonitorNumber);
        cmd.Options.Add(CliOptions.MonitorId);
        cmd.Options.Add(CliOptions.BrightnessFlag);
        cmd.Options.Add(CliOptions.ContrastFlag);
        cmd.Options.Add(CliOptions.VolumeFlag);
        cmd.Options.Add(CliOptions.Step);
    }
}
