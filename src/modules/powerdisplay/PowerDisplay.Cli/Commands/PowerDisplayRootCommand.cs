// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using PowerDisplay.Cli.Options;

namespace PowerDisplay.Cli.Commands;

/// <summary>
/// Builds the <c>powerdisplay</c> root command and its subcommands. The
/// instances are captured as properties so <see cref="Program"/> can use reference
/// equality on <c>parseResult.CommandResult.Command</c> for dispatch.
/// </summary>
public sealed partial class PowerDisplayRootCommand : RootCommand
{
    public PowerDisplayRootCommand()
        : base("PowerToys PowerDisplay - control monitor settings from the command line.")
    {
        AddGlobalOption(CliOptions.Json);

        ListCommand = BuildList();
        CapabilitiesCommand = BuildCapabilities();
        GetCommand = BuildGet();
        SetCommand = BuildSet();

        AddCommand(ListCommand);
        AddCommand(CapabilitiesCommand);
        AddCommand(GetCommand);
        AddCommand(SetCommand);
    }

    public Command ListCommand { get; }

    public Command CapabilitiesCommand { get; }

    public Command GetCommand { get; }

    public Command SetCommand { get; }

    private static Command BuildList()
    {
        return new Command("list", "Discover attached monitors and print their number, stable id, name, and transport.");
    }

    private static Command BuildCapabilities()
    {
        var cmd = new Command("capabilities", "Print the VCP capabilities advertised by the monitor.");
        cmd.AddOption(CliOptions.MonitorNumber);
        cmd.AddOption(CliOptions.MonitorId);
        return cmd;
    }

    private static Command BuildGet()
    {
        var cmd = new Command("get", "Read the current value of one or all settings for a monitor.");
        cmd.AddOption(CliOptions.MonitorNumber);
        cmd.AddOption(CliOptions.MonitorId);
        cmd.AddOption(CliOptions.SettingFilter);
        return cmd;
    }

    private static Command BuildSet()
    {
        var cmd = new Command("set", "Apply a single setting to a monitor. Exactly one --<setting> flag must be provided.");
        cmd.AddOption(CliOptions.MonitorNumber);
        cmd.AddOption(CliOptions.MonitorId);
        cmd.AddOption(CliOptions.Brightness);
        cmd.AddOption(CliOptions.Contrast);
        cmd.AddOption(CliOptions.Volume);
        cmd.AddOption(CliOptions.ColorTemperature);
        cmd.AddOption(CliOptions.InputSource);
        cmd.AddOption(CliOptions.PowerState);
        cmd.AddOption(CliOptions.Orientation);
        return cmd;
    }
}
