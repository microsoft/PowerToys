// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Keyboard;
using PowerOCR.Settings;
using PowerToys.Interop;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace PowerOCR;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IDisposable
{
    private KeyboardMonitor? keyboardMonitor;
    private EventMonitor? eventMonitor;
    private Mutex? _instanceMutex;
    private int _powerToysRunnerPid;
    private ETWTrace etwTrace = new ETWTrace();

    private CancellationTokenSource NativeThreadCTS { get; set; }

    public App()
    {
        Logger.InitializeLogger("\\TextExtractor\\Logs");

        try
        {
            string appLanguage = LanguageHelper.LoadLanguage();
            if (!string.IsNullOrEmpty(appLanguage))
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
            }
        }
        catch (CultureNotFoundException ex)
        {
            Logger.LogError("CultureNotFoundException: " + ex.Message);
        }

        NativeThreadCTS = new CancellationTokenSource();

        NativeEventWaiter.WaitForEventLoop(
            Constants.TerminatePowerOCRSharedEvent(),
            this.Shutdown,
            this.Dispatcher,
            NativeThreadCTS.Token);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        keyboardMonitor?.Dispose();
        etwTrace?.Dispose();
    }

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        await RestartWithIdentityIfNecessaryAsync(e.Args);

        // If we are still here (not exited / relaunched), continue normal startup.
        if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredTextExtractorEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
        {
            Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
            Shutdown();
            return;
        }

        // allow only one instance of PowerOCR
        _instanceMutex = new Mutex(true, @"Local\PowerToys_PowerOCR_InstanceMutex", out bool createdNew);
        if (!createdNew)
        {
            Logger.LogWarning("Another running TextExtractor instance was detected. Exiting TextExtractor");
            _instanceMutex = null;
            Shutdown();
            return;
        }

        if (e.Args?.Length > 0)
        {
            try
            {
                _ = int.TryParse(e.Args[0], out _powerToysRunnerPid);
                Logger.LogInfo($"TextExtractor started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");

                RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
                {
                    Logger.LogInfo("PowerToys Runner exited. Exiting TextExtractor");
                    NativeThreadCTS.Cancel();
                    Current.Dispatcher.Invoke(() => Shutdown());
                });
                var userSettings = new UserSettings(new Helpers.ThrottledActionInvoker());
                eventMonitor = new EventMonitor(Current.Dispatcher, NativeThreadCTS.Token);
            }
            catch (Exception ex)
            {
                Logger.LogError($"TextExtractor got an exception on start: {ex}");
            }
        }
        else
        {
            Logger.LogInfo($"TextExtractor started detached from PowerToys Runner.");
            _powerToysRunnerPid = -1;
            var userSettings = new UserSettings(new Helpers.ThrottledActionInvoker());
            keyboardMonitor = new KeyboardMonitor(userSettings);
            keyboardMonitor?.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Dispose();
    }

    // Sparse identity constants (finalized): values correspond to shared sparse package manifest.
    private const string SparseMsixFileName = "PowerToysSparse.msix";
    private const string PackageIdentityName = "Microsoft.PowerToys.SparseApp"; // Used for presence check
    private const string AppUserModelId = "Microsoft.PowerToys.SparseApp_djwsxzxb4ksa8!PowerToys.OCR"; // Used for activation

    private async Task RestartWithIdentityIfNecessaryAsync(string[] args)
    {
        if (IsPackagedProcess())
        {
            Logger.LogDebug("Sparse identity already active.");
            return; // Continue startup normally.
        }

        Logger.LogInfo("PowerOCR starting without package identity (will register sparse package).");
        try
        {
            await RegisterSparsePackageAsync();
            RunWithIdentity(args);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Sparse package registration failed: " + ex.ToString());
            Logger.LogWarning("Continuing without identity; AI-dependent features may be disabled.");
        }
    }

    private async Task RegisterSparsePackageAsync()
    {
        string? exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (exeDir == null)
        {
            throw new InvalidOperationException("Cannot determine executing assembly directory.");
        }

        string externalLocation = exeDir; // Where unpackaged exe & content live
        string sparsePkgPath = Path.Combine(exeDir, SparseMsixFileName);

        if (!File.Exists(sparsePkgPath))
        {
            throw new FileNotFoundException("Sparse MSIX not found.", sparsePkgPath);
        }

        var pm = new PackageManager();
        bool installed = false;
        try
        {
            installed = pm.FindPackagesForUserWithPackageTypes(string.Empty, PackageIdentityName, PackageTypes.Main).Any();
            if (!installed)
            {
                // Broader fallback enumeration
                installed = pm.FindPackagesForUser(string.Empty).Any(p => p.Id.Name.Equals(PackageIdentityName, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Sparse package presence detection failed: " + ex.Message);
        }

        if (installed)
        {
            Logger.LogDebug("Sparse package already installed (presence check). Skipping registration.");
            return;
        }

        Uri externalUri = new(externalLocation);
        Uri packageUri = new(sparsePkgPath);

        var options = new AddPackageOptions
        {
            ExternalLocationUri = externalUri,
        };

        Logger.LogDebug($"Registering sparse package: {sparsePkgPath}");
        var op = pm.AddPackageByUriAsync(packageUri, options);
        try
        {
            var result = await op;
            if (op.Status == AsyncStatus.Completed)
            {
                Logger.LogDebug("Sparse package registration succeeded.");
                return;
            }
            throw new InvalidOperationException("Sparse package registration did not complete successfully (status=" + op.Status + ").");
        }
        catch (COMException comEx) when ((uint)comEx.HResult == 0x80073D0B)
        {
            // 0x80073D0B: Package move failed (already installed at different ExternalLocation). Use existing.
            Logger.LogWarning("Sparse package already installed at different external location (0x80073D0B). Proceeding without re-registration.");
            return;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Sparse package AddPackageByUriAsync failed: " + ex.Message);
            throw;
        }
    }

    private void RunWithIdentity(string[] originalArgs)
    {
        // Acquire Activation Manager COM object
        if (NativeMethods.CoCreateInstance(
                NativeMethods.CLSID_ApplicationActivationManager,
                IntPtr.Zero,
                NativeMethods.CLSCTX.LOCAL_SERVER,
                NativeMethods.CLSID_IApplicationActivationManager,
                out object activationObj) != 0)
        {
            throw new InvalidOperationException("Failed to create ApplicationActivationManager.");
        }

        var aam = (NativeMethods.IApplicationActivationManager)activationObj;
        uint pid; // not used further, but capture for completeness

        // NOTE: original sample ignores passing arguments; you may append joined args if needed.
        _ = aam.ActivateApplication(AppUserModelId, string.Empty, NativeMethods.ActivateOptions.None, out pid);
        Logger.LogDebug($"Activated packaged process (PID={pid}).");
    }

    private static bool IsPackagedProcess()
    {
        int length = 0;
        int result = NativeMethods.GetCurrentPackageFullName(ref length, null!);
        const int APPMODEL_ERROR_NO_PACKAGE = 15700;
        if (result == APPMODEL_ERROR_NO_PACKAGE)
        {
            return false;
        }

        char[] buffer = new char[length];
        result = NativeMethods.GetCurrentPackageFullName(ref length, buffer);
        return result == 0;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, [Out] char[] packageFullName);

        public enum ActivateOptions
        {
            None = 0x00000000,
            DesignMode = 0x00000001,
            NoErrorUI = 0x00000002,
            NoSplashScreen = 0x00000004,
        }

        public const string CLSID_ApplicationActivationManager_String = "45ba127d-10a8-46ea-8ab7-56ea9078943c";
        public const string CLSID_IApplicationActivationManager_String = "2e941141-7f97-4756-ba1d-9decde894a3d";

        public static readonly Guid CLSID_ApplicationActivationManager = new(CLSID_ApplicationActivationManager_String);
        public static readonly Guid CLSID_IApplicationActivationManager = new(CLSID_IApplicationActivationManager_String);

        [ComImport]
        [Guid(CLSID_IApplicationActivationManager_String)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IApplicationActivationManager
        {
            IntPtr ActivateApplication([In] string appUserModelId, [In] string arguments, [In] ActivateOptions options, [Out] out uint processId);

            IntPtr ActivateForFile([In] string appUserModelId, IntPtr itemArray, [In] string verb, [Out] out uint processId);

            IntPtr ActivateForProtocol([In] string appUserModelId, IntPtr itemArray, [Out] out uint processId);
        }

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        public static extern uint CoCreateInstance(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            IntPtr pUnkOuter,
            CLSCTX dwClsContext,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object rReturnedComObject);

        [Flags]
        public enum CLSCTX : uint
        {
            LOCAL_SERVER = 0x4,
        }
    }
}
