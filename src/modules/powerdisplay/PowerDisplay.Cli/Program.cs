// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Cli.Commands;
using PowerDisplay.Cli.Ipc;
using PowerDisplay.Cli.Options;
using PowerDisplay.Cli.Output;
using PowerDisplay.Cli.Properties;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli;

public static class Program
{
    private const int DefaultTimeoutSeconds = 30;

    // Canonical args for routing any version request through the default invocation pipeline's
    // version renderer (static readonly to satisfy CA1861 — the array is passed, never mutated).
    private static readonly string[] VersionArgs = { "--version" };

    // Stable program identifier stamped into the `command` field of root-level error envelopes.
    // For an error that resolves to the RootCommand (e.g. an unrecognized top-level option),
    // CommandResult.Command.Name is the auto-derived executable name ("PowerToys.PowerDisplay.Cli");
    // mapping it to this constant keeps the machine-readable field a documented command identifier.
    private const string ProgramCommandLabel = "powerdisplay";

    // The command name for the error envelope: a real subcommand keeps its name; a root-level error
    // is reported as the program label instead of leaking the binary name.
    private static string CommandLabelFor(ParseResult parseResult)
        => parseResult.CommandResult.Command is RootCommand
            ? ProgramCommandLabel
            : parseResult.CommandResult.Command.Name;

    public static async Task<int> Main(string[] args)
    {
        // Emit UTF-8 so non-ASCII glyphs in human-readable output (the → arrow, ° degree sign,
        // … ellipsis) and any UTF-8 JSON render correctly instead of as '?' on legacy code pages.
        TrySetUtf8Output();

        var root = new PowerDisplayRootCommand();
        var parser = new Parser(root);
        var parseResult = parser.Parse(args);

        // Help / version short-circuit through the default invocation pipeline (which owns
        // the version + help renderers). Done BEFORE the logger is created so a pure
        // --help/--version invocation has no file-system side effects.
        if (parseResult.Tokens.Count == 0 || HasHelpToken(parseResult))
        {
            return await root.InvokeAsync(args);
        }

        if (IsVersionRequest(parseResult))
        {
            // Route through the canonical root `--version` invocation rather than re-invoking the
            // original args. This also covers `apply-profile --version`, where the version token was
            // greedily bound to the profile-name argument (see IsVersionRequest) and replaying args
            // would instead dispatch "apply a profile literally named --version".
            return await root.InvokeAsync(VersionArgs);
        }

        var quiet = parseResult.GetValueForOption(CliOptions.Quiet);
        ICliOutput output = new TextCliOutput(quiet);

        if (parseResult.Errors.Count > 0)
        {
            // System.CommandLine can report several parse errors for one bad invocation; collapse
            // them into a single envelope so consumers always receive exactly one parseable
            // object (text output) instead of N concatenated ones.
            output.WriteError(BuildParseErrorResult(
                CommandLabelFor(parseResult),
                parseResult.Errors.Select(e => e.Message)));

            return CliExitCodes.ArgumentError;
        }

        // Logs go to %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs\<version>.
        // Guard initialization: an unwritable log path (locked profile, full disk, policy
        // redirection) creates the directory / trace listener eagerly and would otherwise throw
        // here — OUTSIDE the try below — crashing with a raw stack trace and bypassing the
        // single-envelope error contract. The requested operation does not need the log file,
        // so degrade to no file listener and continue.
        try
        {
            Logger.InitializeLogger("\\PowerDisplay\\Logs");
        }
        catch (Exception)
        {
        }

        var timeoutSeconds = parseResult.GetValueForOption(CliOptions.TimeoutSeconds) ?? DefaultTimeoutSeconds;

        // No --timeout (0/unset) → effectively unbounded. int.MaxValue ms (~24 days) is used rather
        // than Timeout.InfiniteTimeSpan because this span is later cast to int milliseconds for
        // NamedPipeClientStream.ConnectAsync, which rejects a negative (infinite) value.
        var timeout = timeoutSeconds > 0
            ? TimeSpan.FromSeconds(timeoutSeconds)
            : TimeSpan.FromMilliseconds(int.MaxValue);
        var timedOut = false;
        Timer? timeoutTimer = null;
        ConsoleCancelEventHandler? cancelHandler = null;

        using var cts = new CancellationTokenSource();
        try
        {
            // Captured in a local so the finally can unsubscribe it. Console.CancelKeyPress is a
            // process-global static event; leaving the handler attached would leak a closure over a
            // disposed cts across repeated DispatchAsync/Main invocations (e.g. in tests).
            cancelHandler = (_, e) =>
            {
                e.Cancel = true;
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            };
            Console.CancelKeyPress += cancelHandler;

            if (timeoutSeconds > 0)
            {
                // `timedOut` is set on the timer thread before cts.Cancel(); the cancel→token
                // propagation establishes happens-before, so the catch below reads it reliably.
                timeoutTimer = new Timer(
                    _ =>
                    {
                        timedOut = true;
                        try
                        {
                            cts.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    },
                    null,
                    TimeSpan.FromSeconds(timeoutSeconds),
                    Timeout.InfiniteTimeSpan);
            }

            var dispatcher = new IpcDispatcher(output, timeout);

            return await DispatchAsync(root, args, parseResult, dispatcher, output, cts.Token);
        }
        catch (OperationCanceledException)
        {
            output.WriteError(BuildTimeoutErrorResult(CommandLabelFor(parseResult), timedOut, timeoutSeconds));
            return CliExitCodes.Timeout;
        }
        catch (Exception ex)
        {
            Logger.LogError($"PowerDisplay CLI failed: {ex}");
            output.WriteError(new CliErrorResult
            {
                Command = CommandLabelFor(parseResult),
                Error = new CliError
                {
                    Code = CliErrorCodes.InternalError,
                    Message = Resources.Error_UnexpectedError(ex.Message),
                },
            });
            return CliExitCodes.InternalError;
        }
        finally
        {
            if (cancelHandler is not null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }

            timeoutTimer?.Dispose();
        }
    }

    /// <summary>
    /// Routes the parsed command to the appropriate IPC send-and-render helper.
    /// Pure-syntactic validation (setting count, setting name) is checked here before
    /// any IPC round-trip. Extracted as a static method so tests can drive it directly.
    /// </summary>
    internal static async Task<int> DispatchAsync(
        PowerDisplayRootCommand root,
        string[] args,
        ParseResult parseResult,
        IpcDispatcher dispatcher,
        ICliOutput output,
        CancellationToken cancellationToken)
    {
        // Dispatch on the parsed command's name against the shared CliCommandNames constants,
        // so no shared reference-equality singletons are required.
        switch (parseResult.CommandResult.Command.Name)
        {
            // ── list ──────────────────────────────────────────────────────────
            case CliCommandNames.List:
                return await dispatcher.SendListAsync(CliRequestBuilder.BuildList(), cancellationToken);

            // ── get ───────────────────────────────────────────────────────────
            case CliCommandNames.Get:
            {
                var monitorNumber = parseResult.GetValueForOption(CliOptions.MonitorNumber);
                var monitorId = parseResult.GetValueForOption(CliOptions.MonitorId);
                var settingFilter = parseResult.GetValueForOption(CliOptions.SettingFilter);

                // CLI-side syntactic validation: reject unknown --setting names here so the error
                // is surfaced without a round-trip and matches the existing ARGUMENT_ERROR (7) shape.
                if (settingFilter is not null
                    && System.Array.IndexOf(CliSettingNames.All, settingFilter.ToLowerInvariant()) < 0)
                {
                    output.WriteError(ArgumentError(
                        CliCommandNames.Get,
                        Resources.Error_UnknownSetting(settingFilter),
                        Resources.Hint_ValidSettings(string.Join(", ", CliSettingNames.All))));
                    return CliExitCodes.ArgumentError;
                }

                WarnIfMonitorNumberIgnored(output, monitorNumber, monitorId);

                return await dispatcher.SendGetAsync(
                    CliRequestBuilder.BuildGet(monitorNumber, monitorId, settingFilter),
                    cancellationToken);
            }

            // ── set ───────────────────────────────────────────────────────────
            case CliCommandNames.Set:
            {
                var inputs = new SetCommandInputs
                {
                    MonitorNumber = parseResult.GetValueForOption(CliOptions.MonitorNumber),
                    MonitorId = parseResult.GetValueForOption(CliOptions.MonitorId),
                    Brightness = parseResult.GetValueForOption(CliOptions.Brightness),
                    Contrast = parseResult.GetValueForOption(CliOptions.Contrast),
                    Volume = parseResult.GetValueForOption(CliOptions.Volume),
                    ColorTemperature = parseResult.GetValueForOption(CliOptions.ColorTemperature),
                    InputSource = parseResult.GetValueForOption(CliOptions.InputSource),
                    PowerState = parseResult.GetValueForOption(CliOptions.PowerState),
                    Orientation = parseResult.GetValueForOption(CliOptions.Orientation),
                    ConfirmPowerOff = parseResult.GetValueForOption(CliOptions.ConfirmPowerOff),
                };

                // CLI-side syntactic validation: exactly one setting must be specified.
                var selected = SetCommand.CountSelectedSettings(inputs);
                if (selected == 0)
                {
                    output.WriteError(ArgumentError(CliCommandNames.Set, Resources.Error_NoSettingSpecified));
                    return CliExitCodes.ArgumentError;
                }

                if (selected > 1)
                {
                    output.WriteError(ArgumentError(CliCommandNames.Set, Resources.Error_OnlyOneSetting, Resources.Hint_OnlyOneSetting));
                    return CliExitCodes.ArgumentError;
                }

                WarnIfMonitorNumberIgnored(output, inputs.MonitorNumber, inputs.MonitorId);

                return await dispatcher.SendSetAsync(CliRequestBuilder.BuildSet(inputs), cancellationToken);
            }

            // ── up / down ─────────────────────────────────────────────────────
            case CliCommandNames.Up:
            case CliCommandNames.Down:
            {
                var inputs = new AdjustCommandInputs
                {
                    MonitorNumber = parseResult.GetValueForOption(CliOptions.MonitorNumber),
                    MonitorId = parseResult.GetValueForOption(CliOptions.MonitorId),
                    Brightness = parseResult.GetValueForOption(CliOptions.BrightnessFlag),
                    Contrast = parseResult.GetValueForOption(CliOptions.ContrastFlag),
                    Volume = parseResult.GetValueForOption(CliOptions.VolumeFlag),
                    Step = parseResult.GetValueForOption(CliOptions.Step),
                };

                var commandName = parseResult.CommandResult.Command.Name;

                // CLI-side syntactic validation: exactly one continuous setting must be specified.
                var selected = AdjustCommand.CountSelectedSettings(inputs);
                if (selected == 0)
                {
                    output.WriteError(ArgumentError(commandName, Resources.Error_NoAdjustSettingSpecified));
                    return CliExitCodes.ArgumentError;
                }

                if (selected > 1)
                {
                    output.WriteError(ArgumentError(commandName, Resources.Error_OnlyOneSetting, Resources.Hint_OnlyOneSetting));
                    return CliExitCodes.ArgumentError;
                }

                WarnIfMonitorNumberIgnored(output, inputs.MonitorNumber, inputs.MonitorId);

                return await dispatcher.SendAdjustAsync(CliRequestBuilder.BuildAdjust(commandName, inputs), cancellationToken);
            }

            // ── capabilities ──────────────────────────────────────────────────
            case CliCommandNames.Capabilities:
            {
                var monitorNumber = parseResult.GetValueForOption(CliOptions.MonitorNumber);
                var monitorId = parseResult.GetValueForOption(CliOptions.MonitorId);
                var settingFilter = parseResult.GetValueForOption(CliOptions.SettingFilter);

                WarnIfMonitorNumberIgnored(output, monitorNumber, monitorId);

                // An out-of-range --setting (not one of the 3 discrete settings) is validated app-side
                // and comes back as a single ARGUMENT_ERROR envelope.
                return await dispatcher.SendCapabilitiesAsync(
                    CliRequestBuilder.BuildCapabilities(monitorNumber, monitorId, settingFilter),
                    cancellationToken);
            }

            // ── profiles ──────────────────────────────────────────────────────
            case CliCommandNames.Profiles:
                return await dispatcher.SendProfilesAsync(CliRequestBuilder.BuildProfiles(), cancellationToken);

            // ── apply-profile ─────────────────────────────────────────────────
            case CliCommandNames.ApplyProfile:
            {
                var profileName = parseResult.GetValueForArgument(CliOptions.ProfileName);
                return await dispatcher.SendApplyProfileAsync(
                    CliRequestBuilder.BuildApplyProfile(profileName),
                    cancellationToken);
            }

            default:
                return await root.InvokeAsync(args);
        }
    }

    // Carry-forward: the app discards -n when -i is also supplied; surface that warning
    // CLI-side without a round-trip. Shared by the get/set/capabilities branches.
    private static void WarnIfMonitorNumberIgnored(ICliOutput output, int? monitorNumber, string? monitorId)
    {
        if (monitorNumber.HasValue && !string.IsNullOrEmpty(monitorId))
        {
            output.WriteWarning(Resources.Warn_MonitorNumberIgnored(monitorNumber.GetValueOrDefault()));
        }
    }

    public static bool HasHelpToken(ParseResult parseResult)
        => parseResult.UnmatchedTokens.Any(IsHelpToken)
            || HelpBoundToProfileNameArgument(parseResult);

    private static bool IsHelpToken(string token)
        => token is "--help" or "-h" or "-?" or "/?";

    // The `apply-profile <name>` positional argument greedily captures a "--help" token (it binds to
    // the argument, so it never reaches UnmatchedTokens). Without this, `apply-profile --help` would
    // be dispatched as "apply a profile literally named --help" instead of printing help like every
    // other command. Option *values* that look like help (e.g. `set -i -h`) are unaffected: they are
    // matched to an option, not to this argument.
    private static bool HelpBoundToProfileNameArgument(ParseResult parseResult)
        => parseResult.CommandResult.Command.Name == CliCommandNames.ApplyProfile
            && IsHelpToken(parseResult.GetValueForArgument(CliOptions.ProfileName) ?? string.Empty);

    public static bool HasVersionToken(ParseResult parseResult)
        => parseResult.UnmatchedTokens.Any(t => t == "--version");

    public static bool IsVersionRequest(ParseResult parseResult)
        => (HasVersionToken(parseResult) && parseResult.CommandResult.Command is RootCommand)
            || VersionBoundToProfileNameArgument(parseResult);

    // Mirror of HelpBoundToProfileNameArgument for "--version": the `apply-profile <name>` positional
    // argument greedily captures a "--version" token (it binds to the argument, so it never reaches
    // UnmatchedTokens and IsVersionRequest's RootCommand gate cannot see it). Without this,
    // `apply-profile --version` would be dispatched as "apply a profile literally named --version".
    private static bool VersionBoundToProfileNameArgument(ParseResult parseResult)
        => parseResult.CommandResult.Command.Name == CliCommandNames.ApplyProfile
            && parseResult.GetValueForArgument(CliOptions.ProfileName) == "--version";

    /// <summary>
    /// Collapses one or more System.CommandLine parse-error messages into a single
    /// <see cref="CliErrorResult"/> so the error stream stays a single parseable envelope.
    /// </summary>
    public static CliErrorResult BuildParseErrorResult(string command, IEnumerable<string> messages)
    {
        var combined = string.Join("; ", messages.Where(m => !string.IsNullOrWhiteSpace(m)));
        return ArgumentError(command, combined.Length == 0 ? Resources.Error_InvalidArguments : combined);
    }

    // Single ARGUMENT_ERROR envelope shape, shared by the syntactic-validation sites in
    // DispatchAsync and by BuildParseErrorResult. Setting/Hint default to null (omitted from JSON).
    private static CliErrorResult ArgumentError(string command, string message, string? hint = null)
        => new()
        {
            Command = command,
            Error = new CliError
            {
                Code = CliErrorCodes.ArgumentError,
                Message = message,
                Hint = hint,
            },
        };

    // Shared TIMEOUT envelope for both the OperationCanceledException catch path.
    private static CliErrorResult BuildTimeoutErrorResult(string command, bool timedOut, int timeoutSeconds)
        => new()
        {
            Command = command,
            Error = new CliError
            {
                Code = CliErrorCodes.Timeout,
                Message = timedOut
                    ? Resources.Error_TimedOut(timeoutSeconds)
                    : Resources.Error_Cancelled,
            },
        };

    private static void TrySetUtf8Output()
    {
        try
        {
            // UTF-8 without a BOM: a leading BOM in redirected/piped output can confuse some
            // consumers that don't strip it (e.g. some parsers and shells).
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (IOException)
        {
            // No real console attached (handles redirected/closed); leave the default encoding.
        }
        catch (System.Security.SecurityException)
        {
            // Host policy forbids changing console encoding; not fatal for the operation.
        }
    }
}
