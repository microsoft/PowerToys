// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Execution;
using PowerScripts.Core.Security;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Tests;

[TestClass]
public class MxcLinuxTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "powerscripts-linux-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void Unsupported_Or_Unaccepted_Policies_Never_Launch(bool enabled, bool acceptExceptions)
    {
        var context = Context();
        var settings = new MxcSettings
        {
            Enabled = enabled,
            RiskAccepted = acceptExceptions,
            DisabledPolicies = new() { MxcPolicies.Ui, MxcPolicies.LeastPrivilege },
        };
        var policy = MxcPolicyResolver.Resolve(settings);
        var launcher = new RecordingLauncher(_ => throw new AssertFailedException("Unsupported execution must never start."));

        var result = MxcLinuxProcessRunner.Run(
            "missing-script.py", "{}", PowerScriptDataFormat.Text,
            context, policy, settings, 1000, launcher);

        Assert.AreEqual(125, result.ExitCode);
        Assert.AreEqual(0, launcher.Calls);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StdErr));
    }

    [TestMethod]
    public void AcceptedGlobalExceptionsAreSufficientWithoutAnyScriptSettings()
    {
        var settings = new MxcSettings
        {
            Enabled = true,
            RiskAccepted = true,
            DisabledPolicies = new() { MxcPolicies.Ui, MxcPolicies.LeastPrivilege },
        };
        var policy = MxcPolicyResolver.Resolve(settings);
        Assert.IsNull(MxcLinuxProcessRunner.GetPolicyError(policy));
    }

    [TestMethod]
    [DataRow(MxcPolicies.Filesystem)]
    [DataRow(MxcPolicies.Ui)]
    [DataRow(MxcPolicies.LeastPrivilege)]
    public void Unsupported_Effective_Control_Is_Refused_Before_Executor_Starts(string control)
    {
        var enabled = control == MxcPolicies.Filesystem
            ? new[] { MxcPolicies.Network }
            : new[] { MxcPolicies.Filesystem, MxcPolicies.Network, control };
        var policy = new MxcEffectivePolicy { Enabled = true, EnabledPolicies = enabled };
        var launcher = new RecordingLauncher(_ => throw new AssertFailedException("Unsupported policy must not execute."));
        var result = MxcLinuxProcessRunner.Run(
            "missing.py", "{}", PowerScriptDataFormat.Text,
            Context(), policy, new MxcSettings(), 1000, launcher);
        Assert.AreEqual(125, result.ExitCode);
        Assert.AreEqual(0, launcher.Calls);
    }

    [TestMethod]
    public void Successful_File_Output_Is_Mapped_And_Only_The_Workspace_Is_Retained()
    {
        var scriptDirectory = Path.Combine(_root, "script's folder");
        Directory.CreateDirectory(scriptDirectory);
        var script = Path.Combine(scriptDirectory, "main.py");
        var selected = Path.Combine(_root, "selected input.txt");
        File.WriteAllText(script, "import json\nprint(json.dumps({'files': []}))");
        File.WriteAllText(selected, "selected");
        var executor = Path.Combine(_root, "wxc-exec.exe");
        File.WriteAllText(executor, "test executor");
        File.WriteAllText(Path.Combine(_root, "wslcsdk.dll"), "test SDK");
        var runDirectory = Path.Combine(_root, "run");
        var context = Context(scriptDirectory, selected);
        var policy = MxcPolicyResolver.Resolve(new MxcSettings
        {
            RiskAccepted = true,
            DisabledPolicies = new() { MxcPolicies.Ui, MxcPolicies.LeastPrivilege },
        });
        var launcher = new RecordingLauncher(start =>
        {
            Assert.AreEqual("--experimental", start.ArgumentList[0]);
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            var filesystem = config.RootElement.GetProperty("filesystem");
            CollectionAssert.AreEquivalent(
                new[] { scriptDirectory, Path.Combine(runDirectory, "request"), selected },
                filesystem.GetProperty("readonlyPaths").EnumerateArray().Select(path => path.GetString()!).ToArray());
            Assert.AreEqual("block", config.RootElement.GetProperty("network").GetProperty("defaultPolicy").GetString());
            StringAssert.StartsWith(config.RootElement.GetProperty("process").GetProperty("commandLine").GetString()!, "cd '");
            Assert.IsFalse(config.RootElement.TryGetProperty("ui", out _));
            Assert.IsFalse(start.RedirectStandardInput);
            Assert.AreEqual("{\"text\":\"input\"}", File.ReadAllText(Path.Combine(runDirectory, "request", "input.json")));
            var output = Path.Combine(runDirectory, "workspace", "output.txt");
            File.WriteAllText(output, "produced");
            return new ProcessRunResult(0, JsonSerializer.Serialize(new { files = new[] { MxcLinuxProcessRunner.ToContainerPath(output) } }), string.Empty);
        });

        var result = MxcLinuxProcessRunner.Run(
            script, "{\"text\":\"input\"}", PowerScriptDataFormat.Files, context, policy,
            new MxcSettings { ExecutorPath = executor }, 1000, launcher,
            () => Directory.CreateDirectory(runDirectory).FullName);

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.AreEqual(1, launcher.Calls);
        using var outputJson = JsonDocument.Parse(result.StdOut);
        var path = outputJson.RootElement.GetProperty("files")[0].GetString()!;
        Assert.AreEqual("produced", File.ReadAllText(path));
        Assert.IsFalse(Directory.Exists(Path.Combine(runDirectory, "request")));
        Assert.IsFalse(File.Exists(Path.Combine(runDirectory, "mxc-config.json")));
    }

    [TestMethod]
    [DataRow("/tmp/output.txt")]
    [DataRow("/mnt/c/Windows/win.ini")]
    public void Output_Outside_The_Projected_Workspace_Is_Refused(string path)
    {
        Assert.ThrowsException<InvalidDataException>(() =>
            MxcLinuxProcessRunner.MapOutputPath(path, Path.Combine(_root, "workspace")));
    }

    [TestMethod]
    public void Output_Path_Traversal_Is_Refused()
    {
        var workspace = Path.Combine(_root, "workspace");
        Assert.ThrowsException<InvalidDataException>(() =>
            MxcLinuxProcessRunner.MapOutputPath(MxcLinuxProcessRunner.ToContainerPath(workspace) + "/../secret.txt", workspace));
    }

    [TestMethod]
    public void Unc_Input_Is_Refused_Instead_Of_Being_Projected_Incorrectly()
    {
        Assert.ThrowsException<NotSupportedException>(() => MxcLinuxProcessRunner.ToContainerPath(@"\\server\share\input.txt"));
    }

    [TestMethod]
    [DoNotParallelize]
    public void Managed_Mode_RoundTrips_Without_Changing_Existing_Environments_Or_Isolation()
    {
        var previous = SettingsStore.Current;
        try
        {
            SettingsStore.Current = new FileSettingsStore(_root);
            SettingsStore.Current.WriteBlob(PowerScriptsPaths.ConfigFileName, """{"mxc":{"enabled":true},"scriptsRoot":"C:\\scripts"}""");
            new PythonSettings
            {
                Mode = PythonRuntimeMode.ManagedLinux,
                Windows = new() { InterpreterPath = @"C:\Python\python.exe" },
                Wsl = new() { Distribution = "Ubuntu" },
                TimeoutSeconds = 47,
            }.Save();
            var loaded = PythonSettings.Load();
            Assert.AreEqual(PythonRuntimeMode.ManagedLinux, loaded.Mode);
            Assert.AreEqual("Ubuntu", loaded.Wsl.Distribution);
            Assert.AreEqual(@"C:\Python\python.exe", loaded.Windows.InterpreterPath);
            Assert.AreEqual(47, loaded.TimeoutSeconds);
            using var config = JsonDocument.Parse(SettingsStore.Current.ReadBlob(PowerScriptsPaths.ConfigFileName)!);
            Assert.IsTrue(config.RootElement.GetProperty("mxc").GetProperty("enabled").GetBoolean());
            Assert.AreEqual(@"C:\scripts", config.RootElement.GetProperty("scriptsRoot").GetString());
        }
        finally
        {
            SettingsStore.Current = previous;
        }
    }

    private static ProcessExecutionContext Context(string directory = "", params string[] inputs) => new()
    {
        ScriptId = "demo",
        ScriptDirectory = directory,
        ScriptsRoot = directory,
        InputPaths = inputs,
    };

    private sealed class RecordingLauncher(Func<ProcessStartInfo, ProcessRunResult> execute) : IProcessLauncher
    {
        public int Calls { get; private set; }

        public ProcessRunResult Run(ProcessStartInfo startInfo, string? standardInput, int timeoutMs)
        {
            Calls++;
            return execute(startInfo);
        }
    }
}
