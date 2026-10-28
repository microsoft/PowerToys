// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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
    private const int WorkerDrainTimeoutMs = 2_000;

    // How long, at most, the window a prompt was answered for gets to come back to the front.
    private const int PromptRefocusTimeoutMs = 350;

    private static readonly uint RequestShutdownMessage =
        Native.RegisterWindowMessageW("PowerToys.TextExpander.RequestShutdown");

    private static readonly uint ShowDiagnosticsMessage =
        Native.RegisterWindowMessageW("PowerToys.TextExpander.ShowDiagnostics");

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

        SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(
            _hostSettings?.Read().SnippetsPath,
            HostSettings.DataFolder(_hostSettings?.FilePath));
        _store = new SnippetStore(resolution.FilePath, resolution.ResolveFolderNames);
        SnippetStore.TransitionResult initialLoad = _store.Load();
        if (!initialLoad.Succeeded)
        {
            ReportSnippetTransitionFailure("load the initial snippet library", initialLoad);
            Error(EngineText.Get(
                "Error_SnippetFile",
                "Text Expander couldn't load your snippets from:\n\n{0}\n\nCheck that the file is available, then restart PowerToys.")
                .Replace("{0}", initialLoad.AttemptedPath, StringComparison.Ordinal));
            _store.Dispose();
            return 1;
        }

        IntPtr hwnd = CreateMessageWindow();
        if (hwnd == IntPtr.Zero)
        {
            Error(EngineText.Get("Error_MessageWindow", "Text Expander couldn't start because Windows didn't let it create a window it needs. Restart PowerToys to try again."));
            return 1;
        }

        Clipboard.OwnerWindow = hwnd;

        ConfigureInjection();
        _hostSettings?.StartWatching(OnHostSettingsChanged);

        _hostLink = HostLink.Create(args);
        _hostLink?.Start(() => Native.PostMessageW(hwnd, RequestShutdownMessage, IntPtr.Zero, IntPtr.Zero));

        _hook = new KeyboardHook(_store);
        if (!_hook.Install())
        {
            Error(EngineText.Get("Error_KeyboardHook", "Text Expander couldn't start watching what you type. Another app may be blocking it. Restart PowerToys to try again."));
            return 1;
        }

        _worker = new Thread(ExpansionLoop)
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

    private static void ConfigureInjection()
    {
        ApplyHostSettings();

        Injector.Backend = InjectionPolicy.ParseBackend(Environment.GetEnvironmentVariable(InjectionPolicy.BackendVariable), Injector.Backend);

        Injector.ClipboardThresholdChars = InjectionPolicy.ParseThreshold(Environment.GetEnvironmentVariable(InjectionPolicy.ThresholdVariable), Injector.ClipboardThresholdChars);

        HostInputTags.AcceptRemappedInput = !InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.IgnoreRemappedVariable), fallback: !HostInputTags.AcceptRemappedInput);

        WordBoundary.Required = InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.RequireWordBoundaryVariable), fallback: WordBoundary.Required);

        WordBoundary.KeepTerminator = InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.WordBoundaryKeepSpaceVariable), fallback: WordBoundary.KeepTerminator);

        Injector.ClipboardRestoreFailed = message =>
        {
            if (Interlocked.Exchange(ref _clipboardLossReported, 1) == 0)
            {
                Error(message);
            }
        };
    }

    private static void ApplyHostSettings()
    {
        if (_hostSettings is null)
        {
            return;
        }

        HostSettings.Values values = _hostSettings.Read();

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
            SnippetStore.TransitionResult? result = _store?.SetCaseSensitive(caseSensitive);
            if (result.HasValue && !result.Value.Succeeded)
            {
                ReportSnippetTransitionFailure("apply the case-sensitive setting", result.Value);
            }
        }
    }

    private static void OnHostSettingsChanged()
    {
        ApplyHostSettings();

        Injector.Backend = InjectionPolicy.ParseBackend(Environment.GetEnvironmentVariable(InjectionPolicy.BackendVariable), Injector.Backend);

        Injector.ClipboardThresholdChars = InjectionPolicy.ParseThreshold(Environment.GetEnvironmentVariable(InjectionPolicy.ThresholdVariable), Injector.ClipboardThresholdChars);

        HostInputTags.AcceptRemappedInput = !InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.IgnoreRemappedVariable), fallback: !HostInputTags.AcceptRemappedInput);

        WordBoundary.Required = InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.RequireWordBoundaryVariable), fallback: WordBoundary.Required);

        WordBoundary.KeepTerminator = InjectionPolicy.ParseFlag(Environment.GetEnvironmentVariable(InjectionPolicy.WordBoundaryKeepSpaceVariable), fallback: WordBoundary.KeepTerminator);

        RepointSnippetStore();
    }

    private static void RepointSnippetStore()
    {
        if (_hostSettings is null || _store is null)
        {
            return;
        }

        try
        {
            SnippetPathResolution resolution = SnippetStore.ResolveConfiguredPathInfo(
                _hostSettings.Read().SnippetsPath,
                HostSettings.DataFolder(_hostSettings.FilePath));
            SnippetStore.TransitionResult result = _store.Repoint(resolution.FilePath, resolution.ResolveFolderNames);
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

        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static void RunMessageLoop()
    {
        while (Native.GetMessageW(out Native.MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);
        }
    }

    private static void ExpansionLoop()
    {
        if (_hook is null || _store is null)
        {
            return;
        }

        try
        {
            foreach (PendingExpansion work in _hook.Pending.GetConsumingEnumerable())
            {
                try
                {
                    Expand(work);
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
    }

    private static void Expand(PendingExpansion work)
    {
        // A key press or click after this was queued may have moved the caret, so the work is dropped
        // rather than applied wherever the caret is now. Checked again right before anything is erased.
        bool InputUnchanged() => _hook!.InputGeneration == work.InputGeneration;

        if (work.IsUndo)
        {
            // Ctrl is normally still held from the Ctrl+Z that asked for this. Backspaces sent now
            // would arrive as Ctrl+Backspace, which deletes whole words in most editors.
            if (!Injector.WaitForModifierRelease(Injector.UndoModifierReleaseTimeoutMs))
            {
                return;
            }

            if (work.TargetWindow != IntPtr.Zero && Native.GetForegroundWindow() != work.TargetWindow)
            {
                return;
            }

            // A $|$ left the caret inside the expansion; it goes back to the end before the expansion is erased.
            Injector.Replace(Injector.CountDeletableUnits(work.Replacement), work.Trigger, work.TargetWindow, cursorRightUnits: work.CursorLeft, inputUnchanged: InputUnchanged);
            return;
        }

        // Before any prompt, so nobody answers questions for an expansion that can no longer happen.
        if (!InputUnchanged())
        {
            return;
        }

        (string text, int cursor) = Variables.NeedsPrompt(work.Replacement)
            ? ExpandWithPrompts(work.Replacement, work.TargetWindow)
            : Variables.ExpandWithPromptRequests(work.Replacement, static _ => string.Empty, Clipboard.GetText);

        if (work.TargetWindow != IntPtr.Zero && Native.GetForegroundWindow() != work.TargetWindow)
        {
            return;
        }

        // The marker comes from the snippet's own text only; a "$|$" typed into a prompt or
        // copied to the clipboard stays as it is.
        int cursorLeft = cursor < 0 ? 0 : Injector.CountDeletableUnits(text[cursor..]);

        bool inserted = Injector.ReplaceTyped(work.TypedPrefix, text, work.TargetWindow, cursorLeft, InputUnchanged);
        if (ShouldRecordExpansion(inserted, work.Replacement))
        {
            _hook!.RecordExpansion(work.TypedText ?? work.Trigger, text, work.TargetWindow, work.InputGeneration, cursorLeft);
        }
    }

    internal static bool ShouldRecordExpansion(bool inserted, string sourceReplacement)
        => inserted && !Variables.UsesSensitiveRuntimeInput(sourceReplacement);

    private static (string Text, int Cursor) ExpandWithPrompts(string replacement, IntPtr target)
    {
        _hook!.Suspended = true;
        (string Text, int Cursor) expanded;
        try
        {
            expanded = Variables.ExpandWithPromptRequests(replacement, request => PromptDialog.Show(request, target), Clipboard.GetText);
        }
        finally
        {
            _hook.Suspended = false;
        }

        if (target != IntPtr.Zero)
        {
            WindowFocus.Force(target);

            // Types as soon as the window is in front again, instead of after a fixed 350 ms.
            WindowFocus.WaitUntilInFront(target, PromptRefocusTimeoutMs);
        }

        return expanded;
    }

    private static void ShowDiagnostics()
        => Info(EngineText.Get("Diagnostics_Title", "Text Expander diagnostics") + "\n\n"
            + InjectionDiagnostics.BuildReport(Injector.Backend, Injector.ClipboardThresholdChars));

    private static void WriteDiagnosticsFile()
    {
        string path = DiagnosticsFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string source = _store is null
            ? "snippets_file=(none)\nsnippet_count=0\nwatching=false\n"
            : $"snippets_file={_store.FilePath}\nsnippet_count={_store.Current.Map.Count}\n"
                + $"watching={(_store.IsWatching ? "true" : "false")}\n";

        source += $"word_boundary={(WordBoundary.Required ? "true" : "false")}\n"
            + $"word_boundary_keeps_space={(WordBoundary.KeepTerminator ? "true" : "false")}\n";

        File.WriteAllText(path, source + InjectionDiagnostics.BuildMachineReport(Injector.Backend, Injector.ClipboardThresholdChars));
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
    private static string MessageTitle => EngineText.Get("Message_Title", "Text Expander");

    private static void Info(string message)
        => _ = Native.MessageBoxW(IntPtr.Zero, message, MessageTitle, Native.MB_OK | Native.MB_ICONINFORMATION | Native.MB_SETFOREGROUND);

    private static void Error(string message)
        => _ = Native.MessageBoxW(IntPtr.Zero, message, MessageTitle, Native.MB_OK | Native.MB_ICONERROR | Native.MB_SETFOREGROUND);
}
