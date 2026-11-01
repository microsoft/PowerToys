// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Awake.UITests;

internal sealed class AwakeTerminal : IDisposable
{
    private readonly TestContext context;
    private readonly Process shell;
    private readonly string directory;
    private readonly string pidPath;
    private readonly string exitPath;
    private readonly string outputPath;
    private readonly Session terminal;
    private Process? awake;

    internal AwakeTerminal(TestContext context)
    {
        this.context = context;
        directory = Path.Combine(context.TestRunResultsDirectory ?? throw new InvalidOperationException("The test results directory is unavailable."), "AwakeCLI", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        pidPath = Path.Combine(directory, "pid.txt");
        exitPath = Path.Combine(directory, "exit.txt");
        outputPath = Path.Combine(directory, "terminal.txt");
        var title = $"Awake CLI {Guid.NewGuid():N}";

        var startup = $"$Host.UI.RawUI.WindowTitle={Quote(title)}; $ErrorActionPreference='Stop'; " +
            $"$pidFile={Quote(pidPath)}; $exitFile={Quote(exitPath)}; " +
            $"function prompt {{ 'AWAKE_READY> ' }}; " +
            $"Start-Transcript -Path {Quote(outputPath)}; Write-Host 'AWAKE_TERMINAL_READY'";
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = true,
        };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NoExit", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(startup)) })
        {
            startInfo.ArgumentList.Add(argument);
        }

        shell = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not open the PowerShell terminal.");
        Session? openedTerminal = null;
        try
        {
            openedTerminal = WindowsFinder.WaitForWindowByTitle(title, 60_000);
            terminal = openedTerminal
                ?? throw new InvalidOperationException("The Awake CLI terminal did not appear.");
            WindowHelper.MaximizeWindow(new IntPtr(terminal.WindowHandle));
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => File.Exists(outputPath) ? ReadOutput() : string.Empty,
                    text => text is not null && text.Contains("AWAKE_TERMINAL_READY", StringComparison.Ordinal),
                    15_000,
                    shouldRetryException: exception => exception is IOException).Succeeded,
                "The terminal shell did not finish initialization.");
            WaitForVisibleText("AWAKE_READY>");
        }
        catch (Exception initializationFailure)
        {
            var failures = new List<Exception>();
            CollectCleanupFailure(() => StopProcess(shell, "failed terminal"), failures);
            if (openedTerminal is not null)
            {
                CollectCleanupFailure(
                    () => Assert.IsTrue(WindowControl.TryCloseWindow(openedTerminal.WindowHandle), "The failed terminal window did not close."),
                    failures);
            }

            CollectCleanupFailure(shell.Dispose, failures);
            if (failures.Count > 0)
            {
                throw new AggregateException("Terminal initialization and cleanup failed.", new[] { initializationFailure }.Concat(failures));
            }

            throw;
        }
    }

    internal int ShellId => shell.Id;

    internal Process AwakeProcess => awake ?? throw new InvalidOperationException("No Awake command has been submitted.");

    internal void Start(string arguments, bool waitForCommandExit = false, int? expirationDelaySeconds = null)
    {
        var executable = Path.Combine(Path.GetDirectoryName(SessionHelper.GetExecutablePath(PowerToysModule.Runner))!, "PowerToys.Awake.exe");
        Assert.IsTrue(File.Exists(executable), $"The build under test is missing {executable}.");
        var nativeArguments = Quote(arguments);
        if (expirationDelaySeconds is int seconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);

            // Compute the deadline after the asynchronous UI paste, when the shell executes it.
            nativeArguments = $"('--expire-at \"'+[DateTimeOffset]::Now.AddSeconds({seconds}).ToString('O')+'\" '+{Quote(arguments)})";
        }

        var waitArgument = waitForCommandExit ? " -Wait" : string.Empty;
        var command = $"$awake=Start-Process -FilePath {Quote(executable)} -ArgumentList {nativeArguments} -PassThru -NoNewWindow{waitArgument}; " +
            "[void]$awake.Handle; $awake.Id | Set-Content $pidFile; $awake.WaitForExit(); " +
            "if ($null -eq $awake.ExitCode) { throw 'Awake exit code is unavailable.' }; " +
            "$awake.ExitCode | Set-Content $exitFile; ";
        command += "Write-Host ('AWAKE_EXIT:'+$awake.ExitCode)";
        context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] Submitting to terminal: PowerToys.Awake.exe {arguments}");
        Send(command);
        var result = WaitHelper.WaitForStable(
            () => File.Exists(pidPath) ? File.ReadAllText(pidPath).Trim() : string.Empty,
            text => int.TryParse(text, CultureInfo.InvariantCulture, out var pid) && pid > 0,
            20_000,
            shouldRetryException: exception => exception is IOException);
        Assert.IsTrue(result.Succeeded, $"The terminal did not launch Awake and publish its PID. Output: {ReadOutput()}");
        var processId = int.Parse(result.LastObservation!, CultureInfo.InvariantCulture);
        if (!waitForCommandExit && !File.Exists(exitPath))
        {
            awake = Process.GetProcessById(processId);
            _ = awake.SafeHandle;
        }
    }

    internal int WaitForExit(int timeoutMS = 30_000)
    {
        var result = WaitHelper.WaitForStable(
            () => File.Exists(exitPath) ? File.ReadAllText(exitPath).Trim() : string.Empty,
            text => int.TryParse(text, CultureInfo.InvariantCulture, out _),
            timeoutMS,
            shouldRetryException: exception => exception is IOException);
        Assert.IsTrue(result.Succeeded, $"Awake did not exit within {timeoutMS}ms. Terminal output: {ReadOutput()}");
        var code = int.Parse(result.LastObservation!, CultureInfo.InvariantCulture);
        WaitForVisibleText($"AWAKE_EXIT:{code}");
        return code;
    }

    internal void WaitForVisibleText(params string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentOutOfRangeException.ThrowIfZero(expected.Length);
        JsonElement lastTree = default;
        var result = WaitHelper.WaitForStable(
            () =>
            {
                lastTree = terminal.Inspect(depth: 12);
                return AwakeUi.Nodes(lastTree)
                    .Where(node =>
                        ((node.TryGetProperty("type", out var type) && type.GetString() == "Document") ||
                         (node.TryGetProperty("className", out var className) && className.GetString() == "TermControl")) &&
                        node.TryGetProperty("selector", out _))
                    .Select(node => terminal.Find<Element>(By.Slug(node.GetProperty("selector").GetString()!)).GetValue())
                    .ToArray();
            },
            values => values is not null && values.Any(value =>
                expected.All(text => value.ReplaceLineEndings(string.Empty).Contains(text, StringComparison.Ordinal))),
            15_000,
            2,
            shouldRetryException: ShellMenu.IsTransientElementException);
        Assert.IsTrue(result.Succeeded, $"Terminal UI did not show [{string.Join(", ", expected)}]. Readouts: {string.Join("\n", result.LastObservation ?? [])}. Tree: {lastTree}");
    }

    internal string ReadOutput()
    {
        using var stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal void StopParent()
    {
        // Do not kill the tree: Awake must detect its bound parent's death and exit itself.
        shell.Kill();
        Assert.IsTrue(shell.WaitForExit(10_000), "The CLI parent did not exit.");
    }

    public void Dispose()
    {
        var failures = new List<Exception>();
        if (awake is not null)
        {
            CollectCleanupFailure(() => StopProcess(awake, "Awake"), failures);
            CollectCleanupFailure(awake.Dispose, failures);
        }

        CollectCleanupFailure(() => StopProcess(shell, "terminal"), failures);
        CollectCleanupFailure(
            () => Assert.IsTrue(WindowControl.TryCloseWindow(terminal.WindowHandle), "The test-owned terminal window did not close."),
            failures);
        CollectCleanupFailure(shell.Dispose, failures);
        if (File.Exists(outputPath))
        {
            CollectCleanupFailure(() => context.AddResultFile(outputPath), failures);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Could not completely clean up the Awake terminal fixture.", failures);
        }
    }

    private static void StopProcess(Process process, string description)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
        }

        Assert.IsTrue(process.WaitForExit(10_000), $"The test-owned {description} process did not stop.");
    }

    private static void CollectCleanupFailure(Action action, List<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private void Send(string command)
    {
        var inputNode = AwakeUi.Nodes(terminal.Inspect(depth: 12)).First(node =>
            ((node.TryGetProperty("type", out var type) && type.GetString() == "Document") ||
             (node.TryGetProperty("className", out var className) && className.GetString() == "TermControl")) &&
            node.TryGetProperty("selector", out _));
        terminal.Find<Element>(By.Slug(inputNode.GetProperty("selector").GetString()!)).Click(msPostAction: 0);
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(terminal.WindowHandle), 15_000), $"The terminal must own foreground before CLI input. Actual: {WindowControl.GetForegroundWindowInfo()}");
        Assert.IsTrue(ClipboardHelper.SetText(command), "Could not put the CLI command on the clipboard.");
        KeyboardHelper.SendChord(Key.Ctrl, Key.V);

        // Windows Terminal pastes asynchronously. Enter must follow the visible command tail,
        // otherwise its ConPTY input can interleave with the still-running clipboard write.
        WaitForVisibleText("AWAKE_EXIT:");
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(terminal.WindowHandle), 15_000), "The terminal lost foreground before command submission.");
        KeyboardHelper.SendChord(Key.Enter);
    }
}
