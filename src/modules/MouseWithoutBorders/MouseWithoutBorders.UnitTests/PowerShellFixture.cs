// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.MouseWithoutBorders.UITests;

namespace MouseWithoutBorders.UnitTests;

internal sealed class PowerShellFixture
{
    private readonly string readyPath;
    private readonly string releasePath;

    public PowerShellFixture(string root)
    {
        ScriptRoot = Path.Combine(root, "powershell-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ScriptRoot);
        readyPath = Path.Combine(ScriptRoot, "initialized");
        releasePath = Path.Combine(ScriptRoot, "run");
    }

    public string ScriptRoot { get; }

    public static WinAppSandboxCommand StartCommand(string script, string root, string? targetStateRoot = null, Guid? workflowId = null, string? initialize = null)
    {
        var fixture = new PowerShellFixture(root);
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
        var command = WinAppSandboxCommand.Start(
            executable,
            ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", fixture.Wrap(script, initialize)],
            targetStateRoot ?? root,
            root,
            workflowId);
        try
        {
            fixture.WaitForStartup(() =>
            {
                if (command.HasExited)
                {
                    throw command.Failure("fixture_exited_before_initialization");
                }
            });
            return command;
        }
        catch (Exception startupError)
        {
            try
            {
                command.Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("PowerShell fixture initialization and cleanup failed.", startupError, cleanupError);
            }

            throw;
        }
    }

    public string Wrap(string script, string? initialize = null) => $$"""
        $ErrorActionPreference='Stop'
        $null=Get-Process -Id $PID
        $null=@{}|ConvertTo-Json -Compress
        {{initialize}}
        [IO.File]::WriteAllText('{{Literal(readyPath)}}','')
        $startupGate=[Diagnostics.Stopwatch]::StartNew()
        while(-not [IO.File]::Exists('{{Literal(releasePath)}}')){
            if($startupGate.Elapsed.TotalMinutes -ge 2){throw 'Fixture was not released after initialization.'}
            Start-Sleep -Milliseconds 20
        }
        {{script}}
        """;

    public void WaitForStartup(Action requireLiveProcess)
    {
        // Shell and module cold start is not part of the command/lease deadline under test.
        var timer = System.Diagnostics.Stopwatch.StartNew();
        RunFiles.Wait(
            () => File.Exists(readyPath),
            TimeSpan.FromMinutes(2),
            "The Windows PowerShell fixture did not initialize.",
            requireLiveProcess);
        Console.WriteLine($"Windows PowerShell fixture initialized after {timer.ElapsedMilliseconds} ms.");
        File.WriteAllText(releasePath, string.Empty);
    }

    private static string Literal(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
