// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Tests;

[TestClass]
public class CliActionExecutionTests
{
    [TestMethod]
    public void FixedFileValuesUseTheActualScriptIdentityAndEffectivePolicy()
    {
        using var package = new Package();
        var input = package.CreateInput();
        var manifest = package.Write(Action(
            ("path", Constant(input)),
            ("json", Constant(false))));
        var settings = AcceptedDirectSettings();
        ProcessStartInfo? launched = null;
        ProcessExecutionContext? launchContext = null;
        MxcSettings? launchSettings = null;
        var timeout = 0;

        var result = CliActionProvider.Run(manifest, null, null, settings, package.Installation,
            (start, context, timeoutMs, effectiveSettings) =>
            {
                launched = start;
                launchContext = context;
                launchSettings = effectiveSettings;
                timeout = timeoutMs;
                return new ProcessRunResult(0, "query result", string.Empty);
            });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("query result", result.StdOut);
        Assert.IsNotNull(launched);
        Assert.AreEqual(Path.Combine(package.Installation, "FileLocksmithCLI.exe"), launched.FileName);
        Assert.AreEqual(package.Folder, launched.WorkingDirectory);
        Assert.IsFalse(launched.UseShellExecute);
        CollectionAssert.AreEqual(new[] { input }, launched.ArgumentList.ToArray());
        Assert.IsNotNull(launchContext);
        Assert.AreEqual(manifest.Id, launchContext.ScriptId);
        Assert.AreEqual(package.Folder, launchContext.ScriptDirectory);
        Assert.AreEqual(package.ScriptsRoot, launchContext.ScriptsRoot);
        CollectionAssert.AreEqual(new[] { input }, launchContext.InputPaths.ToArray());
        Assert.AreSame(settings, launchSettings);
        Assert.AreEqual(CliActionProvider.DefaultTimeoutMs, timeout);
    }

    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, false, false)]
    [DataRow(true, true, true)]
    public void FileLockQueriesCannotReportSuccessWithoutHostProcessVisibility(bool enabled, bool riskAccepted, bool disableDesktopIsolation)
    {
        using var package = new Package();
        var manifest = package.Write(Action(("path", Constant(package.CreateInput()))));
        var settings = new MxcSettings
        {
            Enabled = enabled,
            RiskAccepted = riskAccepted,
            DisabledPolicies = disableDesktopIsolation ? new List<string> { MxcPolicies.Ui } : new List<string>(),
        };
        var originalPolicies = settings.DisabledPolicies.ToArray();
        var launched = false;

        var result = CliActionProvider.Run(manifest, null, null, settings, package.Installation,
            (start, context, timeout, effectiveSettings) =>
            {
                launched = true;
                return default;
            });

        Assert.IsFalse(launched);
        Assert.AreEqual(ProcessRunner.UnsupportedIsolationExitCode, result.ExitCode);
        StringAssert.Contains(result.StdErr, "host processes");
        Assert.AreEqual(enabled, settings.Enabled);
        Assert.AreEqual(riskAccepted, settings.RiskAccepted);
        CollectionAssert.AreEqual(originalPolicies, settings.DisabledPolicies);
    }

    [DataTestMethod]
    [DataRow("fancyzones.get-layouts")]
    [DataRow("fancyzones.get-monitors")]
    public void DesktopIsolationIsReportedBeforeAttemptingToContactFancyZones(string commandId)
    {
        using var package = new Package();
        var action = new CliAction { SchemaVersion = 1, CommandId = commandId, Bindings = new() };
        var manifest = package.Write(action);
        var settings = new MxcSettings { Enabled = true };
        var launched = false;

        var result = CliActionProvider.Run(manifest, null, null, settings, package.Installation,
            (start, context, timeout, effectiveSettings) =>
            {
                launched = true;
                return default;
            });

        Assert.IsFalse(launched);
        Assert.AreEqual(ProcessRunner.UnsupportedIsolationExitCode, result.ExitCode);
        StringAssert.Contains(result.StdErr, "desktop-isolation");
        Assert.IsTrue(settings.Enabled);
        Assert.IsFalse(settings.RiskAccepted);
        Assert.AreEqual(0, settings.DisabledPolicies.Count);
    }

    [DataTestMethod]
    [DataRow("fancyzones.get-layouts")]
    [DataRow("fancyzones.get-monitors")]
    public void AcceptedDesktopExceptionPreservesTheRemainingSandboxSettings(string commandId)
    {
        using var package = new Package();
        var manifest = package.Write(new CliAction { SchemaVersion = 1, CommandId = commandId, Bindings = new() });
        var settings = new MxcSettings
        {
            Enabled = true,
            RiskAccepted = true,
            DisabledPolicies = new List<string> { MxcPolicies.Ui },
        };
        MxcSettings? launchedSettings = null;
        var launches = 0;

        var result = CliActionProvider.Run(manifest, null, null, settings, package.Installation,
            (start, context, timeout, effectiveSettings) =>
            {
                launchedSettings = effectiveSettings;
                launches++;
                return new ProcessRunResult(0, "module result", string.Empty);
            });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, launches);
        Assert.AreSame(settings, launchedSettings);
        Assert.IsTrue(settings.Enabled);
        Assert.IsTrue(settings.RiskAccepted);
        CollectionAssert.AreEqual(new[] { MxcPolicies.Ui }, settings.DisabledPolicies);
    }

    [TestMethod]
    public void AvailabilityUsesAcceptedEffectivePolicyWithoutInvalidatingTheConfiguration()
    {
        using var package = new Package();
        var manifest = package.Write(Action(("path", Constant(Path.Combine(package.Root, "may-exist-later.txt")))));
        var command = BuiltinCliCatalog.Load().Get("filelocksmith.query")!;
        Assert.IsNotNull(CliActionProvider.GetUnavailableReason(command, new MxcSettings { Enabled = true }));
        Assert.AreEqual(0, CliActionProvider.Validate(manifest).Count);
        Assert.IsNotNull(CliActionProvider.GetUnavailableReason(command, new MxcSettings { Enabled = false, RiskAccepted = false }));
        Assert.IsNull(CliActionProvider.GetUnavailableReason(command, AcceptedDirectSettings()));

        var desktop = BuiltinCliCatalog.Load().Get("fancyzones.get-layouts")!;
        Assert.IsNotNull(CliActionProvider.GetUnavailableReason(desktop, new MxcSettings
        {
            Enabled = true,
            RiskAccepted = false,
            DisabledPolicies = new List<string> { MxcPolicies.Ui },
        }));
        Assert.IsNull(CliActionProvider.GetUnavailableReason(desktop, AcceptedDirectSettings()));
    }

    [DataTestMethod]
    [DataRow(null, false)]
    [DataRow("false", false)]
    [DataRow("TRUE", true)]
    public void RuntimeValuesAndFalseDefaultsRemainDistinctFromFixedValues(string? suppliedJson, bool hasSwitch)
    {
        using var package = new Package();
        var input = package.CreateInput();
        var descriptor = Descriptor();
        descriptor["inputSchema"]!["properties"]!["parameters"] = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["file"] = new JsonObject { ["type"] = "string", ["format"] = "file-path" },
                ["returnJson"] = new JsonObject { ["type"] = "boolean", ["default"] = false },
            },
            ["required"] = new JsonArray("file"),
        };
        var manifest = package.Write(Action(
            ("path", new CliActionBinding { Parameter = "file" }),
            ("json", new CliActionBinding { Parameter = "returnJson" })), descriptor);
        var supplied = new Dictionary<string, string?> { ["file"] = input };
        if (suppliedJson is not null)
        {
            supplied["returnJson"] = suppliedJson;
        }

        string[]? arguments = null;
        var result = CliActionProvider.Run(manifest, null, supplied, AcceptedDirectSettings(), package.Installation,
            (start, context, timeout, settings) =>
            {
                arguments = start.ArgumentList.ToArray();
                return new ProcessRunResult(0, string.Empty, string.Empty);
            });

        Assert.IsTrue(result.Succeeded, result.StdErr);
        CollectionAssert.AreEqual(hasSwitch ? new[] { "--json", input } : new[] { input }, arguments!);
    }

    [TestMethod]
    public void MissingFixedFilesAreConfigurationValidButCannotLaunch()
    {
        using var package = new Package();
        var manifest = package.Write(Action(("path", Constant(Path.Combine(package.Root, "not-created.txt")))));
        Assert.AreEqual(0, CliActionProvider.Validate(manifest).Count);
        var launched = false;

        var result = CliActionProvider.Run(manifest, null, null, AcceptedDirectSettings(), package.Installation,
            (start, context, timeout, settings) =>
            {
                launched = true;
                return default;
            });

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(launched);
        StringAssert.Contains(result.StdErr, "existing file");
    }

    [TestMethod]
    public void MissingInstalledExecutablesNeverFallBackToAnotherProgram()
    {
        using var package = new Package();
        var manifest = package.Write(Action(("path", Constant(package.CreateInput()))));
        File.Delete(Path.Combine(package.Installation, "FileLocksmithCLI.exe"));
        File.WriteAllText(Path.Combine(package.Root, "FileLocksmithCLI.exe"), string.Empty);
        var launched = false;

        var result = CliActionProvider.Run(manifest, null, null, AcceptedDirectSettings(), package.Installation,
            (start, context, timeout, settings) =>
            {
                launched = true;
                return default;
            });

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(launched);
        StringAssert.Contains(result.StdErr, "FileLocksmithCLI.exe");
    }

    [TestMethod]
    public void ConflictingOrUnknownProvidersCannotReachTheProcessLauncher()
    {
        using var package = new Package();
        var manifest = package.Write(Action(("path", Constant(package.CreateInput()))));
        manifest.Execute!.Provider = "other-provider";
        manifest.Execute.Command = new List<string> { "cmd.exe", "/c", "echo other" };
        var launched = false;

        var result = CliActionProvider.Run(manifest, null, null, AcceptedDirectSettings(), package.Installation,
            (start, context, timeout, settings) =>
            {
                launched = true;
                return default;
            });

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(launched);
    }

    [TestMethod]
    public void NativeExitCodeAndDiagnosticsArePreservedWithoutRetry()
    {
        using var package = new Package();
        var action = new CliAction { SchemaVersion = 1, CommandId = "fancyzones.get-layouts", Bindings = new() };
        var descriptor = Descriptor();
        var manifest = package.Write(action, descriptor);
        var launches = 0;

        var result = CliActionProvider.Run(manifest, null, null, AcceptedDirectSettings(), package.Installation,
            (start, context, timeout, settings) =>
            {
                launches++;
                CollectionAssert.AreEqual(new[] { "get-layouts" }, start.ArgumentList.ToArray());
                return new ProcessRunResult(125, "diagnostic output", "sandbox refused");
            });

        Assert.AreEqual(1, launches);
        Assert.AreEqual(125, result.ExitCode);
        Assert.AreEqual("diagnostic output", result.StdOut);
        Assert.AreEqual("sandbox refused", result.StdErr);
        Assert.IsFalse(result.Succeeded);
    }

    [TestMethod]
    public void ArgumentVectorPreservesLiteralValuesAndAttachedResponseFilePrefixes()
    {
        var definition = new BuiltinCliDefinition
        {
            FixedArguments = new[] { "fixed subcommand" },
            Bindings = new[]
            {
                new CliArgumentBinding { Parameter = "name", Kind = CliArgumentKind.Option, Name = "--filename", AttachedValue = true },
                new CliArgumentBinding { Parameter = "empty", Kind = CliArgumentKind.Option, Name = "--empty", AttachedValue = true },
                new CliArgumentBinding { Parameter = "literal", Kind = CliArgumentKind.Positional },
                new CliArgumentBinding { Parameter = "flag", Kind = CliArgumentKind.Switch, Name = "--verbose" },
            },
        };
        const string literal = "Unicode 中文 %PATH% \"quoted\" & ;\nnext line";
        var start = CliActionProvider.CreateStartInfo(definition, "C:\\installed\\known.exe", "C:\\scripts", new Dictionary<string, string?>
        {
            ["name"] = "@snapshot",
            ["empty"] = string.Empty,
            ["literal"] = literal,
            ["flag"] = "false",
        });

        Assert.IsFalse(start.UseShellExecute);
        CollectionAssert.AreEqual(new[] { "fixed subcommand", "--filename=@snapshot", "--empty", string.Empty, literal }, start.ArgumentList.ToArray());
    }

    [DataTestMethod]
    [DataRow("../other.exe")]
    [DataRow("..\\other.exe")]
    [DataRow("C:\\other.exe")]
    [DataRow("https://example.test/other.exe")]
    [DataRow("known.cmd")]
    public void ExecutableResolutionRejectsAnythingOutsideTheInstalledRelativeExeContract(string candidate)
    {
        Assert.IsFalse(CliExecutableResolver.IsValidRelativeExecutable(candidate));
    }

    [TestMethod]
    public void ActionReaderBoundsEntrySizeAndNestingDepth()
    {
        Assert.ThrowsException<InvalidDataException>(() => CliActionProvider.Parse(new string(' ', CliActionProvider.MaximumActionBytes + 1)));
        var nested = new string('[', 20) + "0" + new string(']', 20);
        Assert.ThrowsException<InvalidDataException>(() => CliActionProvider.Parse(nested));
    }

    private static MxcSettings AcceptedDirectSettings() => new() { Enabled = false, RiskAccepted = true };

    private static JsonObject Descriptor()
    {
        var descriptor = TestTool.Descriptor(name: "literal-operation");
        descriptor["x-execute"] = new JsonObject { ["provider"] = CliActionProvider.ProviderName };
        descriptor["x-powerscript"] = new JsonObject { ["capabilities"] = new JsonArray("fileRead") };
        return descriptor;
    }

    private static CliAction Action(params (string Name, CliActionBinding Binding)[] bindings) => new()
    {
        SchemaVersion = 1,
        CommandId = "filelocksmith.query",
        Bindings = bindings.ToDictionary(pair => pair.Name, pair => pair.Binding),
    };

    private static CliActionBinding Constant(object value) => new() { Constant = JsonSerializer.SerializeToElement(value) };

    private sealed class Package : IDisposable
    {
        internal Package()
        {
            Root = Path.Combine(Path.GetTempPath(), "powerscripts-native-execution-" + Guid.NewGuid().ToString("N"));
            Installation = Path.Combine(Root, "installed");
            ScriptsRoot = Path.Combine(Root, "scripts");
            Folder = Path.Combine(ScriptsRoot, "literal-operation");
            Directory.CreateDirectory(Installation);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Installation, "FileLocksmithCLI.exe"), string.Empty);
            File.WriteAllText(Path.Combine(Installation, "FancyZonesCLI.exe"), string.Empty);
        }

        internal string Root { get; }

        internal string Installation { get; }

        internal string ScriptsRoot { get; }

        internal string Folder { get; }

        internal string CreateInput()
        {
            var input = Path.Combine(Root, "résumé 中文 %literal% @snapshot.txt");
            File.WriteAllText(input, "literal input");
            return input;
        }

        internal PowerScriptManifest Write(CliAction action, JsonObject? descriptor = null)
        {
            descriptor ??= Descriptor();
            var entry = Path.Combine(Folder, "action.json");
            File.WriteAllText(entry, JsonSerializer.Serialize(action));
            File.WriteAllText(entry + ".tool.json", descriptor.ToJsonString());
            var manifest = TestTool.Parse(descriptor, "action.json");
            manifest.FolderPath = Folder;
            manifest.ScriptsRoot = ScriptsRoot;
            return manifest;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
