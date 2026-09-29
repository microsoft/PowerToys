// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

internal static class Program
{
    private const string ManagedSwitch = "--managed";
    private const string DiagnosticsSwitch = "--diagnostics";
    private const string DiagnosticsFileSwitch = "--diagnostics-file";
    private const string WindowClassName = "PowerToys.TextExpander.MessageWindow";
    private const string MutexName = @"Global\PowerToys.TextExpander.SingleInstance";
    private const int WorkerDrainTimeoutMs = 2_000;

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

        using var mutex = new Mutex(true, MutexName + instanceSuffix, out bool createdNew);
        if (!createdNew)
        {
            return 0;
        }

        _hostSettings = HostSettings.Create(args);

        _store = new SnippetStore(SnippetStore.ResolveConfiguredPath(_hostSettings?.Read().SnippetsPath, HostSettings.DataFolder(_hostSettings?.FilePath)));
        _store.Load();

        InputTarget.ApplyConfigTrust(_store.FilePath);

        IntPtr hwnd = CreateMessageWindow();
        if (hwnd == IntPtr.Zero)
        {
            Error("Could not create the internal message window.");
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
            Error("Could not install the keyboard hook. Another program may be blocking it, or the process lacks permission.");
            return 1;
        }

        _worker = new Thread(ExpansionLoop)
        {
            IsBackground = true,
            Name = "PowerToys Text Expander",
        };
        _worker.Start();

        _store.StartWatching();
        RunMessageLoop();

        _hook.Dispose();
        _worker.Join(WorkerDrainTimeoutMs);

        _hostLink?.Dispose();
        _hostSettings?.Dispose();
        _store.Dispose();
        return 0;
    }

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
            string desired = SnippetStore.ResolveConfiguredPath(_hostSettings.Read().SnippetsPath, HostSettings.DataFolder(_hostSettings.FilePath));
            int count = _store.Repoint(desired);
            if (count >= 0)
            {
                InputTarget.ApplyConfigTrust(_store.FilePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

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
        if (work.IsUndo)
        {
            if (work.TargetWindow != IntPtr.Zero && Native.GetForegroundWindow() != work.TargetWindow)
            {
                return;
            }

            Injector.Replace(Injector.CountDeletableUnits(work.Replacement), work.Trigger, work.TargetWindow);
            return;
        }

        string text = Variables.NeedsPrompt(work.Replacement)
            ? ExpandWithPrompts(work.Replacement, work.TargetWindow)
            : Variables.Expand(work.Replacement, static _ => string.Empty, Clipboard.GetText);

        if (work.TargetWindow != IntPtr.Zero && Native.GetForegroundWindow() != work.TargetWindow)
        {
            return;
        }

        int cursorLeft = 0;
        int marker = text.IndexOf("$|$", StringComparison.Ordinal);
        if (marker >= 0)
        {
            cursorLeft = Injector.CountDeletableUnits(text[(marker + 3)..]);
            text = text.Remove(marker, 3);
        }

        if (Injector.ReplaceTyped(work.TypedPrefix, text, work.TargetWindow, cursorLeft))
        {
            _hook!.RecordExpansion(work.TypedText ?? work.Trigger, text, work.TargetWindow);
        }
    }

    private static string ExpandWithPrompts(string replacement, IntPtr target)
    {
        _hook!.Suspended = true;
        string text;
        try
        {
            text = Variables.Expand(replacement, PromptDialog.Show, Clipboard.GetText);
        }
        finally
        {
            _hook.Suspended = false;
        }

        if (target != IntPtr.Zero)
        {
            WindowFocus.Force(target);
            Thread.Sleep(350);
        }

        return text;
    }

    private static void ShowDiagnostics()
        => Info("PowerToys Text Expander injection diagnostics\n\n"
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
                Error("PowerToys Text Expander is not running, so there is nothing to report.");
            }

            return 1;
        }

        IntPtr mode = toFile ? new IntPtr(1) : IntPtr.Zero;
        if (!Native.PostMessageW(hwnd, ShowDiagnosticsMessage, mode, IntPtr.Zero))
        {
            if (!toFile)
            {
                Error("PowerToys Text Expander is running but did not accept the request.");
            }

            return 1;
        }

        return 0;
    }

    private static void Info(string message)
        => _ = Native.MessageBoxW(IntPtr.Zero, message, "PowerToys Text Expander", Native.MB_OK | Native.MB_ICONINFORMATION | Native.MB_SETFOREGROUND);

    private static void Error(string message)
        => _ = Native.MessageBoxW(IntPtr.Zero, message, "PowerToys Text Expander", Native.MB_OK | Native.MB_ICONERROR | Native.MB_SETFOREGROUND);
}
