// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Microsoft.Workspaces.UITests
{
    internal sealed class TestAppFixture : IDisposable
    {
        internal const string DefaultTitle = "Workspaces UI test app";
        private const string PackageName = "Microsoft.PowerToys.Workspaces.TestApp";
        private const string Publisher = "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
        private readonly PackageManager packageManager = new();
        private Package? package;
        private bool ownsRegistration;
        private global::Windows.Foundation.IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress>? installationOperation;
        private Task<DeploymentResult>? installation;

        internal string ExecutablePath { get; } = Path.Combine(AppContext.BaseDirectory, "Fixture", "Workspaces.TestApp.exe");

        internal string PackageFullName => package?.Id.FullName ?? throw new InvalidOperationException("The packaged fixture has not been installed.");

        internal string AppUserModelId => package is not null
            ? $"{package.Id.FamilyName}!App"
            : throw new InvalidOperationException("The packaged fixture has not been installed.");

        internal string PackagedExecutablePath => package is not null
            ? Path.Combine(package.InstalledLocation.Path, "Workspaces.TestApp.exe")
            : throw new InvalidOperationException("The packaged fixture has not been installed.");

        internal Session OpenUnpackaged(string title, TestContext context, string payload = "")
        {
            Assert.IsTrue(File.Exists(ExecutablePath), $"The unpackaged fixture was not staged: {ExecutablePath}");
            context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting unpackaged fixture '{title}'.");
            var start = new ProcessStartInfo(ExecutablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("--title");
            start.ArgumentList.Add(title);
            start.ArgumentList.Add("--payload");
            start.ArgumentList.Add(payload);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the unpackaged fixture.");
            Assert.IsFalse(ElevationHelper.IsProcessElevated(process.Id), "The fixture must start non-elevated.");
            Assert.IsNull(NativeMethods.PackageFullName(process.Id), "The unpackaged fixture unexpectedly has package identity.");
            return WaitForWindow(title);
        }

        internal Session OpenPackaged(TestContext context)
        {
            EnsurePackage(context);
            var entries = package!.GetAppListEntriesAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
            var application = entries.Single(entry => entry.AppUserModelId == AppUserModelId);
            context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Activating packaged fixture '{AppUserModelId}'.");
            Assert.IsTrue(
                application.LaunchAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult(),
                "Windows did not activate the packaged fixture.");
            var window = WaitForWindow(DefaultTitle);
            Assert.AreEqual(PackageFullName, NativeMethods.PackageFullName(window.ProcessId), "The fixture window must have the installed package identity.");
            Assert.IsFalse(window.IsElevated, "The packaged fixture must run non-elevated.");
            return window;
        }

        internal Session WaitForWindow(string title, int timeoutMS = 45_000)
        {
            var ready = WaitHelper.WaitForStable(
                () => FindWindow(title),
                window => window is not null,
                timeoutMS,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 150);
            Assert.IsTrue(ready.Succeeded, $"Fixture window '{title}' did not appear. Owned PIDs: {string.Join(", ", ProcessIds())}.");
            var match = ready.LastObservation!;
            var session = WindowsFinder.WaitForWindowByApp(
                match.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                window => window.Hwnd == match.Hwnd,
                timeoutMS: 5_000);
            Assert.IsNotNull(session, $"The ready fixture HWND disappeared: {match}.");
            return session;
        }

        internal WindowsFinder.WindowInfo? FindWindow(string title)
        {
            var ids = ProcessIds();
            return WindowsFinder.ListAll().SingleOrDefault(window => ids.Contains(window.ProcessId) && window.Title == title);
        }

        internal IReadOnlyList<int> ProcessIds()
        {
            var ids = new List<int>();
            foreach (var process in Process.GetProcessesByName("Workspaces.TestApp"))
            {
                using (process)
                {
                    try
                    {
                        if (process.HasExited)
                        {
                            continue;
                        }

                        var path = process.MainModule?.FileName;
                        if (string.Equals(path, ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
                            (package is not null && string.Equals(path, PackagedExecutablePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            ids.Add(process.Id);
                        }
                    }
                    catch (InvalidOperationException) when (process.HasExited)
                    {
                    }
                    catch (Win32Exception) when (process.HasExited)
                    {
                    }
                }
            }

            return ids;
        }

        internal void CloseAll()
        {
            foreach (var id in ProcessIds())
            {
                Process process;
                try
                {
                    process = Process.GetProcessById(id);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                using (process)
                {
                    var windows = WindowControl.EnumerateProcessWindows([id]);
                    foreach (var window in windows)
                    {
                        Assert.IsTrue(WindowControl.TryCloseWindow(window.Hwnd.ToInt64()), $"Could not close fixture HWND {window.Hwnd}.");
                    }

                    if (!process.WaitForExit(windows.Count == 0 ? 0 : 5_000))
                    {
                        process.Kill(entireProcessTree: true);
                        Assert.IsTrue(process.WaitForExit(10_000), $"The test-owned fixture PID {id} did not exit.");
                    }
                }
            }

            Assert.HasCount(0, ProcessIds(), "A test-owned fixture process survived cleanup.");
        }

        public void Dispose()
        {
            try
            {
                CloseAll();
            }
            finally
            {
                if (installation is { IsCompleted: false })
                {
                    installationOperation!.Cancel();
                    try
                    {
                        installation.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                if (ownsRegistration)
                {
                    foreach (var registered in RegisteredPackages())
                    {
                        Assert.AreEqual(Publisher, registered.Id.Publisher, "Refusing to remove an unrelated package.");
                        var removed = packageManager.RemovePackageAsync(registered.Id.FullName)
                            .AsTask().WaitAsync(TimeSpan.FromSeconds(90)).GetAwaiter().GetResult();
                        Assert.IsNull(removed.ExtendedErrorCode, $"Could not remove the fixture package: {removed.ErrorText}");
                    }

                    Assert.HasCount(0, RegisteredPackages(), "The fixture package remained registered after cleanup.");
                    ownsRegistration = false;
                    package = null;
                }
            }
        }

        private void EnsurePackage(TestContext context)
        {
            if (package is not null)
            {
                return;
            }

            var packagePath = Path.Combine(AppContext.BaseDirectory, "Workspaces.TestApp.msix");
            Assert.IsTrue(File.Exists(packagePath), $"The signed fixture package was not staged: {packagePath}");
            Assert.HasCount(0, RegisteredPackages(), "A previous fixture package is still registered; remove it before starting a new suite.");
            context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Installing the signed fixture for the current user: {packagePath}");
            ownsRegistration = true;
            installationOperation = packageManager.AddPackageAsync(new Uri(packagePath), [], DeploymentOptions.None);
            installation = installationOperation.AsTask();
            var deployment = installation.WaitAsync(TimeSpan.FromSeconds(90)).GetAwaiter().GetResult();
            Assert.IsNull(
                deployment.ExtendedErrorCode,
                $"Fixture deployment failed: {deployment.ErrorText}. Activity: {deployment.ActivityId}. " +
                "Stage a package signed by the shared UI-test signing setup and trust its public certificate in the test VM.");
            package = RegisteredPackages().Single();
            Assert.AreEqual(Publisher, package.Id.Publisher, "Unexpected fixture publisher.");
            Assert.AreEqual(
                RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
                package.Id.Architecture.ToString().ToLowerInvariant(),
                "Fixture architecture must match the test host.");
        }

        private Package[] RegisteredPackages() =>
            packageManager.FindPackagesForUser(string.Empty).Where(candidate => candidate.Id.Name == PackageName).ToArray();
    }
}
