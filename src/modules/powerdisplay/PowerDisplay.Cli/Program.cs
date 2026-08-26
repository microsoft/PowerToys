// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
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
    // Overall wall-clock deadline for one CLI invocation (pipe connect + request/response + any
    // hardware write). There is deliberately no --timeout option: the CLI is a thin client that
    // blocks waiting on the app, and the app's DDC/CI writes are synchronous and cannot be cancelled
    // mid-call, so the client must bound its own wait or a slow/stuck monitor (or a hung app) would
    // hang it indefinitely. 5s covers a normal connect plus one VCP exchange with margin. When it
    // elapses the invocation is reported as TIMEOUT (exit 8).
    internal static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    // Bound on just the pipe-connect phase. MUST stay strictly less than OperationTimeout:
    // NamedPipeClientStream.ConnectAsync polls until either its own timeout (-> TimeoutException,
    // which CliPipeClient maps to a null response -> PROVIDER_UNAVAILABLE, exit 10) or ct
    // cancellation. If this equalled OperationTimeout, the deadline timer would cancel ct at the same
    // instant and win the race, so a not-running app would be misreported as TIMEOUT (exit 8) after a
    // full 5s wait instead of a fast, correct PROVIDER_UNAVAILABLE ("PowerDisplay is not running").
    // A running app connects near-instantly, so the shorter bound never affects the normal path.
    internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    // Extra wall-clock budget added per additional monitor in a comma-separated -n batch (set/up/down).
    // The base OperationTimeout covers the first target (connect + one VCP exchange); each further
    // monitor is a fast reconnect plus one more exchange, so a smaller per-monitor increment suffices.
    internal static readonly TimeSpan PerAdditionalMonitorTimeout = TimeSpan.FromSeconds(3);

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

    // Overall deadline for one invocation, scaled by the number of per-monitor IPC round-trips. A
    // comma-separated -n batch (set/up/down) dispatches once per monitor, so each additional target
    // adds PerAdditionalMonitorTimeout; a single-target invocation keeps the base OperationTimeout.
    internal static TimeSpan ComputeOperationTimeout(int targetCount)
        => OperationTimeout + (PerAdditionalMonitorTimeout * Math.Max(0, targetCount - 1));

    // Counts how many per-monitor dispatches a parsed invocation will make, so the overall deadline
    // can scale with a -n batch. Only the write commands (set/up/down) batch; a monitor id wins and
    // collapses to one target. Everything else is a single round-trip.
    internal static int CountDispatchTargets(ParseResult parseResult)
    {
        var command = parseResult.CommandResult.Command.Name;
        if (command is not (CliCommandNames.Set or CliCommandNames.Up or CliCommandNames.Down))
        {
            return 1;
        }

        if (!string.IsNullOrEmpty(parseResult.GetValueForOption(CliOptions.MonitorId)))
        {
            return 1;
        }

        var numbers = parseResult.GetValueForOption(CliOptions.MonitorNumber);
        return numbers is { Length: > 1 } ? numbers.Length : 1;
    }

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

        var timedOut = false;
        Timer? timeoutTimer = null;
        ConsoleCancelEventHandler? cancelHandler = null;

        // A comma-separated -n batch (set/up/down) performs one IPC round-trip per monitor, so the
        // overall deadline scales with the number of targets (a single-target invocation keeps 5s).
        var operationTimeout = ComputeOperationTimeout(CountDispatchTargets(parseResult));

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

            // Fire the fixed deadline. `timedOut` is set on the timer thread before cts.Cancel(); the
            // cancel→token propagation establishes happens-before, so the catch below reads it
            // reliably. The flag lets the error envelope distinguish a timeout from a Ctrl+C
            // cancellation (both map to exit 8).
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
                operationTimeout,
                Timeout.InfiniteTimeSpan);

            // The dispatcher's own timeout bounds only the pipe-connect phase (ConnectTimeout, shorter
            // than OperationTimeout) so a not-running app surfaces as PROVIDER_UNAVAILABLE quickly
            // rather than racing the overall deadline into a misleading TIMEOUT.
            var dispatcher = new IpcDispatcher(output, ConnectTimeout);

            return await DispatchAsync(root, args, parseResult, dispatcher, output, cts.Token);
        }
        catch (OperationCanceledException)
        {
            output.WriteError(BuildTimeoutErrorResult(CommandLabelFor(parseResult), timedOut, operationTimeout));
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
                var monitorId = parseResult.GetValueForOption(CliOptions.MonitorId);
                var settingFilter = parseResult.GetValueForOption(CliOptions.SettingFilter);

                if (!TryGetSingleMonitorNumber(parseResult, output, CliCommandNames.Get, out var monitorNumber))
                {
                    return CliExitCodes.ArgumentError;
                }

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

                WarnIfMonitorNumberIgnored(output, monitorNumber.HasValue ? new[] { monitorNumber.Value } : System.Array.Empty<int>(), monitorId);

                return await dispatcher.SendGetAsync(
                    CliRequestBuilder.BuildGet(monitorNumber, monitorId, settingFilter),
                    cancellationToken);
            }

            // ── set ───────────────────────────────────────────────────────────
            case CliCommandNames.Set:
            {
                var monitorNumbers = parseResult.GetValueForOption(CliOptions.MonitorNumber) ?? System.Array.Empty<int>();
                var monitorId = parseResult.GetValueForOption(CliOptions.MonitorId);

                SetCommandInputs MakeSetInputs(int? number) => new()
                {
                    MonitorNumber = number,
                    MonitorId = monitorId,
                    Brightness = parseResult.GetValueForOption(CliOptions.Brightness),
                    Contrast = parseResult.GetValueForOption(CliOptions.Contrast),
                    Volume = parseResult.GetValueForOption(CliOptions.Volume),
                    ColorTemperature = parseResult.GetValueForOption(CliOptions.ColorTemperature),
                    InputSource = parseResult.GetValueForOption(CliOptions.InputSource),
                    PowerState = parseResult.GetValueForOption(CliOptions.PowerState),
                    Orientation = parseResult.GetValueForOption(CliOptions.Orientation),
                    ConfirmPowerOff = parseResult.GetValueForOption(CliOptions.ConfirmPowerOff),
                };

                // CLI-side syntactic validation: exactly one setting must be specified. The setting is
                // independent of the monitor selection, so validate it once before any per-monitor dispatch.
                var selected = SetCommand.CountSelectedSettings(MakeSetInputs(null));
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

                WarnIfMonitorNumberIgnored(output, monitorNumbers, monitorId);

                return await DispatchWriteTargetsAsync(
                    monitorNumbers,
                    monitorId,
                    number => dispatcher.SendSetAsync(CliRequestBuilder.BuildSet(MakeSetInputs(number)), cancellationToken));
            }

            // ── up / down ─────────────────────────────────────────────────────
            case CliCommandNames.Up:
            case CliCommandNames.Down:
            {
                var monitorNumbers = parseResult.GetValueForOption(CliOptions.MonitorNumber) ?? System.Array.Empty<int>();
                var monitorId = parseResult.GetValueForOption(CliOptions.MonitorId);
                var commandName = parseResult.CommandResult.Command.Name;

                AdjustCommandInputs MakeAdjustInputs(int? number) => new()
                {
                    MonitorNumber = number,
                    MonitorId = monitorId,
                    Brightness = parseResult.GetValueForOption(CliOptions.BrightnessFlag),
                    Contrast = parseResult.GetValueForOption(CliOptions.ContrastFlag),
                    Volume = parseResult.GetValueForOption(CliOptions.VolumeFlag),
                    Step = parseResult.GetValueForOption(CliOptions.Step),
                };

                // CLI-side syntactic validation: exactly one continuous setting must be specified.
                var selected = AdjustCommand.CountSelectedSettings(MakeAdjustInputs(null));
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

                WarnIfMonitorNumberIgnored(output, monitorNumbers, monitorId);

                return await DispatchWriteTargetsAsync(
                    monitorNumbers,
                    monitorId,
                    number => dispatcher.SendAdjustAsync(CliRequestBuilder.BuildAdjust(commandName, MakeAdjustInputs(number)), cancellationToken));
            }

            // ── capabilities ──────────────────────────────────────────────────
            case CliCommandNames.Capabilities:
            {
                var monitorId = parseResult.GetValueForOption(CliOptions.MonitorId);
                var settingFilter = parseResult.GetValueForOption(CliOptions.SettingFilter);

                if (!TryGetSingleMonitorNumber(parseResult, output, CliCommandNames.Capabilities, out var monitorNumber))
                {
                    return CliExitCodes.ArgumentError;
                }

                WarnIfMonitorNumberIgnored(output, monitorNumber.HasValue ? new[] { monitorNumber.Value } : System.Array.Empty<int>(), monitorId);

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
                var profileId = parseResult.GetValueForArgument(CliOptions.ProfileId);
                return await dispatcher.SendApplyProfileAsync(
                    CliRequestBuilder.BuildApplyProfile(profileId),
                    cancellationToken);
            }

            default:
                return await root.InvokeAsync(args);
        }
    }

    // Carry-forward: the app discards -n when -i is also supplied; surface that warning
    // CLI-side without a round-trip. Shared by the get/set/up/down/capabilities branches. Internal
    // (rather than private) so the complete-ignored-list formatting is directly unit-testable.
    internal static void WarnIfMonitorNumberIgnored(ICliOutput output, IReadOnlyList<int> monitorNumbers, string? monitorId)
    {
        if (monitorNumbers.Count > 0 && !string.IsNullOrEmpty(monitorId))
        {
            var ignored = string.Join(",", monitorNumbers.Select(n => n.ToString(CultureInfo.InvariantCulture)));
            output.WriteWarning(Resources.Warn_MonitorNumberIgnored(ignored));
        }
    }

    // Resolves the -n option to a single optional 1-based index for the single-monitor read commands
    // (get, capabilities). A comma-separated batch is rejected with an ARGUMENT_ERROR — only the write
    // commands (set/up/down) apply to multiple monitors. Returns false (and emits the error) on a batch.
    private static bool TryGetSingleMonitorNumber(ParseResult parseResult, ICliOutput output, string command, out int? monitorNumber)
    {
        var numbers = parseResult.GetValueForOption(CliOptions.MonitorNumber) ?? System.Array.Empty<int>();
        if (numbers.Length > 1)
        {
            output.WriteError(ArgumentError(command, Resources.Error_SingleMonitorOnly));
            monitorNumber = null;
            return false;
        }

        monitorNumber = numbers.Length == 1 ? numbers[0] : (int?)null;
        return true;
    }

    // Dispatches a write command (set/up/down) to each selected monitor, rendering each per-monitor
    // result/error via <paramref name="dispatchOne"/> and returning the aggregated exit code. A monitor
    // id (when present) wins and collapses to a single dispatch; 0 or 1 monitor numbers also dispatch
    // once (preserving the single-monitor path). For a real batch (>1 numbers, no id) it dispatches per
    // number, aborting early on PROVIDER_UNAVAILABLE (the app is not running) and otherwise aggregating
    // the worst outcome (see WorseBatchExit). Extracted (internal) so the routing/aggregation is testable.
    internal static async Task<int> DispatchWriteTargetsAsync(
        IReadOnlyList<int> monitorNumbers,
        string? monitorId,
        Func<int?, Task<int>> dispatchOne)
    {
        var hasId = !string.IsNullOrEmpty(monitorId);
        if (hasId || monitorNumbers.Count <= 1)
        {
            int? single = !hasId && monitorNumbers.Count == 1 ? monitorNumbers[0] : (int?)null;
            return await dispatchOne(single);
        }

        var worst = CliExitCodes.Ok;
        foreach (var number in monitorNumbers)
        {
            var exit = await dispatchOne(number);
            if (exit == CliExitCodes.ProviderUnavailable)
            {
                return exit;
            }

            worst = WorseBatchExit(worst, exit);
        }

        return worst;
    }

    // Folds a per-monitor exit code into the running batch exit code. UNSUPPORTED_FEATURE does not count
    // as a batch failure (a monitor that lacks the setting is skipped, mirroring apply-profile), and a
    // higher exit code ranks as more severe (HardwareFailure > InvalidDiscreteValue > OutOfRange >
    // MonitorNotFound), so the batch reports its worst outcome.
    internal static int WorseBatchExit(int current, int next)
    {
        if (next == CliExitCodes.Ok || next == CliExitCodes.UnsupportedFeature)
        {
            return current;
        }

        return next > current ? next : current;
    }

    public static bool HasHelpToken(ParseResult parseResult)
        => parseResult.Tokens.Any(t =>
            (t.Type != TokenType.Argument || parseResult.Errors.Count > 0)
            && IsHelpToken(t.Value));

    private static bool IsHelpToken(string token)
        => token is "--help" or "-h" or "-?" or "/?";

    public static bool HasVersionToken(ParseResult parseResult)
        => parseResult.Tokens.Any(t =>
            (t.Type != TokenType.Argument || parseResult.Errors.Count > 0)
            && t.Value == "--version");

    public static bool IsVersionRequest(ParseResult parseResult)
        => HasVersionToken(parseResult)
            && (parseResult.CommandResult.Command is RootCommand
                || parseResult.CommandResult.Command.Name == CliCommandNames.ApplyProfile);

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

    // Shared TIMEOUT envelope for the OperationCanceledException catch path. Distinguishes the fixed
    // deadline elapsing (timedOut) from a Ctrl+C cancellation; both map to exit 8. The deadline is the
    // per-invocation timeout (scaled by monitor count for a -n batch), so the message reports it verbatim.
    private static CliErrorResult BuildTimeoutErrorResult(string command, bool timedOut, TimeSpan operationTimeout)
        => new()
        {
            Command = command,
            Error = new CliError
            {
                Code = CliErrorCodes.Timeout,
                Message = timedOut
                    ? Resources.Error_TimedOut((int)operationTimeout.TotalSeconds)
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
