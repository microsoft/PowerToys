// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Tests;

[TestClass]
public class MxcTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "powerscripts-mxc-" + Guid.NewGuid().ToString("N"));
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
    public void Settings_Defaults_Enable_All_Restrictions()
    {
        var settings = MxcSettings.Load(new FileSettingsStore(_root), _ => SupportedPlatform());
        Assert.IsTrue(settings.Enabled);
        Assert.IsTrue(settings.UsesPlatformDefault);
        Assert.IsFalse(settings.RiskAccepted);
        Assert.AreEqual(string.Empty, settings.ExecutorPath);
        Assert.AreEqual(0, settings.DisabledPolicies.Count);

        var effective = MxcPolicyResolver.Resolve(settings, "demo");
        Assert.IsTrue(effective.Enabled);
        CollectionAssert.AreEquivalent(MxcPolicies.All.ToArray(), effective.EnabledPolicies.ToArray());
    }

    [TestMethod]
    public void Settings_RoundTrip_Preserves_Other_Config_Keys()
    {
        var store = new FileSettingsStore(_root);
        store.WriteBlob(PowerScriptsPaths.ConfigFileName, """{"scriptsRoot":"C:\\scripts","enabled":true}""");

        var settings = new MxcSettings
        {
            ExecutorPath = @"C:\tools\wxc-exec.exe",
            RiskAccepted = true,
            DisabledPolicies = new List<string> { MxcPolicies.Network },
            Scripts = new Dictionary<string, MxcScriptOverride>(StringComparer.OrdinalIgnoreCase)
            {
                ["stable-id"] = new()
                {
                    Enabled = false,
                    EnabledPolicies = new List<string> { MxcPolicies.Filesystem },
                    RiskAccepted = true,
                },
            },
        };
        settings.Save(store);

        var loaded = MxcSettings.Load(store);
        Assert.AreEqual(settings.ExecutorPath, loaded.ExecutorPath);
        CollectionAssert.AreEqual(new[] { MxcPolicies.Network }, loaded.DisabledPolicies);
        Assert.AreEqual(false, loaded.Scripts["stable-id"].Enabled);
        CollectionAssert.AreEqual(
            new[] { MxcPolicies.Filesystem },
            loaded.Scripts["stable-id"].EnabledPolicies);
        Assert.IsTrue(loaded.Scripts["stable-id"].RiskAccepted);

        using var document = JsonDocument.Parse(store.ReadBlob(PowerScriptsPaths.ConfigFileName)!);
        Assert.AreEqual(@"C:\scripts", document.RootElement.GetProperty("scriptsRoot").GetString());
        Assert.IsTrue(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.IsTrue(document.RootElement.TryGetProperty("mxc", out _));
    }

    [TestMethod]
    public void Corrupt_Settings_Fall_Back_To_Fully_Restricted()
    {
        var store = new FileSettingsStore(_root);
        store.WriteBlob(PowerScriptsPaths.ConfigFileName, """{"mxc":{"enabled":"no","riskAccepted":true,"disabledPolicies":["filesystem"]}}""");

        var settings = MxcSettings.Load(store, _ => SupportedPlatform());
        var effective = MxcPolicyResolver.Resolve(settings, "demo");

        Assert.IsTrue(effective.Enabled);
        CollectionAssert.AreEquivalent(MxcPolicies.All.ToArray(), effective.EnabledPolicies.ToArray());
    }

    [TestMethod]
    public void Unsupported_Platform_Defaults_Mxc_Off_Without_Risk_Acceptance()
    {
        var settings = MxcSettings.Load(
            new FileSettingsStore(_root),
            _ => new MxcPlatformSupportResult
            {
                IsSupported = false,
                Reason = "unsupported test host",
            });

        Assert.IsFalse(settings.Enabled);
        Assert.IsTrue(settings.UsesPlatformDefault);
        Assert.AreEqual("unsupported test host", settings.PlatformSupport?.Reason);
        Assert.IsFalse(MxcPolicyResolver.Resolve(settings, "demo").Enabled);
    }

    [TestMethod]
    public void Saving_Platform_Default_Does_Not_Create_Explicit_Preference()
    {
        var store = new FileSettingsStore(_root);
        var unsupported = new MxcPlatformSupportResult
        {
            IsSupported = false,
            Reason = "unsupported test host",
        };
        var settings = MxcSettings.Load(store, _ => unsupported);

        settings.ExecutorPath = @"C:\tools\wxc-exec.exe";
        settings.Save(store);

        using var document = JsonDocument.Parse(store.ReadBlob(PowerScriptsPaths.ConfigFileName)!);
        Assert.IsFalse(document.RootElement.GetProperty("mxc").TryGetProperty("enabled", out _));

        var reloaded = MxcSettings.Load(store, _ => unsupported);
        Assert.IsFalse(reloaded.Enabled);
        Assert.IsTrue(reloaded.UsesPlatformDefault);
        Assert.IsFalse(MxcPolicyResolver.Resolve(reloaded, "demo").Enabled);
    }

    [TestMethod]
    public void Explicit_Mxc_Preference_Is_Preserved_On_Unsupported_Platform()
    {
        var store = new FileSettingsStore(_root);
        store.WriteBlob(PowerScriptsPaths.ConfigFileName, """{"mxc":{"enabled":true}}""");
        var probeCalled = false;

        var settings = MxcSettings.Load(
            store,
            _ =>
            {
                probeCalled = true;
                return new MxcPlatformSupportResult { IsSupported = false };
            });

        Assert.IsTrue(settings.Enabled);
        Assert.IsFalse(settings.UsesPlatformDefault);
        Assert.IsFalse(probeCalled);
    }

    [TestMethod]
    public void Platform_Probe_Requires_Supported_Build_And_Valid_Tier()
    {
        var belowFloor = MxcPlatformSupport.EvaluateProbe(
            MxcPlatformSupport.MinimumWindowsBuild - 1,
            @"C:\wxc-exec.exe",
            0,
            """{"tier":"appcontainer-dacl","warnings":[]}""",
            string.Empty);
        Assert.IsFalse(belowFloor.IsSupported);

        var supported = MxcPlatformSupport.EvaluateProbe(
            MxcPlatformSupport.MinimumWindowsBuild,
            @"C:\wxc-exec.exe",
            0,
            """{"tier":"appcontainer-dacl","warnings":[]}""",
            string.Empty);
        Assert.IsTrue(supported.IsSupported);
        Assert.AreEqual("appcontainer-dacl", supported.Tier);

        var invalid = MxcPlatformSupport.EvaluateProbe(
            MxcPlatformSupport.MinimumWindowsBuild,
            @"C:\wxc-exec.exe",
            0,
            "not json",
            string.Empty);
        Assert.IsFalse(invalid.IsSupported);
    }

    [TestMethod]
    public void Wsl_Target_Uses_Direct_Execution_Only_For_Platform_Default()
    {
        var platformDefault = MxcSettings.Load(
            new FileSettingsStore(_root),
            _ => SupportedPlatform());
        var defaultPolicy = MxcPolicyResolver.Resolve(platformDefault, "demo");
        Assert.IsTrue(defaultPolicy.Enabled);
        Assert.IsTrue(defaultPolicy.UsesPlatformDefault);
        Assert.IsTrue(ProcessRunner.IsWslTarget(new ProcessStartInfo { FileName = "wsl.exe" }));

        var explicitPolicy = MxcPolicyResolver.Resolve(new MxcSettings { Enabled = true }, "demo");
        Assert.IsFalse(explicitPolicy.UsesPlatformDefault);
    }

    [TestMethod]
    public void Weakening_Requires_Risk_Acceptance_At_Each_Scope()
    {
        var globalRejected = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                DisabledPolicies = new List<string> { MxcPolicies.Network },
            },
            "demo");
        CollectionAssert.AreEquivalent(MxcPolicies.All.ToArray(), globalRejected.EnabledPolicies.ToArray());

        var disableRejected = MxcPolicyResolver.Resolve(new MxcSettings { Enabled = false }, "demo");
        Assert.IsTrue(disableRejected.Enabled);
        CollectionAssert.AreEquivalent(MxcPolicies.All.ToArray(), disableRejected.EnabledPolicies.ToArray());

        var disableAccepted = MxcPolicyResolver.Resolve(
            new MxcSettings { Enabled = false, RiskAccepted = true },
            "demo");
        Assert.IsFalse(disableAccepted.Enabled);

        var globalAccepted = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                RiskAccepted = true,
                DisabledPolicies = new List<string> { MxcPolicies.Network },
            },
            "demo");
        Assert.IsFalse(globalAccepted.IsEnabled(MxcPolicies.Network));

        var perScriptRejected = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                RiskAccepted = true,
                DisabledPolicies = new List<string> { MxcPolicies.Network },
                Scripts = new Dictionary<string, MxcScriptOverride>(StringComparer.OrdinalIgnoreCase)
                {
                    ["demo"] = new() { Enabled = false },
                },
            },
            "demo");
        Assert.IsTrue(perScriptRejected.Enabled);
        CollectionAssert.AreEquivalent(MxcPolicies.All.ToArray(), perScriptRejected.EnabledPolicies.ToArray());

        var neutralOverrideInheritsAcceptedGlobalWeakening = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                Enabled = false,
                RiskAccepted = true,
                Scripts = new Dictionary<string, MxcScriptOverride>(StringComparer.OrdinalIgnoreCase)
                {
                    ["demo"] = new(),
                },
            },
            "demo");
        Assert.IsFalse(neutralOverrideInheritsAcceptedGlobalWeakening.Enabled);

        var perScriptAccepted = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                Scripts = new Dictionary<string, MxcScriptOverride>(StringComparer.OrdinalIgnoreCase)
                {
                    ["demo"] = new() { Enabled = false, RiskAccepted = true },
                },
            },
            "demo");
        Assert.IsFalse(perScriptAccepted.Enabled);

        var perPolicyAccepted = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                Scripts = new Dictionary<string, MxcScriptOverride>(StringComparer.OrdinalIgnoreCase)
                {
                    ["demo"] = new()
                    {
                        DisabledPolicies = new List<string> { MxcPolicies.Ui },
                        RiskAccepted = true,
                    },
                },
            },
            "demo");
        Assert.IsFalse(perPolicyAccepted.IsEnabled(MxcPolicies.Ui));

        var perScriptStrengthening = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                RiskAccepted = true,
                DisabledPolicies = new List<string> { MxcPolicies.Network },
                Scripts = new Dictionary<string, MxcScriptOverride>(StringComparer.OrdinalIgnoreCase)
                {
                    ["demo"] = new()
                    {
                        EnabledPolicies = new List<string> { MxcPolicies.Network },
                    },
                },
            },
            "demo");
        Assert.IsTrue(perScriptStrengthening.IsEnabled(MxcPolicies.Network));
    }

    [TestMethod]
    public void Descriptor_Parses_Display_Only_Recommendations()
    {
        File.WriteAllText(Path.Combine(_root, "demo.ps1"), "Write-Output ok");
        var descriptor = Path.Combine(_root, "demo.ps1.tool.json");
        File.WriteAllText(descriptor, """
            {
              "name": "demo",
              "description": "demo",
              "x-powerscript": {
                "mxc": {
                  "recommendedPolicies": ["filesystem", "network"]
                }
              }
            }
            """);

        var manifest = ToolDescriptorParser.TryParseFile(descriptor);
        Assert.IsNotNull(manifest);
        CollectionAssert.AreEqual(
            new[] { MxcPolicies.Filesystem, MxcPolicies.Network },
            manifest!.Mxc.RecommendedPolicies);

        var effective = MxcPolicyResolver.Resolve(new MxcSettings(), manifest.Id, manifest.Mxc.RecommendedPolicies);
        CollectionAssert.AreEquivalent(MxcPolicies.All.ToArray(), effective.EnabledPolicies.ToArray());
    }

    [TestMethod]
    public void Config_Uses_Stable_Schema_And_Readonly_Script_Root()
    {
        var scriptsRoot = Path.Combine(_root, "scripts");
        var package = Path.Combine(scriptsRoot, "demo");
        var workspace = Path.Combine(_root, "workspace");
        var input = Path.Combine(_root, "input.txt");
        Directory.CreateDirectory(package);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(input, "input");

        var target = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            WorkingDirectory = package,
        };
        target.ArgumentList.Add("/c");
        target.ArgumentList.Add(Path.Combine(package, "demo.cmd"));
        var undeclaredAbsoluteArgument = Path.Combine(_root, "not-an-input");
        Directory.CreateDirectory(undeclaredAbsoluteArgument);
        target.ArgumentList.Add(undeclaredAbsoluteArgument);
        target.Environment["SECRET_TOKEN"] = "must-not-leak";

        var context = new ProcessExecutionContext
        {
            ScriptId = "demo",
            ScriptDirectory = package,
            ScriptsRoot = scriptsRoot,
            InputPaths = new[] { input },
        };
        var policy = MxcPolicyResolver.Resolve(new MxcSettings(), "demo");
        var json = MxcProcessRunner.BuildConfigurationJson(
            target,
            target.FileName,
            context,
            policy,
            workspace,
            timeoutMs: 1234);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.AreEqual("0.8.0-alpha", root.GetProperty("version").GetString());
        Assert.AreEqual("processcontainer", root.GetProperty("containment").GetString());
        Assert.AreEqual(1234, root.GetProperty("process").GetProperty("timeout").GetInt32());

        var filesystem = root.GetProperty("filesystem");
        var readonlyPaths = filesystem.GetProperty("readonlyPaths")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        var readwritePaths = filesystem.GetProperty("readwritePaths")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        CollectionAssert.Contains(readonlyPaths, Path.GetFullPath(scriptsRoot));
        CollectionAssert.Contains(readonlyPaths, Path.GetFullPath(package));
        CollectionAssert.Contains(readonlyPaths, Path.GetFullPath(input));
        CollectionAssert.Contains(readonlyPaths, Path.GetPathRoot(Path.GetFullPath(scriptsRoot))!);
        CollectionAssert.DoesNotContain(readonlyPaths, Path.GetFullPath(undeclaredAbsoluteArgument));
        CollectionAssert.AreEqual(new[] { Path.GetFullPath(workspace) }, readwritePaths);
        Assert.IsFalse(readwritePaths.Any(path => IsSameOrChild(scriptsRoot, path)));

        Assert.AreEqual("deny", root.GetProperty("network").GetProperty("egress").GetProperty("default").GetString());
        Assert.AreEqual("deny", root.GetProperty("network").GetProperty("ingress").GetProperty("hostLoopback").GetString());
        Assert.IsFalse(root.GetProperty("ui").GetProperty("disable").GetBoolean());
        Assert.AreEqual("none", root.GetProperty("ui").GetProperty("clipboard").GetString());
        Assert.IsTrue(root.GetProperty("processContainer").GetProperty("leastPrivilege").GetBoolean());
        Assert.AreEqual(
            "none",
            root.GetProperty("processContainer").GetProperty("ui").GetProperty("systemSettings").GetString());

        var environment = root.GetProperty("process").GetProperty("env")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.IsTrue(environment.Any(value => value.StartsWith("LOCALAPPDATA=", StringComparison.OrdinalIgnoreCase)));
        CollectionAssert.Contains(
            environment,
            $"{MxcProcessRunner.WorkspaceEnvironmentVariable}={Path.GetFullPath(workspace)}");
        Assert.IsFalse(environment.Any(value => value.StartsWith("SECRET_TOKEN=", StringComparison.OrdinalIgnoreCase)));

        Assert.ThrowsException<InvalidOperationException>(() =>
            MxcProcessRunner.BuildConfigurationJson(
                target,
                target.FileName,
                context,
                policy,
                Path.Combine(scriptsRoot, "unsafe-workspace"),
                timeoutMs: 0));
    }

    [TestMethod]
    public void Config_Keeps_Script_Package_Readonly_When_Filesystem_Restriction_Is_Disabled()
    {
        var scriptsRoot = Path.Combine(_root, "scripts");
        var package = Path.Combine(scriptsRoot, "demo");
        var workspace = Path.Combine(_root, "workspace");
        var input = Path.Combine(_root, "input.txt");
        Directory.CreateDirectory(package);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(input, "input");

        var target = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            WorkingDirectory = package,
        };
        var context = new ProcessExecutionContext
        {
            ScriptId = "demo",
            ScriptDirectory = package,
            ScriptsRoot = scriptsRoot,
            InputPaths = new[] { input },
        };
        var policy = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                RiskAccepted = true,
                DisabledPolicies = new List<string> { MxcPolicies.Filesystem },
            },
            "demo");

        Assert.IsTrue(policy.Enabled);
        Assert.IsFalse(policy.IsEnabled(MxcPolicies.Filesystem));

        var json = MxcProcessRunner.BuildConfigurationJson(
            target,
            target.FileName,
            context,
            policy,
            workspace,
            timeoutMs: 0);

        using var document = JsonDocument.Parse(json);
        var filesystem = document.RootElement.GetProperty("filesystem");
        var readonlyPaths = filesystem.GetProperty("readonlyPaths")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        var readwritePaths = filesystem.GetProperty("readwritePaths")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

        CollectionAssert.Contains(readonlyPaths, Path.GetFullPath(scriptsRoot));
        CollectionAssert.Contains(readonlyPaths, Path.GetFullPath(package));
        CollectionAssert.DoesNotContain(readonlyPaths, Path.GetFullPath(input));
        CollectionAssert.Contains(readwritePaths, Path.GetFullPath(workspace));
        CollectionAssert.Contains(readwritePaths, Path.GetPathRoot(Path.GetFullPath(_root))!);
    }

    [TestMethod]
    public void Config_Is_Not_Built_When_Mxc_Is_Disabled()
    {
        var workspace = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(workspace);
        var target = new ProcessStartInfo { FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe") };
        var context = new ProcessExecutionContext
        {
            ScriptId = "demo",
            ScriptDirectory = _root,
            ScriptsRoot = _root,
        };
        var policy = MxcPolicyResolver.Resolve(
            new MxcSettings
            {
                Enabled = false,
                RiskAccepted = true,
            },
            "demo");

        Assert.IsFalse(policy.Enabled);
        Assert.ThrowsException<InvalidOperationException>(() =>
            MxcProcessRunner.BuildConfigurationJson(
                target,
                target.FileName,
                context,
                policy,
                workspace,
                timeoutMs: 0));
    }

    [TestMethod]
    public void Executor_Discovery_Uses_Documented_Priority()
    {
        var explicitPath = Path.Combine(_root, "explicit.exe");
        var environmentPath = Path.Combine(_root, "environment.exe");
        var adjacentDirectory = Path.Combine(_root, "adjacent");
        var adjacentPath = Path.Combine(adjacentDirectory, "wxc-exec.exe");
        Directory.CreateDirectory(adjacentDirectory);
        File.WriteAllText(explicitPath, string.Empty);
        File.WriteAllText(environmentPath, string.Empty);
        File.WriteAllText(adjacentPath, string.Empty);

        var resolved = MxcProcessRunner.ResolveExecutor(
            new MxcSettings { ExecutorPath = explicitPath },
            environmentPath,
            adjacentDirectory,
            pathEnvironment: string.Empty);
        Assert.AreEqual(Path.GetFullPath(explicitPath), resolved);

        resolved = MxcProcessRunner.ResolveExecutor(
            new MxcSettings(),
            environmentPath,
            adjacentDirectory,
            pathEnvironment: string.Empty);
        Assert.AreEqual(Path.GetFullPath(environmentPath), resolved);

        resolved = MxcProcessRunner.ResolveExecutor(
            new MxcSettings(),
            environmentOverride: null,
            adjacentDirectory,
            pathEnvironment: string.Empty);
        Assert.AreEqual(Path.GetFullPath(adjacentPath), resolved);
    }

    [TestMethod]
    public void Missing_Executor_Fails_Closed_Without_Launching_Target()
    {
        var launched = false;
        var launcher = new RecordingLauncher(() => launched = true);
        var context = new ProcessExecutionContext
        {
            ScriptId = "demo",
            ScriptDirectory = _root,
            ScriptsRoot = _root,
        };

        var result = MxcProcessRunner.Run(
            new ProcessStartInfo { FileName = "cmd.exe" },
            context,
            MxcPolicyResolver.Resolve(new MxcSettings(), "demo"),
            new MxcSettings(),
            launcher: launcher,
            executorResolver: _ => null);

        Assert.AreEqual(MxcProcessRunner.UnavailableExitCode, result.ExitCode);
        StringAssert.Contains(result.StdErr, "wxc-exec.exe is unavailable");
        Assert.IsFalse(launched);
    }

    [TestMethod]
    public void Successful_Run_Preserves_NonEmpty_Workspace_Output()
    {
        var scriptsRoot = Path.Combine(_root, "scripts");
        var scriptDirectory = Path.Combine(scriptsRoot, "demo");
        var runDirectory = Path.Combine(_root, "runs", "mxc-test");
        Directory.CreateDirectory(scriptDirectory);

        var launcher = new OutputCreatingLauncher();
        var context = new ProcessExecutionContext
        {
            ScriptId = "demo",
            ScriptDirectory = scriptDirectory,
            ScriptsRoot = scriptsRoot,
        };

        var result = MxcProcessRunner.Run(
            new ProcessStartInfo { FileName = "cmd.exe" },
            context,
            MxcPolicyResolver.Resolve(new MxcSettings(), "demo"),
            new MxcSettings(),
            launcher: launcher,
            executorResolver: _ => "wxc-exec.exe",
            runDirectoryFactory: () => runDirectory);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(File.Exists(result.StdOut));
        Assert.IsFalse(File.Exists(Path.Combine(runDirectory, "mxc-config.json")));
    }

    private static bool IsSameOrChild(string root, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return relative == "." ||
            (!Path.IsPathRooted(relative) &&
             !relative.Equals("..", StringComparison.Ordinal) &&
             !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static MxcPlatformSupportResult SupportedPlatform() =>
        new()
        {
            IsSupported = true,
            Reason = "supported test host",
            WindowsBuild = MxcPlatformSupport.MinimumWindowsBuild,
            Tier = "test",
        };

    private sealed class RecordingLauncher(Action onRun) : IProcessLauncher
    {
        public ProcessRunResult Run(ProcessStartInfo startInfo, string? standardInput, int timeoutMs)
        {
            onRun();
            return new ProcessRunResult(0, string.Empty, string.Empty);
        }
    }

    private sealed class OutputCreatingLauncher : IProcessLauncher
    {
        public ProcessRunResult Run(ProcessStartInfo startInfo, string? standardInput, int timeoutMs)
        {
            using var config = JsonDocument.Parse(File.ReadAllText(startInfo.ArgumentList[0]));
            var workspace = config.RootElement.GetProperty("process").GetProperty("cwd").GetString()!;
            var outputPath = Path.Combine(workspace, "output.txt");
            File.WriteAllText(outputPath, "result");
            return new ProcessRunResult(0, outputPath, string.Empty);
        }
    }
}
