// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;
using LightSwitch.Cli.Protocol;

namespace LightSwitch.Cli;

internal sealed class CliCommandLine
{
    internal const string HelpText = """
        PowerToys Light Switch
        Control the running Light Switch service. Enable Light Switch in PowerToys first.

        Usage:
          PowerToys.LightSwitch.CLI.exe <command> [options]

        Commands:
          status                         Show the current themes and scheduling state.
          light                          Apply the light theme to the configured targets.
          dark                           Apply the dark theme to the configured targets.
          toggle                         Toggle the configured theme targets.
          schedule enable [--mode MODE]  Enable scheduling, optionally selecting a mode.
          schedule disable               Disable scheduling.

        Schedule modes:
          fixed-hours, sunset-to-sunrise, follow-night-light

        Options:
          --json                         Write one JSON object, including errors.
          -h, --help                     Show help without contacting the service.
          --version                      Show the CLI version without contacting the service.

        JSON command output uses the service response envelope: version, success, state or error.
        JSON help/version output is local: version, success, and help or cliVersion.
        """;

    private static readonly string[] HelpAliases = { "--help", "-h", "-?" };

    private readonly RootCommand _root = new("Control the running PowerToys Light Switch service.");
    private readonly Command _schedule = new("schedule", "Enable or disable scheduling.");
    private readonly Command _enable = new("enable", "Enable scheduling.");
    private readonly Command _disable = new("disable", "Disable scheduling.");
    private readonly Option<string?> _mode = new("--mode", "fixed-hours, sunset-to-sunrise, or follow-night-light") { Arity = ArgumentArity.ExactlyOne };

    internal CliCommandLine()
    {
        _root.AddGlobalOption(Json);
        _root.AddGlobalOption(Help);
        _root.AddGlobalOption(Version);
        _root.AddCommand(new Command("status", "Show themes and scheduling state."));
        _root.AddCommand(new Command("light", "Apply the light theme."));
        _root.AddCommand(new Command("dark", "Apply the dark theme."));
        _root.AddCommand(new Command("toggle", "Toggle the configured theme targets."));
        _enable.AddOption(_mode);
        _schedule.AddCommand(_enable);
        _schedule.AddCommand(_disable);
        _root.AddCommand(_schedule);
    }

    internal Option<bool> Json { get; } = new("--json", "Write one JSON object.");

    internal Option<bool> Help { get; } = new(HelpAliases, "Show help.");

    internal Option<bool> Version { get; } = new("--version", "Show the CLI version.");

    internal ParseResult Parse(string[] args) => new Parser(_root).Parse(args);

    // The pinned parser can consume an option-looking token as the preceding --mode value.
    // These global presentation flags still express intent, but only before the literal --.
    internal static bool HasFlag(string[] args, string flag)
    {
        foreach (string argument in args)
        {
            if (argument == "--")
            {
                break;
            }

            if (argument == flag)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsHelpRequest(string[] args)
    {
        bool onlyJson = true;
        foreach (string argument in args)
        {
            if (argument != "--json")
            {
                onlyJson = false;
                break;
            }
        }

        return onlyJson || HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "-?");
    }

    internal CliRequest CreateRequest(ParseResult result)
    {
        if (result.CommandResult.Command == _enable)
        {
            string? mode = result.GetValueForOption(_mode) switch
            {
                null => null,
                "fixed-hours" => "FixedHours",
                "sunset-to-sunrise" => "SunsetToSunrise",
                "follow-night-light" => "FollowNightLight",
                _ => throw new CliException("INVALID_ARGUMENT", "Unknown schedule mode. Use fixed-hours, sunset-to-sunrise, or follow-night-light."),
            };

            return new CliRequest { Command = "schedule-enable", Mode = mode };
        }

        if (result.CommandResult.Command == _disable)
        {
            return new CliRequest { Command = "schedule-disable" };
        }

        return result.CommandResult.Command.Name switch
        {
            "status" or "light" or "dark" or "toggle" => new CliRequest { Command = result.CommandResult.Command.Name },
            _ => throw new CliException("INVALID_ARGUMENT", "Specify a command. The schedule command requires enable or disable."),
        };
    }
}
