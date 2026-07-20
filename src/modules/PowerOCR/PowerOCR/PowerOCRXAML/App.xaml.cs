// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PowerOCR.Core.Imaging;
using PowerOCR.Core.Ocr;
using PowerOCR.Core.Services;
using PowerOCR.Helpers;
using PowerOCR.Keyboard;
using PowerOCR.Services;
using PowerOCR.Settings;
using PowerOCR.ViewModels;

namespace PowerOCR;

/// <summary>
/// WinUI 3 application composition root for PowerOCR (Text Extractor).
/// </summary>
public partial class App : Application, IDisposable
{
    private readonly ETWTrace _etwTrace = new();
    private readonly int _runnerPid;
    private ServiceProvider? _serviceProvider;
    private bool _disposed;

    public static new App Current => (App)Application.Current;

    public DispatcherQueue? DispatcherQueueForApp { get; private set; }

    public App(int runnerPid)
    {
        _runnerPid = runnerPid;

        string appLanguage = LanguageHelper.LoadLanguage();
        if (!string.IsNullOrEmpty(appLanguage))
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = appLanguage;
        }

        InitializeComponent();
        UnhandledException += (s, e) => Logger.LogError("Unhandled exception", e.Exception);
    }

    /// <summary>
    /// Resolves a service from the DI container.
    /// </summary>
    public static T GetService<T>()
        where T : class
    {
        var provider = Current._serviceProvider
            ?? throw new InvalidOperationException("Service provider is not initialized.");
        return provider.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueueForApp = DispatcherQueue.GetForCurrentThread();

        var services = new ServiceCollection();

        // Core services from Tasks 1-2
        services.AddSingleton<IBitmapPreprocessor, BitmapPreprocessor>();
        services.AddSingleton<IOcrRecognizer, WindowsOcrRecognizer>();
        services.AddSingleton<ITextExtractorService, TextExtractorService>();

        // App-level services
        services.AddSingleton(DispatcherQueueForApp);
        services.AddSingleton<IActivationService, ActivationService>();
        services.AddSingleton<INativeEventListener, NativeEventListener>();
        services.AddSingleton<IThrottledActionInvoker, ThrottledActionInvoker>();
        services.AddSingleton<IUserSettings, UserSettings>();
        services.AddSingleton<KeyboardMonitor>();
        services.AddSingleton<SettingsDeepLink>();

        // Overlay services (Task 4)
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IOverlayWindowFactory, OverlayWindowFactory>();
        services.AddSingleton<IOverlayManager, OverlayManager>();

        // Task 5: Clipboard and session view model
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddTransient<OverlaySessionViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Register native events for show/terminate
        var eventListener = _serviceProvider.GetRequiredService<INativeEventListener>();
        var activationService = _serviceProvider.GetRequiredService<IActivationService>();

        eventListener.Register(
            PowerToys.Interop.Constants.ShowPowerOCRSharedEvent(),
            () => activationService.RequestActivation());

        eventListener.Register(
            PowerToys.Interop.Constants.TerminatePowerOCRSharedEvent(),
            Terminate);

        // Resolve overlay manager so its activation subscription is live
        _serviceProvider.GetRequiredService<IOverlayManager>();

        if (_runnerPid >= 0)
        {
            Logger.LogInfo($"TextExtractor started from the PowerToys Runner. Runner pid={_runnerPid}");
            RunnerHelper.WaitForPowerToysRunner(_runnerPid, () =>
            {
                Logger.LogInfo("PowerToys Runner exited. Exiting TextExtractor");
                Terminate();
            });
        }
        else
        {
            Logger.LogInfo("TextExtractor started detached from PowerToys Runner.");
            var keyboardMonitor = _serviceProvider.GetRequiredService<KeyboardMonitor>();
            keyboardMonitor.Start();
        }
    }

    private void Terminate()
    {
        var queue = DispatcherQueueForApp;
        if (queue is null || !queue.TryEnqueue(() =>
        {
            Dispose();
            Exit();
        }))
        {
            Environment.Exit(0);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _serviceProvider?.Dispose();
            _etwTrace?.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
