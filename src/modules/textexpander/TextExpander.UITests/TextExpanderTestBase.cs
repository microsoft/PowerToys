// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// Starts PowerToys with only Text Expander enabled, after seeding its settings.json and snippets database, and puts
/// the user's files back afterwards. Tests drive Settings and the snippet editor through their AutomationIds and type
/// triggers into <see cref="TypingTarget"/>, a window of the test process, reading the result from it.
/// </summary>
public abstract class TextExpanderTestBase : UITestBase
{
    internal const string EngineProcessName = "PowerToys.TextExpander";

    // The runner starts the engine with --instance:powertoys, which suffixes its message window's class.
    internal const string EngineMessageWindowClass = "PowerToys.TextExpander.MessageWindow.powertoys";

    internal static readonly Snippet Greeting = new(";tehi", "Hello from the Text Expander UI test");
    internal static readonly Snippet Address = new(";teaddr", "One Microsoft Way\nRedmond, WA 98052");
    internal static readonly Snippet Letter = new(";tecur", "Dear $|$,\nSent {{isodate}}");
    internal static readonly Snippet Ticket = new(";teask", "Ticket {{prompt:Ticket number}} is resolved.");
    internal static readonly Snippet Note = new(";tenote", "Note: {{prompt:Your note}}.");

    internal static readonly IReadOnlyList<Snippet> DefaultSnippets = [Greeting, Address, Letter, Ticket, Note];

    private static readonly string[] EnabledModules = [TextExpanderFiles.ModuleKey];

    private TextExpanderDataSnapshot? snapshot;
    private TypingTarget? target;

    protected TextExpanderTestBase()
        : base(PowerToysModule.PowerToysSettings, enableModules: EnabledModules)
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames { get; } =
    [
        "PowerToys",
        "PowerToys.Settings",
        "PowerToys.FancyZonesEditor",
        EngineProcessName,
        SnippetEditor.ProcessName,
    ];

    /// <summary>Gets the window the test types triggers into, created on first use.</summary>
    internal TypingTarget Target => target ??= new TypingTarget(Step);

    /// <summary>Gets the options settings.json starts with. Read before PowerToys starts, so a test can choose by name.</summary>
    internal virtual TextExpanderOptions InitialOptions => new();

    /// <summary>Gets the snippets the database starts with; null starts without a database.</summary>
    internal virtual IReadOnlyList<Snippet>? InitialSnippets => DefaultSnippets;

    internal static int[] ProcessIds(string exactName)
    {
        var processes = Process.GetProcessesByName(exactName);
        try
        {
            return processes.Select(process => process.Id).ToArray();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    internal static bool IsEngineRunning() => ProcessIds(EngineProcessName).Length > 0;

    internal static bool WaitForEngine(bool running, int timeoutMS = 30_000) =>
        WaitHelper.WaitForStable(IsEngineRunning, actual => actual == running, timeoutMS, requiredConsecutiveMatches: 3, pollIntervalMS: 250).Succeeded;

    /// <summary>
    /// Waits until the engine the runner started is reading keys. It creates its message window once its snippets are
    /// loaded, right before it installs its keyboard hook.
    /// </summary>
    internal void WaitForEngineReady(int timeoutMS = 60_000)
    {
        Step("Waiting for the Text Expander engine to start");
        var ready = WaitHelper.WaitForStable(
            () => WindowControl.AnyWindowOfClassExists(EngineMessageWindowClass) && IsEngineRunning(),
            started => started,
            timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);
        Assert.IsTrue(ready.Succeeded, $"PowerToys.TextExpander did not start within {timeoutMS} ms (running: {IsEngineRunning()}).");

        // The keyboard hook goes in right after the window.
        Thread.Sleep(500);
        Step($"The engine is running ({DescribeEngine()})");
    }

    /// <summary>Describes the running engines, with how long ago each started, for the log.</summary>
    internal static string DescribeEngine()
    {
        var processes = Process.GetProcessesByName(EngineProcessName);
        try
        {
            return processes.Length == 0
                ? "not running"
                : string.Join(", ", processes.Select(process => $"process {process.Id} started {(DateTime.Now - process.StartTime).TotalSeconds:0.0} s ago"));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"<unavailable: {ex.Message}>";
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    protected override void PrepareTestState()
    {
        // The hygiene pass doesn't wait, and either process could still be writing the files about to be seeded.
        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(SnippetEditor.ProcessName, 15_000), "A snippet editor from an earlier run did not exit.");
        Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(EngineProcessName, 15_000), "A Text Expander engine from an earlier run did not exit.");

        snapshot ??= TextExpanderDataSnapshot.Capture();
        TextExpanderFiles.WriteOptions(InitialOptions);
        if (InitialSnippets is { } snippets)
        {
            TextExpanderFiles.WriteSnippets(snippets);
        }
        else
        {
            TextExpanderFiles.DeleteSnippets();
        }
    }

    [TestCleanup]
    public async Task CleanupTextExpanderTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));

        var failures = new List<string>();
        try
        {
            target?.Dispose();
        }
        catch (Exception ex)
        {
            failures.Add($"typing target: {ex.Message}");
        }

        target = null;
        TextInput.ReleaseModifiers();

        // Both processes write these files, so they stop before the user's files go back.
        if (!WindowControl.TryKillProcessTreeByNameAndWait(SnippetEditor.ProcessName, 15_000))
        {
            failures.Add("the snippet editor did not exit");
        }

        if (!WindowControl.TryKillProcessTreeByNameAndWait(EngineProcessName, 15_000))
        {
            failures.Add("the Text Expander engine did not exit");
        }

        try
        {
            snapshot?.Dispose();
        }
        catch (Exception ex)
        {
            failures.Add($"restoring the Text Expander files: {ex.Message}");
        }

        snapshot = null;
        if (failures.Count > 0)
        {
            Assert.Fail("Text Expander test cleanup failed: " + string.Join("; ", failures));
        }
    }

    /// <summary>Logs a step with a timestamp, so a hang shows the step it stopped at.</summary>
    protected void Step(string message) =>
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    /// <summary>Opens Settings' Text Expander page and returns a session for the Settings process.</summary>
    protected Session OpenTextExpanderPage()
    {
        Step("Binding to PowerToys Settings");
        var settings = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings, timeoutMS: 30_000);
        if (!settings.Has(By.AccessibilityId("TextExpanderNavItem"), timeoutMS: 1_000))
        {
            Step("Expanding Input / Output");
            settings.Find<NavigationViewItem>(By.AccessibilityId("InputOutputNavItem"), 15_000).Click(msPostAction: 500);
        }

        Step("Opening the Text Expander page");
        settings.Find<NavigationViewItem>(By.AccessibilityId("TextExpanderNavItem"), 15_000).Click(msPostAction: 500);
        Assert.IsTrue(
            settings.WaitForElement(By.AccessibilityId("Toggle_TextExpander"), 20_000),
            "Settings did not show the Text Expander page.");
        return settings;
    }

    /// <summary>Turns a Settings switch on or off through UI Automation and waits for it to show the new state.</summary>
    protected static void SetSwitch(Session settings, string automationId, bool on)
    {
        var toggle = settings.Find<ToggleSwitch>(By.AccessibilityId(automationId), 10_000);
        if (toggle.IsOn != on)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(
            toggle.WaitForProperty("ToggleState", on ? "On" : "Off", 10_000),
            $"The {automationId} switch did not turn {(on ? "on" : "off")}.");
    }

    protected static bool IsSwitchOn(Session settings, string automationId) =>
        settings.Find<ToggleSwitch>(By.AccessibilityId(automationId), 10_000).IsOn;

    /// <summary>Selects Manage snippets on the Text Expander page and waits for the editor.</summary>
    internal SnippetEditor OpenEditor(Session settings)
    {
        Step("Selecting Manage snippets");
        var button = settings.Find<Button>(By.AccessibilityId("Button_TextExpander_ManageSnippets"), 10_000);
        Assert.IsTrue(button.IsEnabled, "Manage snippets is off.");
        button.Invoke(msPostAction: 0);
        return SnippetEditor.WaitForWindow(Step);
    }

    /// <summary>Waits for a settings.json option to have <paramref name="expected"/>.</summary>
    protected void AssertOptionSaved(string name, object expected)
    {
        object? actual = null;
        var saved = WaitHelper.WaitForStable(
            () => actual = expected is bool ? TextExpanderFiles.ReadBoolOption(name) : TextExpanderFiles.ReadStringOption(name),
            value => Equals(value, expected),
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250).Succeeded;
        Assert.IsTrue(saved, $"settings.json has {name} = {actual ?? "<missing>"}, not {expected}.");
    }

    /// <summary>
    /// Types <paramref name="typed"/> into an empty target and waits for <paramref name="expected"/>. The first
    /// expansion after PowerToys starts also proves the engine is ready, so it may retry while nothing was replaced.
    /// Once the engine has taken the trigger, it gets time to finish, and the text must then be exactly what's expected.
    /// </summary>
    internal void AssertExpands(string typed, string expected, bool waitForEngine = false) =>
        AssertExpandsTo(typed, text => text == expected, TextExpanderFiles.Visible(expected), waitForEngine);

    /// <inheritdoc cref="AssertExpands"/>
    internal void AssertExpandsTo(string typed, Func<string?, bool> isExpected, string description, bool waitForEngine = false)
    {
        if (waitForEngine)
        {
            WaitForEngineReady();
        }

        var attempts = waitForEngine ? 4 : 1;
        string? last = null;
        var diagnostics = string.Empty;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            Step($"Typing '{TextExpanderFiles.Visible(typed)}' into the target (attempt {attempt} of {attempts})");
            Target.Clear();
            Target.Focus();
            var cpu = EngineDiagnostics.SampleCpu();
            TextInput.Type(typed);

            // An expansion takes a second or two; a trigger still as typed after that means the engine wasn't ready yet.
            var shown = new TextHistory(() => Target.Text);
            var result = WaitHelper.WaitForStable(
                shown.Read,
                isExpected,
                timeoutMS: attempt < attempts ? 8_000 : 15_000,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 200);
            if (!result.Succeeded && result.LastObservation != typed)
            {
                // The engine took the trigger (it swallows the key that completes one). On a busy machine, such as one
                // core with Windows Update running, the insertion can take far longer than usual.
                Step($"The engine is replacing the trigger: {shown}. Waiting for it to finish");
                result = WaitHelper.WaitForStable(shown.Read, isExpected, timeoutMS: 30_000, requiredConsecutiveMatches: 3, pollIntervalMS: 200);
            }

            if (result.Succeeded)
            {
                Step($"Target shows '{TextExpanderFiles.Visible(result.LastObservation)}' ({shown})");
                return;
            }

            last = result.LastObservation;
            diagnostics = $"Target since typing: {shown}. {EngineDiagnostics.DescribeCpu(cpu)}; {EngineDiagnostics.DescribeWinapp()}. " +
                $"Engine: {DescribeEngine()}; {EngineDiagnostics.Read()}. Foreground: {WindowControl.GetForegroundWindowInfo()}.";
            Step($"Target shows '{TextExpanderFiles.Visible(last)}'. {diagnostics}");
            if (last != typed)
            {
                break;
            }
        }

        Assert.Fail(
            $"Typing '{TextExpanderFiles.Visible(typed)}' should give '{description}', but the target shows '{TextExpanderFiles.Visible(last)}'. " +
            $"Database: {TextExpanderFiles.Describe(TextExpanderFiles.ReadSnippets())}. {diagnostics}");
    }

    /// <summary>Types <paramref name="typed"/> into an empty target and checks that it stays as typed.</summary>
    internal void AssertNotExpanded(string typed, int observeMS = 3_000)
    {
        Step($"Typing '{TextExpanderFiles.Visible(typed)}', which must not expand");
        Target.Clear();
        Target.Focus();
        TextInput.Type(typed);
        AssertStaysAsTyped(() => Target.Text, typed, observeMS, "the target");
    }

    /// <summary>
    /// Waits for <paramref name="typed"/> to arrive whole, then checks it stays that way for <paramref name="observeMS"/>.
    /// The engine swallows the key that completes a trigger it matched, so a matched trigger never arrives whole. Timed
    /// by the clock rather than by counting reads: on one CPU a UI Automation read can take seconds.
    /// </summary>
    internal static void AssertStaysAsTyped(Func<string?> read, string typed, int observeMS, string where)
    {
        var arrived = WaitHelper.WaitForStable(read, text => text == typed, timeoutMS: 10_000, pollIntervalMS: 100);
        Assert.IsTrue(arrived.Succeeded, $"'{TextExpanderFiles.Visible(typed)}' didn't arrive whole: {where} shows '{TextExpanderFiles.Visible(arrived.LastObservation)}'.");

        var clock = Stopwatch.StartNew();
        while (true)
        {
            var readAt = clock.ElapsedMilliseconds;
            var text = read();
            if (text != typed)
            {
                Assert.Fail($"'{TextExpanderFiles.Visible(typed)}' should stay as typed, but after {readAt} ms {where} shows '{TextExpanderFiles.Visible(text)}'.");
            }

            if (readAt >= observeMS)
            {
                return;
            }

            Thread.Sleep(250);
        }
    }

    /// <summary>Types a trigger whose snippet asks a question, and returns the question window.</summary>
    internal EnginePrompt TypeTriggerForPrompt(string typed, bool waitForEngine = true)
    {
        if (waitForEngine)
        {
            WaitForEngineReady();
        }

        var attempts = waitForEngine ? 4 : 1;
        var diagnostics = string.Empty;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            Step($"Typing '{typed}' into the target (attempt {attempt} of {attempts})");
            Target.Clear();
            Target.Focus();
            TextInput.Type(typed);

            Step("Waiting for the question window");
            var prompt = EnginePrompt.WaitForOpen(15_000);
            if (prompt is null && Target.Text != typed)
            {
                // The engine took the trigger; on a busy machine its window can take far longer than usual to open.
                Step($"The engine took the trigger ('{TextExpanderFiles.Visible(Target.Text)}'); waiting longer for its question window");
                prompt = EnginePrompt.WaitForOpen(30_000);
            }

            if (prompt is not null)
            {
                return prompt;
            }

            var text = Target.Text;
            diagnostics = $"{EngineDiagnostics.DescribeWinapp()}. Engine: {DescribeEngine()}; {EngineDiagnostics.Read()}.";
            Step($"No question window; the target shows '{TextExpanderFiles.Visible(text)}'. {diagnostics}");
            if (text != typed)
            {
                break;
            }
        }

        Assert.Fail($"Typing '{typed}' did not open Text Expander's question window. The target shows '{TextExpanderFiles.Visible(Target.Text)}'. {diagnostics}");
        return null!;
    }
}
