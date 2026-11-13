// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using FileExplorerScripts.Core;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerScripts.Client;
using Windows.Data.Xml.Dom;
using Windows.Storage.Pickers;
using Windows.UI.Notifications;

namespace FileExplorerScripts;

public sealed partial class App : Application, IDisposable
{
    private readonly PowerScriptsClient _client = new("file-explorer", reuseHost: true);
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Window? _lifetimeWindow;
    private ExplorerScriptWindow? _window;
    private ExplorerExecutionWindow? _executionWindow;
    private ExplorerScriptStage _lastStage;
    private bool _disposed;

    internal DispatcherQueue UiDispatcher { get; } = DispatcherQueue.GetForCurrentThread();

    public App()
    {
        InitializeComponent();
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Length == 3 && arguments[1] == "--watch")
        {
            // WinUI exits when its last window closes. Keep the runner-owned app alive between requests.
            _lifetimeWindow = new Window();
            _lifetimeWindow.AppWindow.Hide();
        }

        Logger.InitializeLogger("\\File Explorer\\PowerScripts\\Logs");
        UnhandledException += (_, e) => Logger.LogError("Unhandled Explorer PowerScripts exception.", e.Exception);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Window construction can raise OnLaunched before the app constructor returns.
        await Task.Yield();
        var watching = false;
        try
        {
            var arguments = Environment.GetCommandLineArgs();
            if (arguments.Length == 3 && arguments[1] == "--watch")
            {
                watching = true;
                using var lifetime = new CatalogueLifetime(int.Parse(arguments[2], CultureInfo.InvariantCulture));
                using var stopping = lifetime.Token.Register(() => _shutdown.Cancel());
                lifetime.Token.ThrowIfCancellationRequested();
                try
                {
                    using var warmup = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    warmup.CancelAfter(TimeSpan.FromSeconds(10));
                    await _client.ListAsync(warmup.Token);
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException or Win32Exception or System.Text.Json.JsonException or OperationCanceledException)
                {
                    Logger.LogError("Unable to prewarm the Explorer PowerScripts Host. Catalogue refresh will report its current availability.", exception);
                }

                await CatalogueWorker.RunAsync(int.Parse(arguments[2], CultureInfo.InvariantCulture), lifetime.Token);
            }
            else if (arguments.Length == 3 && arguments[1] == "--request")
            {
                await ExecuteRequestAsync(arguments[2]);
            }
            else
            {
                throw new ArgumentException("Launch PowerScripts from a selected file's Explorer context menu.");
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelling the configuration/destination picker never submits an invocation.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or Win32Exception or System.Text.Json.JsonException or COMException or NotSupportedException)
        {
            Logger.LogError("Explorer PowerScript could not complete.", exception);
            if (watching)
            {
                if (MessageBoxW(IntPtr.Zero, exception.Message, Strings.Get("Failed"), 0x10) == 0)
                {
                    Logger.LogError("Unable to display the Explorer PowerScripts error.");
                }
            }
            else
            {
                _window?.Close();
                _window = null;
                _executionWindow ??= new ExplorerExecutionWindow(Strings.Get("Title"), 0);
                await _executionWindow.ShowFailureAsync(exception);
            }
        }
        finally
        {
            _shutdown.Cancel();
            _client.Dispose();
            _window?.Close();
            _window = null;
            _executionWindow?.Finish();
            _executionWindow?.Close();
            _executionWindow = null;
            await _requests.WaitAsync();
            Dispose();
            _lifetimeWindow?.Close();
            Exit();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _shutdown.Cancel();
            _client.Dispose();
            _requests.Dispose();
            _shutdown.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    internal async Task ExecuteRequestAsync(string id)
    {
        var entered = false;
        try
        {
            await _requests.WaitAsync(_shutdown.Token);
            entered = true;
            _lastStage = ExplorerScriptStage.Preparing;
            await RunRequestAsync(InvocationRequest.Read(id));
        }
        catch (OperationCanceledException)
        {
            // Configuration cancellation and owner shutdown do not submit another invocation.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or Win32Exception or System.Text.Json.JsonException or COMException or NotSupportedException)
        {
            Logger.LogError("Explorer PowerScript could not complete.", exception);
            if (!_shutdown.IsCancellationRequested)
            {
                _window?.Close();
                _window = null;
                _executionWindow ??= new ExplorerExecutionWindow(Strings.Get("Title"), 0);
                await _executionWindow.ShowFailureAsync(exception, _lastStage);
            }
        }
        finally
        {
            if (entered)
            {
                _window?.Close();
                _window = null;
                _executionWindow?.Finish();
                _executionWindow?.Close();
                _executionWindow = null;
                _requests.Release();
            }
        }
    }

    private async Task RunRequestAsync(InvocationRequest request)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException("Run PowerScripts from a non-elevated File Explorer window.");
        }

        if (!ExplorerSettings.IsParentEnabled())
        {
            throw new InvalidOperationException("File Explorer Add-ons is disabled.");
        }

        var service = new ExplorerScriptService(_client);
        using var discoveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        discoveryTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        System.Collections.Generic.IReadOnlyList<PowerScriptInfo> scripts;
        try
        {
            scripts = await service.GetApplicableScriptsAsync(request.Files, discoveryTimeout.Token);
        }
        catch (OperationCanceledException exception)
        {
            throw new InvalidOperationException("PowerScripts discovery timed out. Check the PowerScripts editor and try again.", exception);
        }

        if (scripts.Count == 0)
        {
            throw new InvalidOperationException(Strings.Get("NoScripts"));
        }

        var script = string.IsNullOrEmpty(request.ScriptId) ? null : scripts.SingleOrDefault(item => item.Id == request.ScriptId)
            ?? throw new InvalidOperationException("The selected script is no longer available or compatible.");
        var showParameters = ExplorerSettings.ShowParameters();
        System.Collections.Generic.IReadOnlyDictionary<string, System.Text.Json.JsonElement> parameters;
        if (script is null || (showParameters && script.Parameters.Count > 0))
        {
            _window = new ExplorerScriptWindow(scripts, request.Files, request.ScriptId, showParameters);
            (script, parameters) = await _window.ConfigureAsync();
        }
        else
        {
            if (!ScriptParameters.CanRunWithDefaults(script))
            {
                throw new InvalidOperationException(Strings.Get("MissingDefaults"));
            }

            parameters = ScriptParameters.GetDefaults(script);
        }

        var parents = request.Files.Select(path => Path.GetDirectoryName(Path.GetFullPath(path))!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var destination = parents.Length == 1 ? parents[0] : await PickDestinationAsync(scripts, request);
        using var userCancellation = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(userCancellation.Token, timeout.Token, _shutdown.Token);
        var delay = ExplorerSettings.StatusDelay();
        var elapsed = Stopwatch.StartNew();
        var popup = new DispatcherTimer();
        var finished = false;
        void ShowProgress()
        {
            _executionWindow = new ExplorerExecutionWindow(script.Name, request.Files.Count, userCancellation);
            _executionWindow.Start(TimeSpan.Zero, elapsed.Elapsed);
            _executionWindow.ReportStage(_lastStage);
        }

        if (delay == TimeSpan.Zero)
        {
            ShowProgress();
        }
        else
        {
            popup.Interval = delay;
            popup.Tick += (_, _) =>
            {
                popup.Stop();
                if (!finished)
                {
                    ShowProgress();
                }
            };
            popup.Start();
        }

        var progress = new Progress<ExplorerScriptStage>(stage =>
        {
            if (!finished)
            {
                _lastStage = stage;
                _executionWindow?.ReportStage(stage);
            }
        });
        System.Collections.Generic.IReadOnlyList<string> outputs;
        try
        {
            outputs = await service.ExecuteAsync(script, request.Files, parameters, destination, progress, cancellation.Token);
        }
        catch (OperationCanceledException exception)
        {
            throw new InvalidOperationException(Strings.Get(userCancellation.IsCancellationRequested ? "Cancelled" : "TimedOut"), exception);
        }
        finally
        {
            finished = true;
            popup.Stop();
            elapsed.Stop();
        }

        _executionWindow?.Finish();
        _executionWindow?.Close();
        _executionWindow = null;

        try
        {
            var message = outputs.Count == 0 ? Strings.Get("NoFiles") :
                string.Format(CultureInfo.CurrentCulture, Strings.CreatedFiles, outputs.Count, destination);
            var xml = new XmlDocument();
            xml.LoadXml("<toast><visual><binding template=\"ToastGeneric\"><text>" +
                SecurityElement.Escape(Strings.Get("Completed")) + "</text><text>" +
                SecurityElement.Escape(message) + "</text></binding></visual></toast>");

            // Use the existing PowerToys notification channel, registered by the Runner.
            ToastNotificationManager.CreateToastNotifier("Microsoft.PowerToysWin32").Show(new ToastNotification(xml));
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException)
        {
            Logger.LogError("The PowerScript completed, but its notification failed.", exception);
            if (MessageBoxW(IntPtr.Zero, Strings.Get("NotificationFailed") + Environment.NewLine + destination, Strings.Get("Completed"), 0x40) == 0)
            {
                Logger.LogError("Unable to display the completed PowerScript notification warning.");
            }
        }
    }

    private async Task<string> PickDestinationAsync(System.Collections.Generic.IReadOnlyList<PowerScriptInfo> scripts, InvocationRequest request)
    {
        _window ??= new ExplorerScriptWindow(scripts, request.Files, request.ScriptId, false);
        _window.Activate();
        var picker = new FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_window));
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync() ?? throw new OperationCanceledException();
        _window.AppWindow.Hide();
        return folder.Path;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
