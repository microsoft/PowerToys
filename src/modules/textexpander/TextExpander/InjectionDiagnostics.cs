// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>What the injection path is actually doing, as opposed to what it is meant to do.</summary>
internal enum InjectionHealth
{
    /// <summary>No expansion has run yet, so there is nothing to judge.</summary>
    Unknown,

    /// <summary>Replacements are being pasted, which is what this path exists for.</summary>
    Pasting,

    /// <summary>
    /// Only short replacements have been expanded, so the paste path has never been exercised.
    /// </summary>
    Untested,

    /// <summary>The typing backend was asked for, so not pasting is correct.</summary>
    TypingByChoice,

    /// <summary>
    /// The clipboard has only ever opened unowned, so every write refused and every expansion
    /// fell back to typing. Safe, but the paste path is inoperative and would otherwise go
    /// unnoticed, because typing works.
    /// </summary>
    ClipboardOwnershipDeclined,

    /// <summary>Pastes are being attempted and refused for some other reason.</summary>
    PasteUnavailable,
}

/// <summary>
/// Records what the injector actually did, so the active path can be seen on a real machine
/// without a debugger.
///
/// This exists because the failure that matters here is silent. Expansion falls back to typing
/// whenever pasting cannot be done safely, and typing *works* — so a paste path that never once
/// succeeds looks exactly like a healthy one from the outside. In particular, opening the
/// clipboard against a window owned by another thread is undocumented territory: if Windows
/// declines it, every open is unowned, every write refuses, and the whole point of the paste
/// path is quietly lost. That shows up here as a count nobody has to go looking for.
///
/// Counters only, held in plain statics and bumped with Interlocked: no allocation, no strings
/// and no timers on the expansion path. Nothing is written to disk and nothing leaves the
/// machine — the report is built on demand, when a user asks to see it.
/// </summary>
internal static class InjectionDiagnostics
{
    private static int _pasted;
    private static int _typed;
    private static int _abandoned;

    private static int _notAttempted;
    private static int _refusedBeforeErase;
    private static int _failedAfterErase;

    private static int _opensOwned;
    private static int _opensUnowned;
    private static int _opensFailed;

    private static int _clipboardRestoreFailures;
    private static int _clipboardRestoreSkipped;
    private static int _typingAbandoned;

    private static int _lastOutcome = -1;
    private static int _lastOpen = -1;

    public static int Pasted => Volatile.Read(ref _pasted);

    public static int Typed => Volatile.Read(ref _typed);

    public static int Abandoned => Volatile.Read(ref _abandoned);

    public static int OpensOwned => Volatile.Read(ref _opensOwned);

    public static int OpensUnowned => Volatile.Read(ref _opensUnowned);

    public static int OpensFailed => Volatile.Read(ref _opensFailed);

    public static int ClipboardRestoreFailures => Volatile.Read(ref _clipboardRestoreFailures);

    /// <summary>
    /// Gets how often the user's clipboard was deliberately left alone because somebody else had
    /// written to it since our own write. Not a failure — the alternative was destroying data the
    /// user copied moments ago — but worth being able to see, because the expansion's text is
    /// then left on the clipboard rather than the snapshot.
    /// </summary>
    public static int ClipboardRestoresSkipped => Volatile.Read(ref _clipboardRestoreSkipped);

    /// <summary>
    /// Gets how often a typed replacement was cut short because focus moved part-way through. Counted
    /// apart from <see cref="Abandoned"/>, which covers expansions abandoned before anything was
    /// delivered.
    /// </summary>
    public static int TypingAbandoned => Volatile.Read(ref _typingAbandoned);

    /// <summary>Gets the last expansion's outcome, or null before the first expansion.</summary>
    public static PasteOutcome? LastOutcome
    {
        get
        {
            int value = Volatile.Read(ref _lastOutcome);
            return value < 0 ? null : (PasteOutcome)value;
        }
    }

    /// <summary>Gets how the clipboard last opened, or null if it has never been opened.</summary>
    public static ClipboardOpen? LastOpen
    {
        get
        {
            int value = Volatile.Read(ref _lastOpen);
            return value < 0 ? null : (ClipboardOpen)value;
        }
    }

    /// <summary>Records how an expansion ended.</summary>
    public static void RecordOutcome(PasteOutcome outcome)
    {
        Volatile.Write(ref _lastOutcome, (int)outcome);

        switch (outcome)
        {
            case PasteOutcome.Pasted:
                Interlocked.Increment(ref _pasted);
                break;

            case PasteOutcome.TargetLost:
                Interlocked.Increment(ref _abandoned);
                break;

            case PasteOutcome.NotAttempted:
                Interlocked.Increment(ref _notAttempted);
                Interlocked.Increment(ref _typed);
                break;

            case PasteOutcome.RefusedBeforeErase:
                Interlocked.Increment(ref _refusedBeforeErase);
                Interlocked.Increment(ref _typed);
                break;

            case PasteOutcome.FailedAfterErase:
                Interlocked.Increment(ref _failedAfterErase);
                Interlocked.Increment(ref _typed);
                break;
        }
    }

    /// <summary>Records how the clipboard opened, which is the signal that matters most.</summary>
    public static void RecordClipboardOpen(ClipboardOpen open)
    {
        Volatile.Write(ref _lastOpen, (int)open);

        switch (open)
        {
            case ClipboardOpen.Owned:
                Interlocked.Increment(ref _opensOwned);
                break;
            case ClipboardOpen.Unowned:
                Interlocked.Increment(ref _opensUnowned);
                break;
            default:
                Interlocked.Increment(ref _opensFailed);
                break;
        }
    }

    /// <summary>Records that the user's clipboard could not be handed back.</summary>
    public static void RecordClipboardRestoreFailure()
        => Interlocked.Increment(ref _clipboardRestoreFailures);

    /// <summary>Records that the clipboard was left alone because a newer owner had written to it.</summary>
    public static void RecordClipboardRestoreSkipped()
        => Interlocked.Increment(ref _clipboardRestoreSkipped);

    /// <summary>Records that a typed replacement was cut short by focus moving mid-delivery.</summary>
    public static void RecordTypingAbandoned()
        => Interlocked.Increment(ref _typingAbandoned);

    /// <summary>Reads the overall verdict, which is the one line most people need.</summary>
    public static InjectionHealth Assess(InjectionBackend backend)
    {
        if (Pasted > 0)
        {
            return InjectionHealth.Pasting;
        }

        if (backend == InjectionBackend.Typing)
        {
            return InjectionHealth.TypingByChoice;
        }

        if (Typed == 0 && Abandoned == 0)
        {
            return InjectionHealth.Unknown;
        }

        // Every expansion so far was short enough to type, so nothing has exercised the paste
        // path yet. Not a fault - just not evidence of anything either.
        if (Volatile.Read(ref _refusedBeforeErase) == 0 && Volatile.Read(ref _failedAfterErase) == 0)
        {
            return InjectionHealth.Untested;
        }

        // The specific silent failure this whole class exists to catch: the clipboard opens, but
        // never with an owner, so the write path refuses every time.
        if (OpensUnowned > 0 && OpensOwned == 0)
        {
            return InjectionHealth.ClipboardOwnershipDeclined;
        }

        return InjectionHealth.PasteUnavailable;
    }

    public static string DescribeHealth(InjectionBackend backend) => Assess(backend) switch
    {
        InjectionHealth.Pasting => "OK — replacements are being pasted.",
        InjectionHealth.Untested =>
            "Not yet exercised — every expansion so far was short enough to type. "
            + "Expand a snippet of five or more characters and look again.",
        InjectionHealth.TypingByChoice =>
            "Typing, as configured — POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND is set to type.",
        InjectionHealth.ClipboardOwnershipDeclined =>
            "PASTE PATH INACTIVE — the clipboard only ever opened unowned, so Windows declined "
            + "cross-thread clipboard ownership and every write refused. Expansions still work, "
            + "by typing, but the paste path is doing nothing. The clipboard work needs to move "
            + "onto the message-pump thread that owns the window.",
        InjectionHealth.PasteUnavailable =>
            "PASTE UNAVAILABLE — pastes are being attempted and refused. Check whether the "
            + "target window is elevated, or whether the clipboard holds something that cannot "
            + "be restored (an image from an app that publishes only a bitmap handle).",
        _ => "Unknown — no expansion has run yet.",
    };

    /// <summary>
    /// Builds the human-readable report. Allocates freely: this runs when a user opens the
    /// dialog, never during an expansion.
    /// </summary>
    public static string BuildReport(InjectionBackend backend, int thresholdChars)
    {
        var report = new StringBuilder(512);

        report.Append("Status   : ").AppendLine(DescribeHealth(backend));
        report.Append("Backend  : ").Append(backend)
              .Append(" (paste at ").Append(thresholdChars).AppendLine(" chars or more)");
        report.Append("Last     : ")
              .Append(LastOutcome?.ToString() ?? "no expansion yet")
              .Append(", clipboard ")
              .AppendLine(LastOpen?.ToString() ?? "never opened");

        report.AppendLine();
        report.Append("Pasted   : ").Append(Pasted).AppendLine();
        report.Append("Typed    : ").Append(Typed)
              .Append("  (too short ").Append(Volatile.Read(ref _notAttempted))
              .Append(", refused ").Append(Volatile.Read(ref _refusedBeforeErase))
              .Append(", failed mid-paste ").Append(Volatile.Read(ref _failedAfterErase))
              .AppendLine(")");
        report.Append("Abandoned: ").Append(Abandoned).AppendLine("  (focus moved)");
        report.Append("Cut short: ").Append(TypingAbandoned).AppendLine("  (focus moved mid-typing)");

        report.AppendLine();
        report.Append("Clipboard opens: ").Append(OpensOwned).Append(" owned, ")
              .Append(OpensUnowned).Append(" unowned, ")
              .Append(OpensFailed).AppendLine(" failed");
        report.Append("Clipboard restores lost: ").Append(ClipboardRestoreFailures).AppendLine();
        report.Append("Clipboard restores skipped: ").Append(ClipboardRestoresSkipped)
              .AppendLine("  (a newer copy was kept)");

        return report.ToString();
    }

    /// <summary>
    /// Builds a machine-readable form of the same counters, for the end-to-end harness. Written
    /// to a file rather than stdout because the engine is a WinExe and has no console.
    /// </summary>
    public static string BuildMachineReport(InjectionBackend backend, int thresholdChars)
    {
        var report = new StringBuilder(384);

        report.Append("health=").Append(Assess(backend)).Append('\n');
        report.Append("backend=").Append(backend).Append('\n');
        report.Append("threshold=").Append(thresholdChars).Append('\n');
        report.Append("last_outcome=").Append(LastOutcome?.ToString() ?? "none").Append('\n');
        report.Append("last_open=").Append(LastOpen?.ToString() ?? "none").Append('\n');
        report.Append("pasted=").Append(Pasted).Append('\n');
        report.Append("typed=").Append(Typed).Append('\n');
        report.Append("abandoned=").Append(Abandoned).Append('\n');
        report.Append("typed_too_short=").Append(Volatile.Read(ref _notAttempted)).Append('\n');
        report.Append("typed_refused=").Append(Volatile.Read(ref _refusedBeforeErase)).Append('\n');
        report.Append("typed_failed_mid_paste=").Append(Volatile.Read(ref _failedAfterErase))
              .Append('\n');
        report.Append("opens_owned=").Append(OpensOwned).Append('\n');
        report.Append("opens_unowned=").Append(OpensUnowned).Append('\n');
        report.Append("opens_failed=").Append(OpensFailed).Append('\n');
        report.Append("restore_failures=").Append(ClipboardRestoreFailures).Append('\n');
        report.Append("restores_skipped=").Append(ClipboardRestoresSkipped).Append('\n');
        report.Append("typing_abandoned=").Append(TypingAbandoned).Append('\n');

        return report.ToString();
    }

    /// <summary>Clears every counter. For tests; the app never calls this.</summary>
    internal static void Reset()
    {
        Volatile.Write(ref _pasted, 0);
        Volatile.Write(ref _typed, 0);
        Volatile.Write(ref _abandoned, 0);
        Volatile.Write(ref _notAttempted, 0);
        Volatile.Write(ref _refusedBeforeErase, 0);
        Volatile.Write(ref _failedAfterErase, 0);
        Volatile.Write(ref _opensOwned, 0);
        Volatile.Write(ref _opensUnowned, 0);
        Volatile.Write(ref _opensFailed, 0);
        Volatile.Write(ref _clipboardRestoreFailures, 0);
        Volatile.Write(ref _clipboardRestoreSkipped, 0);
        Volatile.Write(ref _typingAbandoned, 0);
        Volatile.Write(ref _lastOutcome, -1);
        Volatile.Write(ref _lastOpen, -1);
    }
}
