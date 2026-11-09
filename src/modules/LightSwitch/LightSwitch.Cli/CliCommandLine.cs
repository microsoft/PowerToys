// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using LightSwitch.Cli.Properties;
using LightSwitch.Cli.Protocol;

namespace LightSwitch.Cli;

internal sealed class CliCommandLine
{
    internal static string HelpText => Resources.Help_Text;

    private static readonly ParserConfiguration NoTokenReplacement = new() { ResponseFileTokenReplacer = null };

    private readonly RootCommand _root = new(Resources.Description_Root);
    private readonly Command _schedule = new("schedule", Resources.Description_Schedule);
    private readonly Command _enable = new("enable", Resources.Description_Enable);
    private readonly Command _disable = new("disable", Resources.Description_Disable);
    private readonly Option<string?> _mode = new("--mode") { Description = Resources.Description_Mode, Arity = ArgumentArity.ExactlyOne };

    internal CliCommandLine()
        : this(presentationOnly: false)
    {
    }

    private CliCommandLine(bool presentationOnly)
    {
        ClearDefaults(_root);
        _root.Options.Add(Json);
        _root.Options.Add(Help);
        _root.Options.Add(Version);
        if (presentationOnly)
        {
            return;
        }

        _root.Subcommands.Add(new Command("status", Resources.Description_Status));
        _root.Subcommands.Add(new Command("light", Resources.Description_Light));
        _root.Subcommands.Add(new Command("dark", Resources.Description_Dark));
        _root.Subcommands.Add(new Command("toggle", Resources.Description_Toggle));
        _enable.Options.Add(_mode);
        _schedule.Subcommands.Add(_enable);
        _schedule.Subcommands.Add(_disable);
        _root.Subcommands.Add(_schedule);
    }

    internal Option<bool> Json { get; } = new("--json") { Description = Resources.Description_Json, Recursive = true };

    internal Option<bool> Help { get; } = new("--help", "-h", "-?") { Description = Resources.Description_Help, Recursive = true };

    internal Option<bool> Version { get; } = new("--version") { Description = Resources.Description_Version, Recursive = true };

    internal ParseResult Parse(string[] expandedArgs)
        => _root.Parse(expandedArgs, NoTokenReplacement);

    internal static (string[] Arguments, bool Json, bool Help, bool Version, string? Error) ParsePresentationOptions(string[] args)
    {
        // Expand response files once, without interpreting options. The command parser's
        // tokens cannot distinguish --mode --help from --mode=--help, so preserve the
        // expanded argument text for both parsing passes. Neither pass reopens a file.
        var expansionRoot = new RootCommand { TreatUnmatchedTokensAsErrors = false };
        ClearDefaults(expansionRoot);
        var expansion = expansionRoot.Parse(args);
        string[] expandedArgs = expansion.Tokens.Select(token => token.Value).ToArray();

        // Parse presentation options independently: the pinned parser can otherwise consume
        // --help or --json as a missing --mode value. Use its Boolean parsing in both passes
        // so aliases, explicit false values, duplicates, and the literal -- agree.
        var presentation = new CliCommandLine(presentationOnly: true);
        presentation._root.TreatUnmatchedTokensAsErrors = false;
        var parsed = presentation.Parse(expandedArgs);
        var errors = expansion.Errors.Concat(parsed.Errors).Select(error => error.Message).ToList();
        var assignmentValidator = new CliCommandLine(presentationOnly: true);
        foreach (string argument in expandedArgs)
        {
            if (argument == "--")
            {
                break;
            }

            // The parser leaves an invalid Boolean assignment such as --help=invalid
            // unmatched and treats the option as true. Validate individual tokens using
            // the parser rather than reimplementing its aliases or assignment syntax.
            var token = assignmentValidator.Parse(new[] { argument });
            if (token.Errors.Count != 0 && token.RootCommandResult.Children.OfType<OptionResult>().Any(option => !option.Implicit))
            {
                errors.AddRange(token.Errors.Select(error => error.Message));
            }
        }

        var jsonResult = parsed.GetResult(presentation.Json);
        bool json = (jsonResult is null || !jsonResult.Errors.Any()) && parsed.GetValue(presentation.Json);
        bool onlyJson = parsed.UnmatchedTokens.Count == 0 && !HasDirectives(parsed) &&
            !IsSpecified(parsed, presentation.Help) && !IsSpecified(parsed, presentation.Version) &&
            !parsed.Tokens.Any(token => token.Type == TokenType.DoubleDash);

        return errors.Count == 0
            ? (expandedArgs, json, onlyJson || parsed.GetValue(presentation.Help), parsed.GetValue(presentation.Version), null)
            : (expandedArgs, json, false, false, string.Join(" ", errors.Distinct()));
    }

    // Directives are disabled for this CLI. System.CommandLine 2.0 still tokenizes leading
    // [name] arguments as directives and ignores unknown ones, so report them as unrecognized.
    internal static List<string> GetErrors(ParseResult result)
        => result.Tokens.Where(token => token.Type == TokenType.Directive)
            .Select(token => Resources.Error_UnrecognizedArgument(token.Value))
            .Concat(result.Errors.Select(error => error.Message))
            .ToList();

    private static bool HasDirectives(ParseResult result)
        => result.Tokens.Any(token => token.Type == TokenType.Directive);

    // System.CommandLine 2.0 reports implicit results for options that have default values.
    private static bool IsSpecified(ParseResult result, Option option)
        => result.GetResult(option) is { Implicit: false };

    // System.CommandLine 2.0 adds help, version, and directives to every RootCommand. This CLI
    // owns those presentation options and treats directive-like tokens as ordinary arguments.
    private static void ClearDefaults(RootCommand root)
    {
        root.Options.Clear();
        root.Directives.Clear();
    }

    internal CliRequest CreateRequest(ParseResult result)
    {
        if (result.CommandResult.Command == _enable)
        {
            string? mode = result.GetValue(_mode) switch
            {
                null => null,
                "fixed-hours" => "FixedHours",
                "sunset-to-sunrise" => "SunsetToSunrise",
                "follow-night-light" => "FollowNightLight",
                _ => throw new CliException("INVALID_ARGUMENT", Resources.Error_UnknownScheduleMode),
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
            _ => throw new CliException("INVALID_ARGUMENT", Resources.Error_CommandRequired),
        };
    }
}
