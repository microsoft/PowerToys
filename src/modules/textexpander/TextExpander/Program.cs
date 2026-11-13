// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.TextExpander;

internal static class Program
{
    private const string ManagedSwitch = "--managed";
    private const string DiagnosticsSwitch = "--diagnostics";
    private const string DiagnosticsFileSwitch = "--diagnostics-file";
    private const string WindowClassName = "PowerToys.TextExpander.MessageWindow";

    // Session-local, like the runner's instance mutex and the exit event: each signed-in session runs its own engine.
    private const string MutexName = @"Local\PowerToys.TextExpander.SingleInstance";

    // Keep in sync with ENGINE_EXIT_TIMEOUT_MS: a paste may settle for five seconds before restoring the clipboard.
    private const int WorkerDrainTimeoutMs = 10_000;

    // How long, at most, the window a prompt was answered for gets to come back to the front.
    private const int PromptRefocusTimeoutMs = 350;

    private static readonly uint RequestShutdownMessage =
        Native.RegisterWindowMessageW("PowerToys.TextExpander.RequestShutdown");

    private static readonly uint ShowDiagnosticsMessage =
        Native.RegisterWindowMessageW("PowerToys.TextExpander.ShowDiagnostics");

    // One diagnostics file write at a time.
    private static readonly object DiagnosticsFileLock = new();

    private static string _windowClassName = WindowClassName;
    private static Native.WndProc? _wndProcDelegate;
    private static SnippetStore? _store;
    private static KeyboardHook? _hook;
    private static Thread? _worker;
    private static HostLink? _hostLink;
    private static HostSettings? _hostSettings;
    private static int _clipboardLossReported;

    public static string DiagnosticsFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "TextExpander", "diagnostics.txt");

    [STAThread]
    private static int Main(string[] args)
    {
        _ = args.Contains(ManagedSwitch, StringComparer.OrdinalIgnoreCase);

        string instanceSuffix = HostLink.ParseInstanceSuffix(args);
        _windowClassName = WindowClassName + instanceSuffix;

        if (args.Contains(DiagnosticsFileSwitch, StringComparer.OrdinalIgnoreCase))
        {
            return RequestDiagnostics(toFile: true);
        }

        if (args.Contains(DiagnosticsSwitch, StringComparer.OrdinalIgnoreCase))
        {
            return RequestDiagnostics(toFile: false);
        }

        // The runner doesn't start a utility that group policy turns off; starting the engine directly mustn't either.
        if (!PolicyAllowsStart(GPOWrapper.GetConfiguredTextExpanderEnabledValue()))
        {
            return 0;
        }

        using var mutex = new Mutex(true, InstanceMutexName(instanceSuffix), out bool createdNew);
        if (!createdNew)
        {
            return 0;
        }

        if (!InputTarget.ApplyIntegrityCeiling())
        {
            Error(EngineText.Get("Error_Elevated", "Text Expander can't run as administrator. Restart PowerToys without administrator rights."));
            return 1;
        }

        _hostSettings = HostSettings.Create(args);

        // One read for both the snippets library and the options. Settings that can't be read here, not written yet or
        // broken, mean the defaults, since no library is in use yet to keep; they're read again shortly, and on each change.
        HostSettings.Values? settings = _hostSettings?.Read();
        SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(
            settings?.SnippetsPath,
            HostSettings.DataFolder(_hostSettings?.FilePath));
        _store = new SnippetStore(resolution.FilePath, resolution.IsDefault);

        // A snippets file that can't be loaded yet, such as one in OneDrive before OneDrive starts at sign-in, leaves
        // Text Expander running with no snippets. Once it's watching, the store keeps trying quietly.
        SnippetStore.TransitionResult initialLoad = _store.Load();
        if (!initialLoad.Succeeded)
        {
            ReportSnippetTransitionFailure("load the initial snippet library", initialLoad);
        }

        IntPtr hwnd = CreateMessageWindow();
        if (hwnd == IntPtr.Zero)
        {
            Error(EngineText.Get("Error_MessageWindow", "Text Expander couldn't start because Windows didn't let it create a window it needs. Restart PowerToys to try again."));
            return 1;
        }

        Clipboard.OwnerWindow = hwnd;

        ConfigureInjection(settings);

        // Settings can change while the initial library loads, even when the first settings read succeeded.
        _hostSettings?.StartWatching(OnHostSettingsChanged, readAgain: true);

        _hostLink = HostLink.Create(args);
        _hostLink?.Start(() => Native.PostMessageW(hwnd, RequestShutdownMessage, IntPtr.Zero, IntPtr.Zero));

        _hook = new KeyboardHook(_store);
        if (!_hook.Install())
        {
            Error(EngineText.Get("Error_KeyboardHook", "Text Expander couldn't start watching what you type. Another app may be blocking it. Restart PowerToys to try again."));
            return 1;
        }

        using var shutdown = new CancellationTokenSource();
        _worker = new Thread(() => ExpansionLoop(shutdown.Token))
        {
            IsBackground = true,
            Name = "PowerToys Text Expander",
        };
        _worker.Start();

        SnippetStore.TransitionResult watchResult = _store.StartWatching();
        if (!watchResult.Succeeded)
        {
            ReportSnippetTransitionFailure("close the initial snippet watcher handoff", watchResult);
        }

        RunMessageLoop();

        shutdown.Cancel();
        _hook.Dispose();
        _worker.Join(WorkerDrainTimeoutMs);

        _hostLink?.Dispose();
        _hostSettings?.Dispose();
        _store.Dispose();
        return 0;
    }

    /// <summary>Only a policy that turns Text Expander off stops the engine; one that isn't configured leaves it to the user.</summary>
    internal static bool PolicyAllowsStart(GpoRuleConfigured configured) => configured != GpoRuleConfigured.Disabled;

    internal static string InstanceMutexName(string instanceSuffix) => MutexName + instanceSuffix;

    private static void ConfigureInjection(HostSettings.Values? settings)
    {
        if (settings is { } values)
        {
            ApplyHostSettings(values, _store);
        }

        ApplyEnvironmentOverrides();

        Injector.ClipboardRestoreFailed = message =>
        {
            if (Interlocked.Exchange(ref _clipboardLossReported, 1) == 0)
            {
                Error(message);
            }
        };
    }

    // The environment variables are escape hatches, so they win over the settings file.
    private static void ApplyEnvironmentOverrides()
    {
        Injector.Backend = InjectionPolicy.ParseBackend(Environment.GetEnvironmentVariable(InjectionPolicy.BackendVariable), Injector.Backend);

        Injector.ClipboardThresholdChars = InjectionPolicy.ParseThreshold(Environment.GetEnvironmentVariable(InjectionPolicy.ThresholdVariable), Injector.ClipboardThresholdChars);

        HostInputTags.AcceptRemappedInput = !InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.IgnoreRemappedVariable), fallback: !HostInputTags.AcceptRemappedInput);

        WordBoundary.Required = InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.RequireWordBoundaryVariable), fallback: WordBoundary.Required);

        WordBoundary.KeepTerminator = InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.WordBoundaryKeepSpaceVariable), fallback: WordBoundary.KeepTerminator);
    }

    private static void ApplyHostSettings(HostSettings.Values values, SnippetStore? store)
    {
        if (values.InjectionBackend is string backend)
        {
            Injector.Backend = InjectionPolicy.ParseBackend(backend, Injector.Backend);
        }

        if (values.ClipboardThresholdChars is int threshold && threshold >= 1)
        {
            Injector.ClipboardThresholdChars = threshold;
        }

        if (values.TreatRemapsAsTyping is bool treatAsTyping)
        {
            HostInputTags.AcceptRemappedInput = treatAsTyping;
        }

        if (values.RequireWordBoundary is bool requireBoundary)
        {
            WordBoundary.Required = requireBoundary;
        }

        if (values.WordBoundaryKeepsSpace is bool keepsSpace)
        {
            WordBoundary.KeepTerminator = keepsSpace;
        }

        if (values.CaseSensitive is bool caseSensitive)
        {
            SnippetStore.TransitionResult? result = store?.SetCaseSensitive(caseSensitive);
            if (result.HasValue && !result.Value.Succeeded)
            {
                ReportSnippetTransitionFailure("apply the case-sensitive setting", result.Value);
            }
        }
    }

    private static bool OnHostSettingsChanged()
        => _hostSettings is null || ApplyChangedSettings(_hostSettings, _store);

    /// <summary>
    /// Reads <paramref name="settings"/> after the file changed and applies it, snippets library included. A file that
    /// can't be read, such as a save caught half-written or a hand edit with a mistake, changes nothing: the options and
    /// the library in use stay as they are.
    /// </summary>
    /// <returns>Whether the file could be read.</returns>
    internal static bool ApplyChangedSettings(HostSettings settings, SnippetStore? store)
    {
        if (settings.Read() is not { } values)
        {
            return false;
        }

        ApplyHostSettings(values, store);
        ApplyEnvironmentOverrides();
        RepointSnippetStore(values.SnippetsPath, store, HostSettings.DataFolder(settings.FilePath));
        return true;
    }

    private static void RepointSnippetStore(string? snippetsPath, SnippetStore? store, string? hostDataFolder)
    {
        if (store is null)
        {
            return;
        }

        try
        {
            SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(snippetsPath, hostDataFolder);
            SnippetStore.TransitionResult result = store.Repoint(resolution.FilePath, resolution.IsDefault);
            if (!result.Succeeded)
            {
                ReportSnippetTransitionFailure("switch the snippet library", result);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Trace.TraceWarning($"Text Expander could not resolve the requested snippet library: {ex.Message}");
        }
    }

    private static void ReportSnippetTransitionFailure(string action, SnippetStore.TransitionResult result)
        => Trace.TraceWarning(
            $"Text Expander could not {action}. Status: {result.Status}; path: '{result.AttemptedPath}'; error: {result.Error?.Message}");

    private static IntPtr CreateMessageWindow()
    {
        _wndProcDelegate = MessageWndProc;

        var windowClass = new Native.WNDCLASSEXW
        {
            CbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEXW>(),
            LpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            HInstance = Native.GetModuleHandleW(null),
            LpszClassName = _windowClassName,
        };

        if (Native.RegisterClassExW(ref windowClass) == 0 && Marshal.GetLastWin32Error() != 1410)
        {
            return IntPtr.Zero;
        }

        return Native.CreateWindowExW(Native.WS_EX_TOOLWINDOW, _windowClassName, "PowerToys Text Expander", Native.WS_OVERLAPPED, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, Native.GetModuleHandleW(null), IntPtr.Zero);
    }

    private static IntPtr MessageWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == RequestShutdownMessage)
        {
            Native.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        if (msg == ShowDiagnosticsMessage)
        {
            bool toFile = wParam != IntPtr.Zero;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (toFile)
                    {
                        WriteDiagnosticsFile();
                    }
                    else
                    {
                        ShowDiagnostics();
                    }
                }
                catch
                {
                }
            });
            return IntPtr.Zero;
        }

        if (msg == Native.WM_DESTROY)
        {
            Native.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        _ = RefreshCultureOnRegionChange(msg, lParam);
        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Reads the user's culture again when Region settings change. .NET keeps it, with the date and time formats,
    /// from when it was first read, so expansions would go on typing the old formats until the engine restarted.
    /// </summary>
    /// <returns>Whether <paramref name="msg"/> was that change.</returns>
    internal static bool RefreshCultureOnRegionChange(uint msg, IntPtr lParam)
    {
        if (msg != Native.WM_SETTINGCHANGE || lParam == IntPtr.Zero || !string.Equals(Marshal.PtrToStringUni(lParam), "intl", StringComparison.Ordinal))
        {
            return false;
        }

        CultureInfo.CurrentCulture.ClearCachedData();
        return true;
    }

    private static void RunMessageLoop()
    {
        while (Native.GetMessageW(out Native.MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);
        }
    }

    private static void ExpansionLoop(CancellationToken cancellationToken)
    {
        if (_hook is null || _store is null)
        {
            return;
        }

        try
        {
            foreach (PendingExpansion work in _hook.Pending.GetConsumingEnumerable(cancellationToken))
            {
                try
                {
                    Expand(work, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static void Expand(PendingExpansion work, CancellationToken cancellationToken)
        => Expand(_hook!, work, () => TryExpand(_hook!, work, cancellationToken: cancellationToken));

    /// <summary>
    /// Runs <paramref name="tryExpand"/> for <paramref name="work"/>, then lets go of what the guard still holds for it.
    /// Tests pass their own <paramref name="tryExpand"/>.
    /// </summary>
    internal static bool Expand(KeyboardHook hook, PendingExpansion work, Func<bool> tryExpand)
    {
        try
        {
            return tryExpand();
        }
        finally
        {
            // However the work ended, keys held since a prompt was answered or cancelled go back to their window, or are
            // dropped if it isn't in front. So do keys held since the match, when no insertion took them over: the key
            // the hook swallowed first, so what was typed stays as typed.
            hook.Guard.Abandon();
            hook.Guard.ReleaseAfterMatch(work.InputGeneration);
        }
    }

    /// <summary>
    /// Erases and types for <see cref="TryExpand"/>: <see cref="Injector.Replace"/>, or a test's fake. Its last parameter is
    /// the key the hook swallowed, which goes back if a terminal can't be given the snippet.
    /// </summary>
    internal delegate bool ReplaceText(int backspaceCount, string text, IntPtr targetWindow, int cursorLeftUnits, int cursorRightUnits, InsertionGuard guard, long inputGeneration, Action<string, int>? inserted, int givenBack, bool fromClipboard, string swallowed);

    /// <summary>
    /// Expands or undoes <paramref name="work"/>. Tests pass a fake <paramref name="replace"/>, <paramref name="isKeyDown"/>,
    /// <paramref name="ask"/>, <paramref name="choose"/>, <paramref name="readClipboard"/>, <paramref name="readHistory"/> and
    /// <paramref name="notify"/>, so nothing is typed, read from the keyboard, the clipboard or clipboard history, asked,
    /// chosen or shown.
    /// </summary>
    internal static bool TryExpand(KeyboardHook hook, PendingExpansion work, ReplaceText? replace = null, Func<int, bool>? isKeyDown = null, Func<PromptRequest, IntPtr, Action?, string?>? ask = null, Func<ClipboardChoice, IntPtr, Action?, string?>? choose = null, Func<string?>? readClipboard = null, Func<ClipboardHistory>? readHistory = null, Action<IntPtr, IReadOnlyList<ClipboardGap>>? notify = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        replace ??= Injector.Replace;
        readClipboard ??= Clipboard.GetText;

        // A key press or click after this was queued may have moved the caret, so the work is dropped rather than
        // applied wherever the caret is now. The hook has the last word when the insertion starts.
        bool InputUnchanged() => hook.InputGeneration == work.InputGeneration;

        if (work.IsUndo)
        {
            // Ctrl is normally still held from the Ctrl+Z that asked for this. Backspaces sent now
            // would arrive as Ctrl+Backspace, which deletes whole words in most editors.
            if (!Injector.WaitForModifierRelease(Injector.UndoModifierReleaseTimeoutMs, isKeyDown))
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (work.TargetWindow != IntPtr.Zero && Native.GetForegroundWindow() != work.TargetWindow)
            {
                return false;
            }

            // A $|$ left the caret inside the expansion; it goes back to the end before the expansion is erased.
            return replace(Injector.CountDeletableUnits(work.Replacement), work.Trigger, work.TargetWindow, cursorLeftUnits: 0, cursorRightUnits: work.CursorLeft, hook.Guard, work.InputGeneration, inserted: null, givenBack: 0, fromClipboard: false, swallowed: string.Empty);
        }

        // Before any prompt, so nobody answers questions for an expansion that can no longer happen.
        if (!InputUnchanged())
        {
            return false;
        }

        // Clipboard history is read now, while any questions are asked, so a choice doesn't wait for it: when no window
        // turns out to be needed, keys typed during the wait would reach the app before anything holds them, and cancel the
        // insertion.
        if (readHistory is null && ClipboardChoiceDialog.PicksCopy(work.Replacement))
        {
            Task<ClipboardHistory> history = Task.Run(ClipboardHistoryReader.Read);
            readHistory = () => ClipboardChoiceDialog.Await(history);
        }

        bool giveBack = false;
        ExpandedSnippet? expanded = Variables.MayAsk(work.Replacement)
            ? ExpandWithPrompts(hook, work.Replacement, work.TargetWindow, work.InputGeneration, ask, choose, readClipboard, readHistory, isKeyDown, out giveBack, cancellationToken)
            : Variables.ExpandWithPromptRequests(work.Replacement, static _ => string.Empty, readClipboard, history: readHistory);
        cancellationToken.ThrowIfCancellationRequested();
        if (expanded is null)
        {
            // A question was cancelled: nothing is erased or inserted, and the keystroke the hook swallowed goes back, so
            // what was typed stays as typed. Not while a modifier is still down: it would leave the app in menu mode.
            if (giveBack)
            {
                _ = replace(0, work.SwallowedText, work.TargetWindow, cursorLeftUnits: 0, cursorRightUnits: 0, hook.Guard, work.InputGeneration, inserted: null, givenBack: work.SwallowedText.Length, fromClipboard: false, swallowed: string.Empty);
            }

            return false;
        }

        (string text, int cursor) = expanded.Value;
        if (work.Terminator is char terminator)
        {
            text += terminator;
        }

        if (work.TargetWindow != IntPtr.Zero && Native.GetForegroundWindow() != work.TargetWindow)
        {
            return false;
        }

        // The marker comes from the snippet's own text only; a "$|$" typed into a prompt or
        // copied to the clipboard stays as it is. One too far from the end stays at the end.
        int cursorLeft = cursor < 0 ? 0 : Injector.CountDeletableUnits(text[cursor..]);
        if (cursorLeft > Injector.MaxCaretMoves)
        {
            cursorLeft = 0;
        }

        // A physical key can produce text before the matched suffix. That text was swallowed too, so insert it once.
        string typedText = work.TypedText ?? work.Trigger;
        int swallowedMatchLength = typedText.Length - work.TypedPrefix.Length;
        string literalPrefix = work.SwallowedText[..(work.SwallowedText.Length - swallowedMatchLength)];
        text = literalPrefix + text;

        // Kept for Ctrl+Z before the keys held meanwhile go back, unless it holds a prompt's answer or the clipboard.
        Action<string, int>? record = Variables.UsesSensitiveRuntimeInput(work.Replacement)
            ? null
            : (sentText, sentCursorLeft) => hook.RecordExpansion(literalPrefix + typedText, sentText, work.TargetWindow, work.InputGeneration, sentCursorLeft);
        bool inserted = replace(Injector.CountDeletableUnits(work.TypedPrefix), text, work.TargetWindow, cursorLeft, cursorRightUnits: 0, hook.Guard, work.InputGeneration, record, givenBack: literalPrefix.Length + (work.Terminator is null ? 0 : 1), fromClipboard: expanded.Value.HasClipboardText, swallowed: work.SwallowedText);

        // A spot a clipboard variable left empty is told once it's all in and the keys held meanwhile went back, so the
        // notice never comes between the snippet and them.
        if (inserted && expanded.Value.Gaps.Count > 0)
        {
            (notify ?? ClipboardNotice.Show)(work.TargetWindow, expanded.Value.Gaps);
        }

        return inserted;
    }

    /// <summary>
    /// Asks the snippet's questions, one at a time, then the choices its clipboard variables ask for, and expands it. Null
    /// if one is cancelled; <paramref name="giveBack"/> then says whether the keystroke the hook swallowed can go back.
    /// Keys typed on after the trigger, which are held since its match (input generation <paramref name="generation"/>),
    /// cancel it before a window opens: the window would take them. They go back as typed, after that keystroke.
    /// </summary>
    private static ExpandedSnippet? ExpandWithPrompts(KeyboardHook hook, string replacement, IntPtr target, long generation, Func<PromptRequest, IntPtr, Action?, string?>? ask, Func<ClipboardChoice, IntPtr, Action?, string?>? choose, Func<string?> readClipboard, Func<ClipboardHistory>? readHistory, Func<int, bool>? isKeyDown, out bool giveBack, CancellationToken cancellationToken)
    {
        ask ??= (request, window, accepted) => PromptDialog.Show(request, window, accepted, cancellationToken);
        choose ??= (choice, window, accepted) => ClipboardChoiceDialog.Show(choice, window, accepted, cancellationToken);

        // Once the last window is answered, keys typed right away are held for the expansion, so they land after it. A
        // clipboard choice may follow the questions, so then the last question doesn't hold; the choice says if it's last.
        int prompts = SnippetTemplate.Parse(replacement).Count(token => token.Kind == SnippetTokenKind.Prompt);
        bool mayChoose = Variables.MayChoose(replacement);
        bool cancelled = false;
        bool released = false;
        bool shown = false;
        bool holding = false;
        hook.SuspendFor(target);
        ExpandedSnippet expanded;
        try
        {
            expanded = Variables.ExpandWithPromptRequests(replacement, Answer, () => cancelled ? null : readClipboard(), Choose, () => cancelled || readHistory is null ? ClipboardHistory.NotRead : readHistory());

            // A window was answered, but the choice expected after it didn't need one, as with a single match: hold from
            // now, before keys reaching the app again can cancel the insertion.
            if (!cancelled && shown && !holding)
            {
                Hold();
            }
        }
        finally
        {
            hook.Suspended = false;
        }

        // Shutdown cancels the work itself, rather than returning a cancelled prompt to an app and taking focus back.
        cancellationToken.ThrowIfCancellationRequested();

        // Only once a window of ours took the front. A snippet that asked nothing, as with one match, never pulls the app
        // back in front: if the user went elsewhere meanwhile, the expansion is dropped instead.
        if (target != IntPtr.Zero && shown)
        {
            WindowFocus.Force(target);

            // Types as soon as the window is in front again, instead of after a fixed 350 ms. If it doesn't come back,
            // the keys held since the answer are dropped once the work ends.
            WindowFocus.WaitUntilInFront(target, PromptRefocusTimeoutMs);
        }

        giveBack = cancelled && released;
        return cancelled ? null : expanded;

        void Hold()
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                holding = true;
                hook.Guard.Hold(target);
            }
        }

        // Esc, Cancel or the close button cancels the whole snippet: nothing more is asked or chosen, nor is the clipboard
        // read. Keys typed from then on are held too, so they land after the keystroke given back, but only once no
        // modifier is down: Alt+F4's Alt, let go of while held, would go back after the insertion pressed it again,
        // opening the app's menu. A modifier still down after a second means nothing goes back.
        string? Answer(PromptRequest request)
        {
            if (cancelled || !WindowMayOpen())
            {
                return null;
            }

            shown = true;
            string? answer = ask(request, target, --prompts == 0 && !mayChoose ? Hold : null);
            cancellationToken.ThrowIfCancellationRequested();
            if (answer is null)
            {
                Cancel();
            }

            return answer;
        }

        string? Choose(ClipboardChoice choice)
        {
            if (cancelled || !WindowMayOpen())
            {
                return null;
            }

            shown = true;
            string? chosen = choose(choice, target, choice.IsLast ? Hold : null);
            cancellationToken.ThrowIfCancellationRequested();
            if (chosen is null)
            {
                Cancel();
            }

            return chosen;
        }

        // Keys typed on since the match would go to the window: the snippet is cancelled instead, and the keystroke the hook
        // swallowed goes back with them held after it, as typed. No modifier needs letting go of: its key-up is held too.
        // Nor is a window opened for an expansion that can no longer happen: a click may have moved the caret, or what
        // was held since the match went back, the swallowed key first, once the insertion was too long coming.
        bool WindowMayOpen()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!hook.Guard.Unchanged(generation))
            {
                cancelled = true;
                return false;
            }

            if (hook.Guard.ReleaseAfterMatchBeforeAWindow(generation))
            {
                return true;
            }

            cancelled = true;
            released = true;
            return false;
        }

        void Cancel()
        {
            cancelled = true;
            released = Injector.WaitForModifierRelease(Injector.UndoModifierReleaseTimeoutMs, isKeyDown);
            if (released)
            {
                hook.Guard.Hold(target);
            }
        }
    }

    private static void ShowDiagnostics()
        => Info(EngineText.Get("Diagnostics_Title", "Text Expander diagnostics") + "\n\n"
            + InjectionDiagnostics.BuildReport(Injector.Backend, Injector.ClipboardThresholdChars));

    /// <summary>
    /// Writes the diagnostics file, which bug reports copy as it is: so where the snippets file is shows only as the
    /// default location or a custom one, never as a path. <paramref name="store"/> and <paramref name="path"/> are for tests.
    /// </summary>
    internal static void WriteDiagnosticsFile(SnippetStore? store = null, string? path = null)
    {
        store ??= _store;
        path ??= DiagnosticsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string source = store is null
            ? "snippets_location=(none)\nsnippets_file_exists=false\nsnippet_count=0\nwatching=false\n"
            : $"snippets_location={(store.IsDefaultPath ? "default" : "custom")}\n"
                + $"snippets_file_exists={(File.Exists(store.FilePath) ? "true" : "false")}\n"
                + $"snippet_count={store.Current.Map.Count}\nwatching={(store.IsWatching ? "true" : "false")}\n";

        source += $"word_boundary={(WordBoundary.Required ? "true" : "false")}\n"
            + $"word_boundary_keeps_space={(WordBoundary.KeepTerminator ? "true" : "false")}\n";

        lock (DiagnosticsFileLock)
        {
            File.WriteAllText(path, source + InjectionDiagnostics.BuildMachineReport(Injector.Backend, Injector.ClipboardThresholdChars));
        }
    }

    private static IntPtr FindEngineWindow()
    {
        IntPtr exact = Native.FindWindowW(_windowClassName, null);
        if (exact != IntPtr.Zero)
        {
            return exact;
        }

        if (!string.Equals(_windowClassName, WindowClassName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        IntPtr found = IntPtr.Zero;
        var buffer = new char[256];
        Native.EnumWindows(
            (hwnd, _) =>
        {
            int written = Native.GetClassNameW(hwnd, buffer, buffer.Length);
            if (written > 0)
            {
                var name = new string(buffer, 0, written);
                if (name.StartsWith(WindowClassName, StringComparison.Ordinal))
                {
                    found = hwnd;
                    return false;
                }
            }

            return true;
        },
            IntPtr.Zero);

        return found;
    }

    private static int RequestDiagnostics(bool toFile)
    {
        IntPtr hwnd = FindEngineWindow();
        if (hwnd == IntPtr.Zero)
        {
            if (!toFile)
            {
                Error(EngineText.Get("Diagnostics_NotRunning", "Text Expander isn't running, so there's nothing to report."));
            }

            return 1;
        }

        IntPtr mode = toFile ? new IntPtr(1) : IntPtr.Zero;
        if (!Native.PostMessageW(hwnd, ShowDiagnosticsMessage, mode, IntPtr.Zero))
        {
            if (!toFile)
            {
                Error(EngineText.Get("Diagnostics_NoAnswer", "Text Expander is running but didn't answer."));
            }

            return 1;
        }

        return 0;
    }

    // The same name as the prompt window's title.
    private static string MessageTitle => EngineText.Get("Message_Title", "PowerToys Text Expander");

    private static void Info(string message)
        => _ = Native.MessageBoxW(IntPtr.Zero, message, MessageTitle, Native.MB_OK | Native.MB_ICONINFORMATION | Native.MB_SETFOREGROUND);

    private static void Error(string message)
        => _ = Native.MessageBoxW(IntPtr.Zero, message, MessageTitle, Native.MB_OK | Native.MB_ICONERROR | Native.MB_SETFOREGROUND);
}
