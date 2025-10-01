#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using ImageResizer.Models;
using ImageResizer.Properties;
using ImageResizer.Utilities;
using ImageResizer.ViewModels;
using ImageResizer.Views;
using ManagedCommon;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace ImageResizer
{
    public partial class App : Application, IDisposable
    {
        private const string SparseMsixFileName = "PowerToysSparse.msix"; // Shared sparse MSIX
        private const string PackageIdentityName = "Microsoft.PowerToys.SparseApp";
        private const string AppUserModelId = "Microsoft.PowerToys.SparseApp_djwsxzxb4ksa8!PowerToys.ImageResizerUI"; // Used for activation
        private const string LogSubFolder = "\\Image Resizer\\Logs"; // Parallels PowerOCR log layout

        static App()
        {
            try
            {
                // Initialize logger early (mirroring PowerOCR pattern)
                Logger.InitializeLogger(LogSubFolder);
                Logger.LogInfo("ImageResizer starting (static ctor). Initializing culture and console encoding.");
            }
            catch
            {
                /* swallow logger init issues silently */
            }

            try
            {
                string appLanguage = LanguageHelper.LoadLanguage();
                if (!string.IsNullOrEmpty(appLanguage))
                {
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(appLanguage);
                }
            }
            catch (CultureNotFoundException)
            {
                Logger.LogWarning("CultureNotFoundException while setting UI culture.");
            }

            Console.InputEncoding = Encoding.Unicode;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.LogDebug("OnStartup entered. Checking package identity state.");
            if (!IsPackagedProcess())
            {
                Logger.LogInfo("Process not running with package identity. Attempting sparse package registration.");
                TryAcquireSparseIdentity(e.Args);
            }
            else
            {
                Logger.LogDebug("Process already has package identity.");
            }

            // Fix for .net 3.1.19 making Image Resizer not adapt to DPI changes.
            ImageResizer.Utilities.NativeMethods.SetProcessDPIAware();

            if (PowerToys.GPOWrapperProjection.GPOWrapper.GetConfiguredImageResizerEnabledValue() == PowerToys.GPOWrapperProjection.GpoRuleConfigured.Disabled)
            {
                /* TODO: Add logs to ImageResizer.
                 * Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
                 */
                Logger.LogWarning("GPO policy disables ImageResizer. Exiting.");
                Environment.Exit(0); // Current.Exit won't work until there's a window opened.
                return;
            }

            var batch = ResizeBatch.FromCommandLine(Console.In, e?.Args);

            // TODO: Add command-line parameters that can be used in lieu of the input page (issue #14)
            var mainWindow = new MainWindow(new MainViewModel(batch, Settings.Default));
            mainWindow.Show();
            Logger.LogInfo("MainWindow shown (unpackaged or activation fallback path).");

            // Temporary workaround for issue #1273
            WindowHelpers.BringToForeground(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            GC.SuppressFinalize(this);
        }

        private bool IsPackagedProcess()
        {
            int length = 0;
            int result = NativeMethods.GetCurrentPackageFullName(ref length, null!);
            const int APPMODEL_ERROR_NO_PACKAGE = 15700;
            if (result == APPMODEL_ERROR_NO_PACKAGE)
            {
                Logger.LogInfo("GetCurrentPackageFullName returned NO_PACKAGE.");
                return false;
            }

            char[] buffer = new char[length];
            result = NativeMethods.GetCurrentPackageFullName(ref length, buffer);
            return result == 0;
        }

        private async void TryAcquireSparseIdentity(string[] args)
        {
            try
            {
                Logger.LogDebug("Beginning sparse package registration workflow.");
                await RegisterSparsePackageAsync();
                Logger.LogDebug("Sparse package registration completed. Attempting packaged activation.");
                ActivatePackaged(args);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Sparse identity acquisition failed; continuing unpackaged. " + ex.Message);
            }
        }

        private async Task RegisterSparsePackageAsync()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(exeDir))
            {
                throw new InvalidOperationException("Cannot determine executing assembly directory.");
            }

            Logger.LogDebug($"ExeDir={exeDir}");

            string sparsePathLocal = Path.Combine(exeDir, "..", SparseMsixFileName);
            string sparsePathShared = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PackagingSparse", "bin", SparseMsixFileName);
            string msixPath = File.Exists(sparsePathLocal) ? sparsePathLocal : sparsePathShared;
            Logger.LogDebug($"Evaluated MSIX path: {msixPath}");
            if (!File.Exists(msixPath))
            {
                throw new FileNotFoundException("Sparse MSIX not found.", msixPath);
            }

            var pm = new PackageManager();
            bool installed = false;
            try
            {
                installed = pm.FindPackagesForUserWithPackageTypes(string.Empty, PackageIdentityName, PackageTypes.Main).Any();
                if (!installed)
                {
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

            Uri externalUri = new(exeDir);
            Uri pkgUri = new(msixPath);
            var options = new AddPackageOptions { ExternalLocationUri = externalUri };
            Logger.LogDebug($"Calling AddPackageByUriAsync: {pkgUri}");
            var op = pm.AddPackageByUriAsync(pkgUri, options);
            try
            {
                var result = await op;
                if (op.Status != AsyncStatus.Completed)
                {
                    throw new InvalidOperationException("Sparse package registration did not complete successfully (status=" + op.Status + ").");
                }

                Logger.LogInfo("Sparse package registration succeeded.");
            }
            catch (COMException comEx) when ((uint)comEx.HResult == 0x80073D0B)
            {
                Logger.LogWarning("Sparse package already installed at different external location (0x80073D0B). Proceeding without re-registration.");
                return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Sparse package AddPackageByUriAsync failed: " + ex.Message);
                throw;
            }
        }

        private void ActivatePackaged(string[] args)
        {
            if (NativeMethods.CoCreateInstance(
                    NativeMethods.CLSID_ApplicationActivationManager,
                    IntPtr.Zero,
                    NativeMethods.CLSCTX.LOCAL_SERVER,
                    NativeMethods.CLSID_IApplicationActivationManager,
                    out object activationObj) != 0)
            {
                Logger.LogWarning("CoCreateInstance for ApplicationActivationManager failed.");
                return;
            }

            var aam = (NativeMethods.IApplicationActivationManager)activationObj;
            uint pid;
            try
            {
                string argLine = string.Join(" ", args ?? Array.Empty<string>());
                Logger.LogDebug($"Attempting ActivateApplication with AUMID={AppUserModelId} Args='" + argLine + "'");
                _ = aam.ActivateApplication(AppUserModelId, argLine, NativeMethods.ActivateOptions.None, out pid);
                Logger.LogDebug($"ActivateApplication succeeded. PID={pid}");
            }
            catch (COMException comEx)
            {
                Logger.LogWarning($"ActivateApplication failed: 0x{comEx.HResult:X8} {comEx.Message}");
            }
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, [Out] char[] packageFullName);

            public enum ActivateOptions
            {
                None = 0x00000000,
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
}
