// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Base class for the next-generation PowerToys UI tests. Engine is winappcli — every UI call
/// shells out to <c>winapp.exe</c>. No WinAppDriver, no Selenium, no third-party NuGet packages.
/// </summary>
/// <remarks>
/// <para>
/// Drop-in shape replacement for the existing <c>Microsoft.PowerToys.UITest.UITestBase</c>:
/// inherit, pass a <see cref="PowerToysModule"/>, and use <c>Session</c> / <c>Find&lt;T&gt;</c> in tests.
/// </para>
/// <para>
/// Test Explorer integration is automatic — MSTest's <c>[TestClass]</c> / <c>[TestInitialize]</c> /
/// <c>[TestCleanup]</c> plus the Microsoft.Testing.Platform runner (enabled repo-wide in
/// <c>Directory.Build.props</c>) are everything Test Explorer and <c>dotnet test</c> need.
/// </para>
/// </remarks>
[TestClass]
public class UITestBase : IDisposable
{
    /// <summary>
    /// Lazy one-shot probe for <c>winapp.exe</c>. Runs the first time any UITest in the
    /// process initializes — the cost is one extra <c>winapp --version</c> call per test run.
    /// </summary>
    private static readonly Lazy<bool> CliAvailable = new(WinappCli.IsAvailable);

    // Class-scoped reuse (opt-in via ReuseScopeAcrossTests): the launcher that owns the shared scope
    // and the test class it belongs to. UI tests never run in parallel, so one slot is enough; the
    // inherited ClassCleanup stops it once the owning class finishes.
    private static SessionHelper? keepAliveHelper;
    private static Type? keepAliveOwner;

    private readonly PowerToysModule scope;
    private readonly WindowSize windowSize;
    private readonly string[]? enableModules;
    private readonly bool isInPipeline = EnvironmentConfig.IsInPipeline;

    private SessionHelper? sessionHelper;
    private System.Threading.Timer? screenshotTimer;
    private ScreenRecording? screenRecording;
    private string? screenshotDirectory;
    private string? recordingDirectory;
    private bool artifactsCaptured;
    private bool disposed;

    public required TestContext TestContext { get; set; }

    public Session Session { get; private set; } = null!;

    /// <summary>
    /// PowerToys processes killed before every test so each run starts from a clean desktop state
    /// (mirrors the legacy harness's <c>CloseOtherApplications</c>). Override to extend the list with
    /// a module's helper processes. Matched by exact name, so short names like "PowerToys" don't hit
    /// unrelated processes.
    /// </summary>
    protected virtual IReadOnlyList<string> StaleProcessNames { get; } = new[]
    {
        "PowerToys",
        "PowerToys.Settings",
        "PowerToys.FancyZonesEditor",
    };

    /// <summary>
    /// When a derived class overrides this to <c>true</c>, the module is launched once for the whole
    /// class and the <b>same window is reused across every test method</b> (no per-test relaunch or
    /// desktop hygiene). The framework still captures failure media per test and stops the scope once
    /// the class finishes. Default <c>false</c> — each test gets an isolated launch + teardown.
    /// </summary>
    protected virtual bool ReuseScopeAcrossTests => false;

    /// <param name="scope">Module whose window the test drives.</param>
    /// <param name="size">Optional fixed window size applied once the window appears.</param>
    /// <param name="enableModules">
    /// When non-null, exactly these modules are enabled (and every other listed module disabled) in
    /// the global <c>settings.json</c> before the runner launches — a deterministic module baseline.
    /// Leave null to launch against whatever state <c>settings.json</c> already holds.
    /// </param>
    protected UITestBase(
        PowerToysModule scope = PowerToysModule.PowerToysSettings,
        WindowSize size = WindowSize.UnSpecified,
        string[]? enableModules = null)
    {
        this.scope = scope;
        this.windowSize = size;
        this.enableModules = enableModules;
    }

    [TestInitialize]
    public void TestInit()
    {
        if (!CliAvailable.Value)
        {
            Assert.Fail(WinappCli.InstallHint);
        }

        try
        {
            // Reuse the already-open window from a previous test in this class when the class opted
            // into a shared scope and it's still alive — skip the hygiene that would minimize/kill it.
            var reuse = ReuseScopeAcrossTests
                && keepAliveOwner == GetType()
                && SessionHelper.IsRunning(scope);

            if (!reuse)
            {
                // Pin the display to a known resolution so coordinate-sensitive tests are
                // deterministic, and snapshot the monitor topology for post-mortem diagnostics.
                if (isInPipeline)
                {
                    DisplayHelper.NormalizeResolution(1920, 1080);
                    DisplayHelper.LogMonitors(TestContext);
                }

                PreTestHygiene();

                // Seed a deterministic module on/off baseline before the runner reads settings.json.
                if (enableModules is not null)
                {
                    SettingsConfigHelper.ConfigureGlobalModuleSettings(enableModules);
                }
            }

            // Start the 1s screenshot timer + FFmpeg recording before the UI work so the artifacts
            // cover the whole test.
            if (isInPipeline)
            {
                StartPipelineCapture();
            }

            sessionHelper = new SessionHelper(scope);
            Session = sessionHelper.Init();  // launches when needed; reuses a running instance otherwise

            ApplyWindowSize();

            // Remember the launcher so the inherited ClassCleanup can stop the shared scope at the
            // end of the class.
            if (ReuseScopeAcrossTests && !reuse)
            {
                keepAliveHelper = sessionHelper;
                keepAliveOwner = GetType();
            }
        }
        catch
        {
            // MSTest does NOT run [TestCleanup] when [TestInitialize] throws, so capture the failure
            // media here (e.g. the window never appeared) before propagating — otherwise an init
            // failure would attach no diagnostics at all.
            CaptureFailureArtifacts();
            throw;
        }
    }

    [TestCleanup]
    public void TestCleanup()
    {
        var failed = TestContext.CurrentTestOutcome is
            UnitTestOutcome.Failed or UnitTestOutcome.Error or UnitTestOutcome.Unknown;

        if (failed)
        {
            CaptureFailureArtifacts();
        }
        else if (isInPipeline)
        {
            // Passing test: stop the capture and discard the (now uninteresting) recording.
            StopPipelineCapture();
            CleanupRecordingDirectory();
        }

        // Tear the scope down only when each test owns its launch. With a class-shared scope the
        // window must survive for the next test; the inherited ClassCleanup stops it at class end.
        if (!ReuseScopeAcrossTests)
        {
            try
            {
                sessionHelper?.StopIfStarted();
            }
            catch
            {
            }
        }

        Dispose();
    }

    /// <summary>
    /// Stop a class-shared scope (see <see cref="ReuseScopeAcrossTests"/>) once the owning class's
    /// tests finish. Runs after every derived class via inheritance; a no-op for classes that never
    /// kept a scope alive.
    /// </summary>
    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void StopSharedScope()
    {
        try
        {
            keepAliveHelper?.StopIfStarted();
        }
        catch
        {
        }

        keepAliveHelper = null;
        keepAliveOwner = null;
    }

    /// <summary>
    /// Collect every diagnostic for a failed test and attach it: a window-independent desktop
    /// screenshot always, plus (in pipeline mode) the 1s screenshot trail, the screen recording, and
    /// the PowerToys log files. Idempotent and fully tolerant — runs from both the <see cref="TestInit"/>
    /// failure path (where <c>[TestCleanup]</c> won't fire) and <see cref="TestCleanup"/>.
    /// </summary>
    private void CaptureFailureArtifacts()
    {
        if (artifactsCaptured)
        {
            return;
        }

        artifactsCaptured = true;

        if (isInPipeline)
        {
            try
            {
                StopPipelineCapture();
            }
            catch
            {
            }
        }

        try
        {
            CaptureFailureScreenshot();
        }
        catch
        {
        }

        if (isInPipeline)
        {
            try
            {
                AddScreenshotsToTestResults();
                AddRecordingsToTestResults();
                AddLogFilesToTestResults();
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Attach a failure screenshot. The primary capture is a window-independent GDI grab of the
    /// desktop, so it works even when the test's window was already closed (e.g. by the test's own
    /// <c>finally</c>) or never appeared (an init failure) — unlike winappcli's <c>--capture-screen</c>,
    /// which requires a live target window. When the session window is still live, a winapp
    /// window/overlay shot is added too. Best-effort.
    /// </summary>
    private void CaptureFailureScreenshot()
    {
        var dir = TestContext.TestRunResultsDirectory ?? TestContext.TestResultsDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        var baseName = $"{TestContext.TestName}_{DateTime.Now:yyyyMMdd_HHmmss}";

        // Reliable, window-independent desktop grab.
        var desktopShot = Path.Combine(dir, $"{baseName}.png");
        if (ScreenCapture.TryCaptureDesktop(desktopShot) && File.Exists(desktopShot))
        {
            TestContext.AddResultFile(desktopShot);
        }

        // Bonus detail: the winapp window/overlay shot, only when the session window is still alive.
        if (Session is not null && Session.WindowHandle != 0)
        {
            var windowShot = Path.Combine(dir, $"{baseName}_window.png");
            try
            {
                if (Session.TryScreenshot(windowShot, captureScreen: true) && File.Exists(windowShot))
                {
                    TestContext.AddResultFile(windowShot);
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Bring the desktop to a known state before launching: minimize every window, dismiss any
    /// lingering popup with <c>Esc</c>, and kill the stale PowerToys processes in
    /// <see cref="StaleProcessNames"/>. Best-effort — never blocks a test from starting.
    /// </summary>
    private void PreTestHygiene()
    {
        try
        {
            // Minimize all windows so the test starts from a known desktop state.
            KeyboardHelper.SendKeys(Key.LWin, Key.M);

            // Dismiss any lingering popup / flyout.
            KeyboardHelper.SendKeys(Key.Esc);

            // Kill stale PowerToys processes so each test launches fresh.
            foreach (var processName in StaleProcessNames)
            {
                WindowControl.TryKillProcessByName(processName);
            }
        }
        catch
        {
            // Hygiene is opportunistic; a failure here must not fail the test.
        }
    }

    /// <summary>Apply the constructor's <see cref="WindowSize"/> to the resolved window, if any.</summary>
    private void ApplyWindowSize()
    {
        if (windowSize != WindowSize.UnSpecified && Session is not null && Session.WindowHandle != 0)
        {
            WindowHelper.SetWindowSize(new IntPtr(Session.WindowHandle), windowSize);
            Thread.Sleep(200);
        }
    }

    /// <summary>
    /// Force a clean restart of the scope (kill + relaunch + rebind to the fresh window), re-seeding
    /// the module baseline first. Equivalent to the legacy <c>RestartScopeExe</c>; assigns and returns
    /// the new <see cref="Session"/>.
    /// </summary>
    /// <param name="enableModules">
    /// Modules to enable before relaunch. When null, the baseline passed to the constructor (if any)
    /// is re-applied so the restart stays deterministic.
    /// </param>
    public Session RestartScope(string[]? enableModules = null)
    {
        var modules = enableModules ?? this.enableModules;
        if (modules is not null)
        {
            SettingsConfigHelper.ConfigureGlobalModuleSettings(modules);
        }

        Session = sessionHelper!.Restart();
        ApplyWindowSize();
        return Session;
    }

    // ----- Pipeline diagnostics (CI only) ---------------------------------------------------

    /// <summary>Start the 1s screenshot timer and FFmpeg screen recording. Best-effort.</summary>
    private void StartPipelineCapture()
    {
        try
        {
            var baseDirectory = TestContext.TestResultsDirectory ?? Path.GetTempPath();

            screenshotDirectory = Path.Combine(baseDirectory, "UITestScreenshots_" + Guid.NewGuid());
            Directory.CreateDirectory(screenshotDirectory);
            screenshotTimer = new System.Threading.Timer(
                ScreenCapture.TimerCallback, screenshotDirectory, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000));

            recordingDirectory = Path.Combine(baseDirectory, "UITestRecordings_" + Guid.NewGuid());
            Directory.CreateDirectory(recordingDirectory);
            try
            {
                screenRecording = new ScreenRecording(recordingDirectory);
                if (screenRecording.IsAvailable)
                {
                    _ = screenRecording.StartRecordingAsync();
                }
                else
                {
                    screenRecording = null;
                }
            }
            catch
            {
                screenRecording = null;
            }
        }
        catch
        {
            // Capture setup is best-effort; never block the test on it.
        }
    }

    /// <summary>Stop the screenshot timer and finalize the recording. Best-effort.</summary>
    private void StopPipelineCapture()
    {
        try
        {
            screenshotTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch
        {
        }

        if (screenRecording is not null)
        {
            try
            {
                screenRecording.StopRecordingAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }
        }
    }

    private void AddScreenshotsToTestResults()
    {
        if (screenshotDirectory is not null && Directory.Exists(screenshotDirectory))
        {
            foreach (var file in Directory.GetFiles(screenshotDirectory))
            {
                TestContext.AddResultFile(file);
            }
        }
    }

    private void AddRecordingsToTestResults()
    {
        if (recordingDirectory is not null && Directory.Exists(recordingDirectory))
        {
            foreach (var file in Directory.GetFiles(recordingDirectory, "*.mp4"))
            {
                TestContext.AddResultFile(file);
            }
        }
    }

    private void CleanupRecordingDirectory()
    {
        if (recordingDirectory is not null && Directory.Exists(recordingDirectory))
        {
            try
            {
                Directory.Delete(recordingDirectory, true);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Copy PowerToys <c>*.log</c> files (from both <c>%LocalAppData%</c> and <c>%LocalAppDataLow%</c>)
    /// into the test results so a failed CI run carries the module logs.
    /// </summary>
    private void AddLogFilesToTestResults()
    {
        try
        {
            var localLow = Path.Combine(
                Environment.GetEnvironmentVariable("USERPROFILE") ?? string.Empty,
                "AppData", "LocalLow", "Microsoft", "PowerToys");
            CopyLogFiles(localLow);

            var localAppData = Path.Combine(
                Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty,
                "Microsoft", "PowerToys");
            CopyLogFiles(localAppData);
        }
        catch
        {
            // Log collection is diagnostic-only.
        }
    }

    private void CopyLogFiles(string sourceDir, string relativePath = "")
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var logFile in Directory.GetFiles(sourceDir, "*.log"))
        {
            try
            {
                var fileName = Path.GetFileName(logFile);
                var prefix = string.IsNullOrEmpty(relativePath) ? string.Empty : relativePath.Replace("\\", "-") + "-";
                var destination = Path.Combine(
                    TestContext.TestResultsDirectory ?? Path.GetTempPath(), $"{prefix}{fileName}");
                File.Copy(logFile, destination, true);
                TestContext.AddResultFile(destination);
            }
            catch
            {
            }
        }

        foreach (var subdir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subdir);
            var newRelative = string.IsNullOrEmpty(relativePath) ? dirName : Path.Combine(relativePath, dirName);
            CopyLogFiles(subdir, newRelative);
        }
    }

    /// <summary>Find an element on the session's window. Shortcut for <c>Session.Find&lt;T&gt;</c>.</summary>
    protected T Find<T>(By by, int timeoutMS = 5000)
        where T : Element, new() => Session.Find<T>(by, timeoutMS);

    /// <summary>Find an element by Name. Shortcut for <c>Session.Find&lt;T&gt;(By.Name(name))</c>.</summary>
    protected T Find<T>(string name, int timeoutMS = 5000)
        where T : Element, new() => Session.Find<T>(By.Name(name), timeoutMS);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        screenshotTimer?.Dispose();
        screenRecording?.Dispose();
        GC.SuppressFinalize(this);
    }
}
