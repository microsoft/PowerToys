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
            context, policy, settings, new PythonWslSettings().ContainerImage, 1000, launcher);

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
            Context(), policy, new MxcSettings(), new PythonWslSettings().ContainerImage, 1000, launcher);
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
        File.WriteAllText(Path.Combine(_root, "wxc-wslc-daemon.exe"), "test daemon");
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
            var phase = TestPhase(config);
            if (phase == "start")
            {
                return new ProcessRunResult(0, "{\"result\":{}}", string.Empty);
            }

            if (phase == "provision")
            {
                var filesystem = config.RootElement.GetProperty("filesystem");
                CollectionAssert.AreEquivalent(
                    new[] { scriptDirectory, Path.Combine(runDirectory, "requests") },
                    filesystem.GetProperty("readonlyPaths").EnumerateArray().Select(path => path.GetString()!).ToArray());
                Assert.AreEqual("block", config.RootElement.GetProperty("network").GetProperty("defaultPolicy").GetString());
                Assert.AreEqual("registry.example:5000/team/python:3.13-slim",
                    config.RootElement.GetProperty("experimental").GetProperty("wslc").GetProperty("provision").GetProperty("image").GetString());
                return new ProcessRunResult(0, "{\"result\":{\"sandboxId\":\"wslc:00000000000000000000000000000001\"}}", string.Empty);
            }

            StringAssert.StartsWith(config.RootElement.GetProperty("process").GetProperty("commandLine").GetString()!, "cd '");
            Assert.IsFalse(config.RootElement.TryGetProperty("ui", out _));
            Assert.IsFalse(config.RootElement.TryGetProperty("filesystem", out _));
            Assert.IsFalse(start.RedirectStandardInput);
            var request = Directory.GetFiles(Path.Combine(runDirectory, "requests"), "input.json", SearchOption.AllDirectories).Single();
            Assert.AreEqual("{\"text\":\"input\"}", File.ReadAllText(request));
            var workspace = Directory.GetDirectories(Path.Combine(runDirectory, "workspaces"), "workspace", SearchOption.AllDirectories).Single();
            var output = Path.Combine(workspace, "output.txt");
            File.WriteAllText(output, "produced");
            return new ProcessRunResult(0, JsonSerializer.Serialize(new { files = new[] { MxcLinuxProcessRunner.ToContainerPath(output) } }), string.Empty);
        });

        var result = MxcLinuxProcessRunner.Run(
            script, "{\"text\":\"input\"}", PowerScriptDataFormat.Files, context, policy,
            new MxcSettings { ExecutorPath = executor }, "registry.example:5000/team/python:3.13-slim", 1000, launcher,
            runDirectory);

        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.AreEqual(3, launcher.Calls);
        var schema = TestTool.Parse(TestTool.Descriptor(input: "files", output: "files"), script).OutputSchema;
        var validated = PythonTransformResult.Parse(result.StdOut, PowerScriptDataFormat.Files, schema);
        using var outputJson = JsonDocument.Parse(result.StdOut);
        var path = outputJson.RootElement.GetProperty("files")[0].GetString()!;
        CollectionAssert.AreEqual(new[] { path }, validated.FilePaths!);
        Assert.AreEqual("produced", File.ReadAllText(path));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(Path.Combine(runDirectory, "requests")).Any());
        Assert.IsFalse(File.Exists(Path.Combine(runDirectory, "phase.json")));
    }

    [TestMethod]
    [DataRow("registry.example:5000/team/python:3.13-slim")]
    [DataRow("registry.example/team/python@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void Setup_And_Probe_Use_The_Configured_Image_Without_Changing_It(string image)
    {
        var executor = Path.Combine(_root, "wxc-exec.exe");
        File.WriteAllText(executor, "test executor");
        File.WriteAllText(Path.Combine(_root, "wslcsdk.dll"), "test SDK");
        File.WriteAllText(Path.Combine(_root, "wxc-wslc-daemon.exe"), "test daemon");
        var settings = new MxcSettings { ExecutorPath = executor };
        var setup = new RecordingLauncher(start =>
        {
            CollectionAssert.AreEqual(
                new[] { "--setup-wslc", "--image", image, "--storage-path", MxcLinuxProcessRunner.ImageStorePath },
                start.ArgumentList.ToArray());
            return new ProcessRunResult(0, string.Empty, string.Empty);
        });
        Assert.AreEqual(0, MxcLinuxProcessRunner.Setup(image, settings, setup, Path.Combine(_root, "session")).ExitCode);
        Assert.AreEqual(1, setup.Calls);

        var probe = new RecordingLauncher(start =>
        {
            Assert.AreEqual("--experimental", start.ArgumentList[0]);
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            Assert.AreEqual(image, config.RootElement.GetProperty("experimental").GetProperty("wslc").GetProperty("image").GetString());
            return new ProcessRunResult(0, "Python available", string.Empty);
        });
        var runDirectory = Path.Combine(_root, "probe");
        Assert.AreEqual(0, MxcLinuxProcessRunner.Probe(image, settings, probe,
            () => Directory.CreateDirectory(runDirectory).FullName, Path.Combine(_root, "session")).ExitCode);
        Assert.AreEqual(1, probe.Calls);
        Assert.IsFalse(Directory.Exists(runDirectory));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("--help")]
    [DataRow("python:3.13 alpine")]
    [DataRow("python:3.13\nalpine")]
    public void Invalid_Image_Is_Refused_By_Setup_Probe_And_Execution_Without_Fallback(string image)
    {
        var launcher = new RecordingLauncher(_ => throw new AssertFailedException("Invalid images must not execute."));
        var settings = new MxcSettings
        {
            Enabled = true,
            RiskAccepted = true,
            DisabledPolicies = new() { MxcPolicies.Ui, MxcPolicies.LeastPrivilege },
        };
        var results = new[]
        {
            MxcLinuxProcessRunner.Setup(image, settings, launcher),
            MxcLinuxProcessRunner.Probe(image, settings, launcher),
            MxcLinuxProcessRunner.Run("missing.py", "{}", PowerScriptDataFormat.Text, Context(),
                MxcPolicyResolver.Resolve(settings), settings, image, 1000, launcher),
        };
        foreach (var result in results)
        {
            Assert.AreEqual(2, result.ExitCode);
            StringAssert.Contains(result.StdErr, "image reference");
        }

        Assert.AreEqual(0, launcher.Calls);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("""{"python":{"mode":"wsl","wsl":{"distribution":"Ubuntu"}}}""")]
    [DataRow("""{"python":{"mode":"managedLinux"}}""")]
    [DoNotParallelize]
    public void Missing_Image_Requires_User_Configuration_Instead_Of_Selecting_A_Default(string configuration)
    {
        var previous = SettingsStore.Current;
        try
        {
            SettingsStore.Current = new FileSettingsStore(_root);
            SettingsStore.Current.WriteBlob(PowerScriptsPaths.ConfigFileName, configuration);
            var settings = PythonSettings.Load();
            var launcher = new RecordingLauncher(_ => throw new AssertFailedException("No image was supplied."));

            var result = MxcLinuxProcessRunner.Setup(settings.Wsl.ContainerImage, launcher: launcher);

            Assert.AreEqual(2, result.ExitCode);
            StringAssert.Contains(result.StdErr, "image reference");
            Assert.AreEqual(0, launcher.Calls);
            settings.Save();
            Assert.AreEqual(string.Empty, PythonSettings.Load().Wsl.ContainerImage);
        }
        finally
        {
            SettingsStore.Current = previous;
        }
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
    [DataRow(true)]
    [DataRow(false)]
    public void Linux_Mode_Uses_Wslc_Without_Falling_Back_To_The_User_Distribution(bool enabled)
    {
        var script = Path.Combine(_root, "script.py");
        File.WriteAllText(script, "raise RuntimeError('Must not run outside WSLC')");
        var manifest = TestTool.Parse(TestTool.Descriptor(), script);
        var settings = new MxcSettings { Enabled = enabled, RiskAccepted = false };
        var result = new PythonRuntime(
            new PythonSettings { Mode = PythonRuntimeMode.Wsl, Wsl = new() { Distribution = "must-not-use-this-distribution" } },
            settings).Run(manifest, new PythonTransformInput());

        Assert.AreEqual(ProcessRunner.UnsupportedIsolationExitCode, result.ExitCode);
        Assert.AreEqual(MxcLinuxProcessRunner.GetPolicyError(MxcPolicyResolver.Resolve(settings)), result.StdErr);
        StringAssert.Contains(result.StdErr, "managed Linux image");
    }

    [TestMethod]
    [DataRow("wsl")]
    [DataRow("managedLinux")]
    [DataRow("ManagedLinux")]
    [DoNotParallelize]
    public void Linux_Mode_Migrates_Without_Changing_Existing_Environments_Or_Isolation(string mode)
    {
        var previous = SettingsStore.Current;
        try
        {
            SettingsStore.Current = new FileSettingsStore(_root);
            SettingsStore.Current.WriteBlob(PowerScriptsPaths.ConfigFileName, JsonSerializer.Serialize(new
            {
                mxc = new { enabled = true },
                scriptsRoot = @"C:\scripts",
                python = new
                {
                    mode,
                    windows = new { interpreterPath = @"C:\Python\python.exe" },
                    wsl = new { distribution = "Ubuntu", containerImage = "python:3.13-slim" },
                    timeoutSeconds = 47,
                },
            }));
            var loaded = PythonSettings.Load();
            Assert.AreEqual(PythonRuntimeMode.Wsl, loaded.Mode);
            Assert.AreEqual("Ubuntu", loaded.Wsl.Distribution);
            Assert.AreEqual("python:3.13-slim", loaded.Wsl.ContainerImage);
            Assert.AreEqual(@"C:\Python\python.exe", loaded.Windows.InterpreterPath);
            Assert.AreEqual(47, loaded.TimeoutSeconds);
            loaded.Save();
            using var config = JsonDocument.Parse(SettingsStore.Current.ReadBlob(PowerScriptsPaths.ConfigFileName)!);
            Assert.AreEqual("Wsl", config.RootElement.GetProperty("python").GetProperty("mode").GetString());
            Assert.IsTrue(config.RootElement.GetProperty("mxc").GetProperty("enabled").GetBoolean());
            Assert.AreEqual(@"C:\scripts", config.RootElement.GetProperty("scriptsRoot").GetString());
            Assert.AreEqual("python:3.13-slim",
                config.RootElement.GetProperty("python").GetProperty("wsl").GetProperty("containerImage").GetString());
        }
        finally
        {
            SettingsStore.Current = previous;
        }
    }

    [TestMethod]
    public void Different_Scripts_Reuse_One_Container_And_Only_Project_Staged_Selected_Files()
    {
        var scripts = Directory.CreateDirectory(Path.Combine(_root, "scripts")).FullName;
        var first = Path.Combine(scripts, "first.py");
        var second = Path.Combine(scripts, "second.py");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        var input = Path.Combine(_root, "selected.txt");
        File.WriteAllText(input, "selected input");
        var settings = RuntimeSettings();
        var provisionCount = 0;
        var execCount = 0;
        var session = Path.Combine(_root, "session");
        var launcher = new RecordingLauncher(start =>
        {
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            var phase = TestPhase(config);
            if (phase == "provision")
            {
                provisionCount++;
                var mounts = config.RootElement.GetProperty("filesystem").GetProperty("readonlyPaths");
                CollectionAssert.AreEquivalent(new[] { scripts, Path.Combine(session, "requests") }, mounts.EnumerateArray().Select(value => value.GetString()!).ToArray());
            }

            if (phase == "exec")
            {
                execCount++;
                var requestPath = Directory.GetFiles(Path.Combine(session, "requests"), "input.json", SearchOption.AllDirectories).Single();
                using var request = JsonDocument.Parse(File.ReadAllText(requestPath));
                var staged = request.RootElement.GetProperty("files")[0].GetString()!;
                Assert.AreNotEqual(MxcLinuxProcessRunner.ToContainerPath(input), staged);
                var stagedPath = PythonRuntime.FromWslPath(staged)!;
                Assert.AreEqual("selected input", File.ReadAllText(stagedPath));
                Assert.AreEqual(staged, request.RootElement.GetProperty("parameters").GetProperty("reference").GetString());
                Assert.AreEqual(MxcLinuxProcessRunner.ToContainerPath(input), request.RootElement.GetProperty("parameters").GetProperty("literal").GetString());
                StringAssert.Contains(config.RootElement.GetProperty("process").GetProperty("commandLine").GetString()!,
                    MxcLinuxProcessRunner.ToContainerPath(execCount == 1 ? first : second));
            }

            return PhaseResult(phase);
        });
        var context = new ProcessExecutionContext
        {
            ScriptsRoot = scripts, ScriptDirectory = scripts, InputPaths = new[] { input }, FileParameterNames = new[] { "reference" },
        };
        var payload = JsonSerializer.Serialize(new
        {
            files = new[] { MxcLinuxProcessRunner.ToContainerPath(input) },
            parameters = new { reference = MxcLinuxProcessRunner.ToContainerPath(input), literal = MxcLinuxProcessRunner.ToContainerPath(input) },
        });
        foreach (var script in new[] { first, second })
        {
            var result = MxcLinuxProcessRunner.Run(script, payload, PowerScriptDataFormat.Text, context,
                MxcPolicyResolver.Resolve(settings), settings, "python:test", 1000, launcher, session);
            Assert.AreEqual(0, result.ExitCode, result.StdErr);
        }

        Assert.AreEqual(1, provisionCount);
        Assert.AreEqual(2, execCount);
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(Path.Combine(session, "requests")).Any());
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(Path.Combine(session, "workspaces")).Any());
        Assert.AreEqual("selected input", File.ReadAllText(input));
    }

    [TestMethod]
    [DataRow("image")]
    [DataRow("network")]
    [DataRow("runtime")]
    public void Image_Policy_And_Runtime_Changes_Replace_The_Shared_Container(string change)
    {
        var settings = RuntimeSettings();
        var phases = new List<string>();
        var launcher = new RecordingLauncher(start =>
        {
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            var phase = TestPhase(config);
            phases.Add(phase);
            return PhaseResult(phase);
        });
        Assert.AreEqual(0, RunText(settings, launcher).ExitCode);
        if (change == "network")
        {
            settings.DisabledPolicies.Add(MxcPolicies.Network);
        }
        else if (change == "runtime")
        {
            File.AppendAllText(settings.ExecutorPath!, "updated");
        }

        var result = RunText(settings, launcher, image: change == "image" ? "python:changed" : "python:test");
        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        CollectionAssert.AreEqual(new[] { "provision", "start", "exec", "stop", "deprovision", "provision", "start", "exec" }, phases);
    }

    [TestMethod]
    [DataRow("not_provisioned")]
    [DataRow("not_started")]
    [DataRow("backend_error")]
    public void Expired_Or_Unhealthy_Container_Is_Recovered_Before_User_Code_Runs(string error)
    {
        var settings = RuntimeSettings();
        var phases = new List<string>();
        var expired = false;
        var launcher = new RecordingLauncher(start =>
        {
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            var phase = TestPhase(config);
            phases.Add(phase);
            if (expired && phase == "check")
            {
                expired = false;
                return new ProcessRunResult(1, JsonSerializer.Serialize(new { error = new { code = error } }), string.Empty);
            }

            return PhaseResult(phase);
        });
        Assert.AreEqual(0, RunText(settings, launcher).ExitCode);
        expired = true;
        var result = RunText(settings, launcher);
        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        CollectionAssert.AreEqual(error == "not_provisioned"
            ? new[] { "provision", "start", "exec", "check", "provision", "start", "exec" }
            : new[] { "provision", "start", "exec", "check", "stop", "deprovision", "provision", "start", "exec" }, phases);
    }

    [TestMethod]
    [DataRow(17)]
    [DataRow(124)]
    public void Failed_Or_Timed_Out_User_Code_Is_Not_Retried_And_Invalidates_The_Container(int exitCode)
    {
        var settings = RuntimeSettings();
        var phases = new List<string>();
        var launcher = new RecordingLauncher(start =>
        {
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            var phase = TestPhase(config);
            phases.Add(phase);
            return phase == "exec"
                ? new ProcessRunResult(exitCode, "{\"error\":{\"code\":\"not_provisioned\"}}", "Script failed.")
                : PhaseResult(phase);
        });
        var result = RunText(settings, launcher);
        Assert.AreEqual(exitCode, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "provision", "start", "exec", "stop", "deprovision" }, phases);
        Assert.IsFalse(File.Exists(Path.Combine(_root, "session", "session.json")));
    }

    [TestMethod]
    public async Task Concurrent_Host_Requests_Are_Serialized_And_Share_The_Container()
    {
        var settings = RuntimeSettings();
        var active = 0;
        var provisions = 0;
        var executions = 0;
        var launcher = new RecordingLauncher(start =>
        {
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            var phase = TestPhase(config);
            if (phase == "provision")
            {
                Interlocked.Increment(ref provisions);
            }

            if (phase == "exec")
            {
                Assert.AreEqual(1, Interlocked.Increment(ref active));
                Thread.Sleep(100);
                Interlocked.Increment(ref executions);
                Interlocked.Decrement(ref active);
            }

            return PhaseResult(phase);
        });
        var scripts = Directory.CreateDirectory(Path.Combine(_root, "scripts")).FullName;
        File.WriteAllText(Path.Combine(scripts, "main.py"), "print('output')");
        var results = await Task.WhenAll(Task.Run(() => RunText(settings, launcher)), Task.Run(() => RunText(settings, launcher)));
        foreach (var result in results)
        {
            Assert.AreEqual(0, result.ExitCode, result.StdErr);
        }

        Assert.AreEqual(1, provisions);
        Assert.AreEqual(2, executions);
    }

    [TestMethod]
    public void Missing_Daemon_Fails_Closed()
    {
        var settings = RuntimeSettings();
        File.Delete(Path.Combine(_root, "wxc-wslc-daemon.exe"));
        var launcher = new RecordingLauncher(_ => throw new AssertFailedException("Missing runtime must not launch."));
        var result = RunText(settings, launcher);
        Assert.AreEqual(125, result.ExitCode);
        StringAssert.Contains(result.StdErr, "wxc-wslc-daemon.exe");
        Assert.AreEqual(0, launcher.Calls);
    }

    [TestMethod]
    public void Abandoned_Requests_And_Expired_Outputs_Are_Pruned_Without_Removing_Recent_Outputs()
    {
        var settings = RuntimeSettings();
        var session = Path.Combine(_root, "session");
        var request = Directory.CreateDirectory(Path.Combine(session, "requests", "mxc-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(request, "input.json"), "abandoned");
        var old = Directory.CreateDirectory(Path.Combine(session, "workspaces", "mxc-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(old, "old.txt"), "expired");
        Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-2));
        var recent = Directory.CreateDirectory(Path.Combine(session, "workspaces", "mxc-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(recent, "recent.txt"), "retained");
        var launcher = new RecordingLauncher(start =>
        {
            using var config = JsonDocument.Parse(File.ReadAllText(start.ArgumentList[1]));
            return PhaseResult(TestPhase(config));
        });

        var result = RunText(settings, launcher);
        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.IsFalse(Directory.Exists(request));
        Assert.IsFalse(Directory.Exists(old));
        Assert.AreEqual("retained", File.ReadAllText(Path.Combine(recent, "recent.txt")));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Image_Operations_Wait_For_The_Released_Daemon_To_Unlock_The_Cache(bool setup)
    {
        var settings = RuntimeSettings();
        var attempts = 0;
        var launcher = new RecordingLauncher(_ => ++attempts == 1
            ? new ProcessRunResult(1, string.Empty, "WslcListSessionImages failed: HRESULT 0x80070020")
            : new ProcessRunResult(0, "Python available", string.Empty));
        var result = setup
            ? MxcLinuxProcessRunner.Setup("python:test", settings, launcher, Path.Combine(_root, "session"))
            : MxcLinuxProcessRunner.Probe("python:test", settings, launcher,
                () => Directory.CreateDirectory(Path.Combine(_root, "probe")).FullName, Path.Combine(_root, "session"));
        Assert.AreEqual(0, result.ExitCode, result.StdErr);
        Assert.AreEqual(2, attempts);
    }

    private MxcSettings RuntimeSettings()
    {
        foreach (var name in new[] { "wxc-exec.exe", "wxc-wslc-daemon.exe", "wslcsdk.dll" })
        {
            File.WriteAllText(Path.Combine(_root, name), "test runtime");
        }

        return new MxcSettings
        {
            ExecutorPath = Path.Combine(_root, "wxc-exec.exe"),
            RiskAccepted = true,
            DisabledPolicies = new() { MxcPolicies.Ui, MxcPolicies.LeastPrivilege },
        };
    }

    private ProcessRunResult RunText(MxcSettings settings, IProcessLauncher launcher, string image = "python:test")
    {
        var scripts = Directory.CreateDirectory(Path.Combine(_root, "scripts")).FullName;
        var script = Path.Combine(scripts, "main.py");
        if (!File.Exists(script))
        {
            File.WriteAllText(script, "print('output')");
        }

        return MxcLinuxProcessRunner.Run(script, "{\"text\":\"input\"}", PowerScriptDataFormat.Text, Context(scripts),
            MxcPolicyResolver.Resolve(settings), settings, image, 1000, launcher, Path.Combine(_root, "session"));
    }

    private static ProcessRunResult PhaseResult(string? phase) => new(0, phase switch
    {
        "provision" => "{\"result\":{\"sandboxId\":\"wslc:00000000000000000000000000000001\"}}",
        "exec" => "output",
        _ => "{\"result\":{}}",
    }, string.Empty);

    private static string TestPhase(JsonDocument config) =>
        config.RootElement.GetProperty("phase").GetString() == "exec" &&
        config.RootElement.GetProperty("process").GetProperty("commandLine").GetString() == ":"
            ? "check"
            : config.RootElement.GetProperty("phase").GetString()!;

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
