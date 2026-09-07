// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.EnvironmentVariables.UITests;

[TestClass]
[TestCategory("EnvironmentVariables")]
[DoNotParallelize]
public sealed class EnvironmentVariablesTests : UITestBase
{
    private static TestState state = null!;
    private static bool ownsPreparedScope;
    private EditorUi editor = null!;
    private string prefix = string.Empty;

    public EnvironmentVariablesTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["EnvironmentVariables"])
    {
    }

    [ClassInitialize]
    public static void InitializeState(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        state = new TestState();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreState()
    {
        try
        {
            // Initialization can fail before per-test cleanup runs; retain the owned-scope safety net.
            StopPreparedScope();
        }
        finally
        {
            state?.Dispose();
        }
    }

    protected override void PrepareTestState()
    {
        // CI agents can launch the test host elevated. Use the Runner's supported opt-out before
        // attaching Settings, rather than attempting to invoke its disabled administrator switch.
        // TestState journals the original global settings before this baseline is changed.
        TestState.ConfigureNonElevatedRunner();
        ownsPreparedScope = true;
        StartRunner("--dont-elevate");
        var ready = WaitHelper.WaitForStable(
            () =>
            {
                var processes = Process.GetProcessesByName("PowerToys");
                try
                {
                    return processes.Length == 1 && ElevationHelper.IsProcessElevated(processes[0].Id) == false;
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            },
            match => match,
            timeoutMS: 60_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 200);
        Assert.IsTrue(ready.Succeeded, "The Runner did not establish a non-elevated instance.");

        StartRunner("--open-settings");
        var settings = WindowsFinder.WaitForWindowByApp("PowerToys.Settings", window => window.Width > 400 && window.Height > 300, timeoutMS: 90_000);
        Assert.IsNotNull(settings, "The non-elevated Runner did not open Settings.");
        Assert.IsFalse(settings.IsElevated, "Settings must be non-elevated so the administrator launch option is editable.");
    }

    private void StartRunner(string arguments)
    {
        string executable = SessionHelper.GetExecutablePath(PowerToysModule.Runner);
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Starting Runner {arguments}");
        using var process = Process.Start(new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = Path.GetDirectoryName(executable),
            UseShellExecute = true,
        });
        Assert.IsNotNull(process, "The Runner launch did not return a process.");
    }

    private static void StopPreparedScope()
    {
        if (!ownsPreparedScope)
        {
            return;
        }

        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait("PowerToys.Settings", 10_000), "Could not close the test-owned Settings process.");
        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait("PowerToys", 10_000), "Could not close the test-owned Runner process.");
        ownsPreparedScope = false;
    }

    [TestInitialize]
    public void OpenEditor()
    {
        state.Reset();
        prefix = $"PTUITest_{Guid.NewGuid():N}";
        LaunchEditor();
    }

    [TestCleanup]
    public async Task RestoreTestState()
    {
        try
        {
            await CaptureFailureArtifactsBeforeCleanupAsync();
            if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed && editor is not null)
            {
                TryCaptureEditorTree();
            }
        }
        finally
        {
            try
            {
                state.Reset();
            }
            finally
            {
                StopPreparedScope();
            }
        }
    }

    private void TryCaptureEditorTree()
    {
        try
        {
            string? directory = TestContext.TestRunResultsDirectory;
            if (string.IsNullOrWhiteSpace(directory))
            {
                TestContext.WriteLine("The test results directory is unavailable; using temporary storage for the editor diagnostic.");
                directory = Path.GetTempPath();
            }

            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"{TestContext.TestName}-{Guid.NewGuid():N}-editor.json");
            File.WriteAllText(path, editor.Session.Inspect(depth: 30).GetRawText());
            TestContext.AddResultFile(path);
        }
        catch (Exception error) when (error is AssertFailedException or IOException or UnauthorizedAccessException or TimeoutException or Win32Exception or InvalidOperationException or ArgumentException or JsonException)
        {
            TestContext.WriteLine($"Could not capture the editor UIA diagnostic: {error.Message}");
        }
    }

    [TestMethod]
    public void StandardUserCannotEditSystemVariables()
    {
        Assert.IsTrue(
            WindowHelper.IsWindowMaximized(new IntPtr(editor.Session.WindowHandle)),
            "The editor must be maximized, matching the Settings window's default state.");
        Assert.IsFalse(editor.Session.IsElevated, "Launch as administrator OFF must launch a non-elevated editor.");
        Assert.IsTrue(editor.Session.Find<Button>(By.AccessibilityId("AddDefaultVariableUserBtn")).IsEnabled);
        Assert.IsFalse(editor.Session.Find<Button>(By.AccessibilityId("AddDefaultVariableSystemBtn")).IsEnabled);
        var system = editor.ExpandDefaultSet(EnvironmentVariableTarget.Machine);
        var path = editor.VariableCard(system, "Path");
        var liveBounds = editor.Session.Find<Element>(By.Slug(path.Selector));
        Assert.IsTrue(
            liveBounds.Width > 0 && liveBounds.Height > 0,
            $"Live winapp ui inspect must preserve slug bounds for {path.Selector}; got {liveBounds.Width}x{liveBounds.Height}.");
        var options = editor.Child<Button>(path, "Button", automationId: "VariableOptionsButton");
        Assert.IsFalse(options.IsEnabled, "System variables must be read-only.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(options.Name), "Variable options must expose an accessible name.");
    }

    [TestMethod]
    public void UserVariableCrudUpdatesAppliedVariables()
    {
        string name = Track("User");
        editor.AddUserVariableAndVerify(name, "original");
        editor.VariableMenu(EnvironmentVariableTarget.User, name, "Edit");
        editor.Session.Find<TextBox>(By.AccessibilityId("EditVariableDialogValueTxtBox")).SetText("edited");
        editor.SaveDialog();
        EditorUi.AssertUserVariable(name, "edited");
        editor.AssertAppliedVariable(name, "edited");
        editor.VariableMenu(EnvironmentVariableTarget.User, name, "Remove");
        editor.ConfirmRemoval();
        EditorUi.AssertUserVariable(name, null);
        editor.AssertAppliedVariable(name, null);
    }

    [TestMethod]
    public void ProfilesCanBeCreatedEditedSwitchedAndRemoved()
    {
        string first = prefix + "_Profile1";
        string second = prefix + "_Profile2";
        string one = Track("One");
        string two = Track("Two");
        string three = Track("Three");

        editor.CreateProfileAndVerify(first, false);
        Assert.AreEqual(0, EditorUi.ReadProfiles().EnumerateArray().Single().GetProperty("Variables").GetArrayLength(), "The first profile should initially be empty.");
        editor.EditProfileAndVerify(first, (one, "first"));
        editor.CreateProfileAndVerify(second, true, (two, "second"), (three, "third"));
        EditorUi.AssertUserVariable(two, "second");
        EditorUi.AssertUserVariable(three, "third");
        editor.AssertAppliedVariable(two, "second");
        editor.AssertAppliedVariable(three, "third");

        editor.SetProfileEnabledAndVerify(first, true);
        editor.AssertProfile(second, false);
        Assert.IsFalse(editor.Child<ToggleSwitch>(editor.Expander(second), "ToggleSwitch").IsOn, "Applying a profile must turn off the previously active profile's switch.");
        EditorUi.AssertUserVariable(one, "first");
        EditorUi.AssertUserVariable(two, null);
        EditorUi.AssertUserVariable(three, null);
        editor.AssertAppliedVariable(one, "first");
        editor.AssertAppliedVariable(two, null);
        editor.AssertAppliedVariable(three, null);

        editor.SetProfileEnabledAndVerify(first, false);
        EditorUi.AssertUserVariable(one, null);
        editor.AssertAppliedVariable(one, null);
        editor.RemoveProfileAndVerify(first);
        editor.RemoveProfileAndVerify(second);
        Assert.AreEqual(0, EditorUi.ReadProfiles().GetArrayLength(), "Both profiles must be gone from profiles.json.");
    }

    [TestMethod]
    public void ExistingVariableOverrideCreatesAndRestoresBackup()
    {
        string name = Track("Override");
        string profile = prefix + "_OverrideProfile";
        string backup = name + "_PowerToys_" + profile;
        state.TrackUserVariable(backup);
        editor.AddUserVariableAndVerify(name, "original");
        editor.CreateProfileAndVerify(profile, false, (name, "overridden"));
        editor.SetProfileEnabledAndVerify(profile, true);
        EditorUi.AssertUserVariable(name, "overridden");
        EditorUi.AssertUserVariable(backup, "original");
        editor.AssertAppliedVariable(name, "overridden");
        editor.AssertAppliedVariable(backup, "original");
        editor.SetProfileEnabledAndVerify(profile, false);
        EditorUi.AssertUserVariable(name, "original");
        EditorUi.AssertUserVariable(backup, null);
        editor.AssertAppliedVariable(name, "original");
        editor.AssertAppliedVariable(backup, null);
        editor.RemoveProfileAndVerify(profile);
    }

    [TestMethod]
    public void PathEntriesCanBeReorderedInsertedAndDeleted()
    {
        string profile = prefix + "_PathEditor";
        editor.CreateProfileAndVerify(profile, false, ("PATH", "path1;path2;path3"));
        editor.AssertPathList(profile, "path1", "path2", "path3");
        editor.VariableMenu(profile, "PATH", "Edit");

        editor.PathEntryMenu(1, "MoveUp");
        editor.AssertPathEditor("path2;path1;path3");
        editor.PathEntryMenu(0, "MoveDown");
        editor.AssertPathEditor("path1;path2;path3");
        editor.PathEntryMenu(0, "MoveUp");
        editor.AssertPathEditor("path1;path2;path3");
        editor.PathEntryMenu(2, "MoveDown");
        editor.AssertPathEditor("path1;path2;path3");
        editor.PathEntryMenu(1, "InsertBefore");
        editor.SetPathEntry(1, "before");
        editor.AssertPathEditor("path1;before;path2;path3");
        editor.PathEntryMenu(2, "InsertAfter");
        editor.SetPathEntry(3, "after");
        editor.AssertPathEditor("path1;before;path2;after;path3");
        editor.PathEntryMenu(0, "Delete");
        editor.AssertPathEditor("before;path2;after;path3");
        editor.SaveDialog();
        editor.AssertProfile(profile, false, ("PATH", "before;path2;after;path3"));
        editor.AssertPathList(profile, "before", "path2", "after", "path3");
        editor.RemoveProfileAndVerify(profile);
    }

    [TestMethod]
    public void AppliedPathSurvivesReopenAndDeletingProfileRestoresUserPath()
    {
        string profile = prefix + "_PathProfile";
        string backup = "PATH_PowerToys_" + profile;
        state.TrackUserVariable("Path");
        state.TrackUserVariable(backup);
        string? original = TestState.ReadUserVariable("Path");
        string system = Environment.ExpandEnvironmentVariables(TestState.ReadSystemVariable("Path")!);
        editor.AssertAppliedVariable("Path", system + (original is null ? string.Empty : ";" + Environment.ExpandEnvironmentVariables(original)));
        editor.CreateProfileAndVerify(profile, true, ("PATH", "path1;path2;path3"));
        EditorUi.AssertUserVariable("Path", "path1;path2;path3");
        EditorUi.AssertUserVariable(backup, original);
        editor.AssertAppliedVariable("Path", system + ";path1;path2;path3");

        editor.Step("Closing and reopening the editor to read persisted profile state");
        WindowControl.TryCloseByApp(TestState.ProcessName);
        EditorUi.Wait(() => !WindowsFinder.ListByApp(TestState.ProcessName).Any(), "The editor window did not close.");
        LaunchEditor();
        editor.AssertProfile(profile, true, ("PATH", "path1;path2;path3"));
        Assert.IsTrue(editor.Child<ToggleSwitch>(editor.Expander(profile), "ToggleSwitch").IsOn);
        EditorUi.AssertUserVariable("Path", "path1;path2;path3");
        editor.AssertAppliedVariable("Path", system + ";path1;path2;path3");

        editor.RemoveProfileAndVerify(profile);
        EditorUi.AssertUserVariable("Path", original);
        EditorUi.AssertUserVariable(backup, null);
        editor.AssertAppliedVariable("Path", system + (original is null ? string.Empty : ";" + Environment.ExpandEnvironmentVariables(original)));
        editor.AssertAppliedVariable(backup, null);
    }

    private void LaunchEditor()
    {
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Launching Environment Variables from Settings with administrator mode OFF.");
        if (!Session.Has(By.AccessibilityId("EnvironmentVariablesNavItem"), 500))
        {
            Session.Find<NavigationViewItem>(By.AccessibilityId("AdvancedNavItem")).Click(msPostAction: 0);
        }

        Session.Find<NavigationViewItem>(By.AccessibilityId("EnvironmentVariablesNavItem")).Click(msPostAction: 0);
        var settings = new EditorUi(Session, TestContext);
        var adminCard = Session.Find<Element>(By.AccessibilityId("EnvironmentVariablesToggleLaunchAdministrator"));
        EditorUi.SetToggle(settings.Child<ToggleSwitch>(adminCard, "ToggleSwitch"), false);
        var launchCard = Session.Find<Element>(By.AccessibilityId("EnvironmentVariablesLaunchButtonControl"));
        launchCard.Focus();
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(Session.WindowHandle), timeoutMS: 10_000),
            $"Settings must own foreground before clicking its launch card: {WindowControl.GetForegroundWindowInfo()}");
        launchCard.Click(msPostAction: 0);
        var window = WindowsFinder.WaitForWindowByApp(TestState.ProcessName, candidate => candidate.Width > 400 && candidate.Height > 300, timeoutMS: 30_000);
        Assert.IsNotNull(window, "Environment Variables did not launch from Settings.");
        WindowHelper.MaximizeWindow(new IntPtr(window.WindowHandle));
        editor = new EditorUi(window, TestContext);
        Assert.IsTrue(window.WaitForElement(By.AccessibilityId("AddDefaultVariableUserBtn"), 15_000), "The editor did not become ready.");
    }

    private string Track(string suffix)
    {
        string name = prefix + "_" + suffix;
        state.TrackUserVariable(name);
        Assert.IsNull(TestState.ReadUserVariable(name), "The unique fixture name must not replace an existing variable.");
        return name;
    }
}
