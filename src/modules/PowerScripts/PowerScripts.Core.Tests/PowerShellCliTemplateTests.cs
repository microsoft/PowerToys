// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;
using PowerScripts.Core.Templates;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class PowerShellCliTemplateTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize() => _root = Path.Combine(Path.GetTempPath(), "ps-cli-content-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void EveryRecipeCreatesAnIndependentDiscoverablePowerShellPackage()
    {
        foreach (var template in PowerShellCliTemplates.All)
        {
            var created = ScriptTemplates.Create(_root, new(template.Name, Input: template.Input, Output: template.Output, CliContent: template.Id));
            Assert.AreEqual("script.ps1", Path.GetFileName(created.EntryPath));
            var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
            Assert.AreEqual(0, ManifestValidator.Validate(manifest, string.Empty).Count, template.Id);
            CollectionAssert.AreEquivalent(template.Parameters.Select(parameter => parameter.Name).ToArray(), manifest.Parameters.Select(parameter => parameter.Name).ToArray());
            StringAssert.Contains(File.ReadAllText(created.GuidePath), "never selected through PATH");
            Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(created.EntryPath)!, "local-policy.json")));
            var wire = JsonSerializer.Serialize(template, ToolDescriptorParser.JsonOptions);
            Assert.IsFalse(wire.Contains("\"arguments\"", StringComparison.Ordinal));
            Assert.IsFalse(wire.Contains("\"executable\"", StringComparison.Ordinal));
            var metadata = JsonSerializer.Deserialize<JsonElement>(wire);
            Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.GetProperty("moduleId").GetString()), template.Id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(metadata.GetProperty("group").GetString()), template.Id);
        }

        var registry = new ScriptRegistry(_root);
        registry.Load();
        Assert.AreEqual(0, registry.Errors.Count);
        Assert.AreEqual(PowerShellCliTemplates.All.Count, registry.Scripts.Count);
    }

    [DataTestMethod]
    [DataRow("python", "none", "text", "fancyzones")]
    [DataRow("powershell", "text", "text", "fancyzones")]
    [DataRow("powershell", "none", "files", "fancyzones")]
    [DataRow("powershell", "none", "text", "missing")]
    [DataRow("powershell", "none", "text", "")]
    public void InvalidCliContractsCannotPublishPackages(string runtime, string input, string output, string content)
    {
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root,
            new("Invalid", Runtime: runtime, Input: input, Output: output, CliContent: content)));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void ConflictingContentAndParameterBindingsCannotBeSilentlyIgnored()
    {
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create("greeting", _root, new("Invalid", CliContent: "fancyzones")));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root,
            new("Invalid", Operation: new("fancyzones.get-layouts"), CliContent: "fancyzones")));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root,
            new("Invalid", Parameters: [new("Command", new() { Type = "string" })], CliContent: "fancyzones")));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root,
            new("Invalid", Input: "files", Parameters: [new("Files", new() { Type = "string" })], CliContent: "file-locksmith")));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void HostExecutionNoticesTravelWithTheCatalogueAndGuideWithoutApprovingCreation()
    {
        foreach (var template in PowerShellCliTemplates.All)
        {
            var wire = JsonSerializer.SerializeToElement(template, ToolDescriptorParser.JsonOptions);
            Assert.AreEqual(template.HostExecutionNotice, wire.GetProperty("hostExecutionNotice").GetString());
            var created = ScriptTemplates.Create(_root, new(template.Name, Input: template.Input, Output: template.Output, CliContent: template.Id));
            StringAssert.Contains(File.ReadAllText(created.GuidePath), template.HostExecutionNotice!);
            var descriptor = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(created.EntryPath + ".tool.json"))!;
            Assert.IsNull(descriptor["x-powerscript"]!["mxc"], "Warnings must not grant an isolation override.");
            Assert.AreEqual(template.Id, descriptor["x-powerscript"]!["hostCli"]!["id"]!.GetValue<string>());
        }
    }

    [DataTestMethod]
    [DataRow("fixed", "none")]
    [DataRow("input", "text")]
    [DataRow("parameter", "none")]
    public void SettingsModuleControlSupportsFixedInputAndRuntimeModuleNames(string source, string input)
    {
        var bindings = FixedDefaults("settings-module-state");
        bindings["Command"] = Fixed("disable");
        bindings["ModuleName"] = source == "fixed" ? Fixed("FancyZones") : new(source);
        var created = ScriptTemplates.Create(_root, new("Save module state", Input: input,
            CliContent: "settings-module-state", CliBindings: bindings));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        CollectionAssert.AreEqual(source == "parameter" ? new[] { "ModuleName" } : [],
            manifest.Parameters.Select(parameter => parameter.Name).ToArray());
        var script = File.ReadAllText(created.EntryPath);
        StringAssert.Contains(script, "$Command = 'disable'");
        StringAssert.Contains(script, "PowerToys.Settings.Cli.exe");
        if (source != "parameter")
        {
            StringAssert.Contains(script, source == "input" ? "$ModuleName = $Text" : "$ModuleName = 'FancyZones'");
        }

        var descriptor = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(created.EntryPath + ".tool.json"))!;
        Assert.IsTrue(descriptor["x-powerscript"]!["capabilities"]!.AsArray().Any(capability => capability!.GetValue<string>() == "fileWrite"));
    }

    [TestMethod]
    public void FixedInputAndRuntimeArgumentsProduceOnlyTheRequestedScriptParameters()
    {
        var bindings = FixedDefaults("fancyzones");
        bindings["Command"] = Fixed("set-layout");
        bindings["Layout"] = new("input");
        bindings["Monitor"] = new("parameter");
        var created = ScriptTemplates.Create(_root, new("Apply incoming layout", Input: "text", CliContent: "fancyzones", CliBindings: bindings));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        CollectionAssert.AreEqual(new[] { "Monitor" }, manifest.Parameters.Select(parameter => parameter.Name).ToArray());
        var script = File.ReadAllText(created.EntryPath);
        StringAssert.Contains(script, "$Command = 'set-layout'");
        StringAssert.Contains(script, "$Layout = $Text");
        StringAssert.Contains(script, "$AllMonitors = $false");
        StringAssert.Contains(script, "ArgumentList.Add");
    }

    [DataTestMethod]
    [DataRow("input", "text", 0)]
    [DataRow("fixed", "none", 0)]
    [DataRow("parameter", "none", 1)]
    public void LocalTextCanComeFromPrimaryInputFixedContentOrARuntimeParameter(string source, string input, int parameterCount)
    {
        var bindings = FixedDefaults("advanced-paste-local");
        bindings["Content"] = source == "fixed" ? Fixed("Local text") : new(source);
        var created = ScriptTemplates.Create(_root, new("Local transformation", Input: input,
            CliContent: "advanced-paste-local", CliBindings: bindings));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;

        Assert.AreEqual(parameterCount, manifest.Parameters.Count);
        Assert.AreEqual(0, ManifestValidator.Validate(manifest, string.Empty).Count);
        var script = File.ReadAllText(created.EntryPath);
        StringAssert.Contains(script, "StandardInput.Write($Content)");
        if (source != "parameter")
        {
            StringAssert.Contains(script, source == "input" ? "$Content = $Text" : "$Content = 'Local text'");
        }
    }

    [DataTestMethod]
    [DataRow("input", "files", 0)]
    [DataRow("fixed", "none", 0)]
    [DataRow("parameter", "none", 1)]
    public void FilePathSourceDeterminesPrimaryFilesContractAndPickerParameter(string source, string input, int parameterCount)
    {
        var bindings = FixedDefaults("file-locksmith");
        bindings["SourceFile"] = source == "fixed" ? Fixed(@"C:\fixtures\quoted ' name.txt") : new(source);
        var created = ScriptTemplates.Create(_root, new("Inspect a file", Input: input, CliContent: "file-locksmith", CliBindings: bindings));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        Assert.AreEqual(parameterCount, manifest.Parameters.Count);
        var descriptor = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(created.EntryPath + ".tool.json"))!;
        Assert.AreEqual(input == "files", descriptor["inputSchema"]!["properties"]!["files"] is not null);
        if (source == "parameter")
        {
            Assert.IsTrue(manifest.Parameters.Single().IsFile);
            Assert.IsTrue(manifest.Parameters.Single().IsRequired);
        }
        else if (source == "fixed")
        {
            StringAssert.Contains(File.ReadAllText(created.EntryPath), @"'C:\fixtures\quoted '' name.txt'");
        }
    }

    [DataTestMethod]
    [DataRow("Monitor", 0)]
    [DataRow("Hotkey", 10)]
    public void FixedNumericValuesMustSatisfyTheCliBounds(string name, int value)
    {
        var bindings = FixedDefaults("fancyzones");
        bindings[name] = Fixed(value);
        Assert.ThrowsException<InvalidDataException>(() => ScriptTemplates.Create(_root,
            new("Invalid", CliContent: "fancyzones", CliBindings: bindings)));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void UnknownAmbiguousAndMixedSourceBindingsCannotPublishPackages()
    {
        var bindings = FixedDefaults("fancyzones");
        bindings["Unknown"] = Fixed("value");
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root, new("Invalid", CliContent: "fancyzones", CliBindings: bindings)));
        bindings.Remove("Unknown");
        bindings["Command"] = new("parameter", JsonSerializer.SerializeToElement("get-layouts"));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root, new("Invalid", CliContent: "fancyzones", CliBindings: bindings)));
        bindings["Command"] = new("unsupported");
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root, new("Invalid", CliContent: "fancyzones", CliBindings: bindings)));
        bindings["Command"] = null!;
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root, new("Invalid", CliContent: "fancyzones", CliBindings: bindings)));
        var mixed = FixedDefaults("file-locksmith");
        mixed["Json"] = new("input");
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root, new("Invalid", Input: "files", CliContent: "file-locksmith", CliBindings: mixed)));
        Assert.ThrowsException<ArgumentException>(() => ScriptTemplates.Create(_root, new("Invalid", CliBindings: bindings)));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void WrongFixedTypesAndInvalidChoicesAreRejectedBeforeWritingFiles()
    {
        var bindings = FixedDefaults("fancyzones");
        bindings["AllMonitors"] = Fixed("false");
        Assert.ThrowsException<InvalidDataException>(() => ScriptTemplates.Create(_root, new("Invalid", CliContent: "fancyzones", CliBindings: bindings)));
        bindings["AllMonitors"] = Fixed(false);
        bindings["Command"] = Fixed("not-a-command");
        Assert.ThrowsException<InvalidDataException>(() => ScriptTemplates.Create(_root, new("Invalid", CliContent: "fancyzones", CliBindings: bindings)));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void GeneratedDiscreteArgumentsAndNativeFailureDetailsRemainSeparate()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        if (!OperatingSystem.IsWindows() || !File.Exists(executable))
        {
            Assert.Inconclusive("This invocation check requires Windows and PowerShell 7.");
        }

        var created = ScriptTemplates.Create(_root, new("Discrete arguments", CliContent: "power-display-discrete",
            CliBindings: FixedDefaults("power-display-discrete")));
        var command = """
            $ErrorActionPreference = 'Stop'
            $script = [IO.File]::ReadAllText($env:CLI_TEMPLATE_ARGS_ENTRY)
            $start = $script.IndexOf('$__powerscriptArguments =')
            $end = $script.IndexOf('$__powerscriptStartInfo =')
            $Setting = 'orientation'; $Value = '90'; $MonitorId = 'test-target'; $MonitorNumber = 0
            $arguments = & ([ScriptBlock]::Create($script.Substring($start, $end - $start) + '; $__powerscriptArguments.ToArray()'))
            if ($arguments.Count -ne 5 -or $arguments[3] -cne '--orientation' -or $arguments[4] -cne '90') { throw 'The discrete flag and value must be separate native arguments.' }
            $__powerscriptExecutable = Join-Path $PSHOME 'pwsh.exe'
            $__powerscriptArguments = [Collections.Generic.List[string]]::new()
            $__powerscriptArguments.AddRange([string[]]@('-NoProfile', '-NonInteractive', '-Command', "[Console]::Out.Write('native failure detail'); exit 17"))
            try {
                & ([ScriptBlock]::Create($script.Substring($end)))
                throw 'Expected a nonzero native exit.'
            } catch {
                if ($_.Exception.Message -notlike '*exit code 17*') { throw }
            }
            exit 0
            """;
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
        startInfo.Environment["CLI_TEMPLATE_ARGS_ENTRY"] = created.EntryPath;
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(30_000), "Generated invocation check did not finish.");
        Assert.AreEqual(0, process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
        StringAssert.Contains(stderr.GetAwaiter().GetResult(), "native failure detail");
        Assert.AreEqual(string.Empty, stdout.GetAwaiter().GetResult(), "Native failures must not produce success-shaped stdout.");
    }

    [TestMethod]
    public void GeneratedPowerShellParsesAndKeepsQuotedDefaultsAsLiteralData()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Module CLI templates target Windows; this parsing check uses Windows PowerShell.");
        }

        const string sentinel = "quoted ' text; $(throw 'must not run') \" & |";
        foreach (var template in PowerShellCliTemplates.All)
        {
            ScriptTemplates.Create(_root, new(template.Name, Input: template.Input, Output: template.Output, CliContent: template.Id,
                Parameters: [new("LiteralValue", new() { Type = "string", Default = JsonSerializer.SerializeToElement(sentinel) })]));
        }

        var fixedBindings = FixedDefaults("fancyzones");
        fixedBindings["Layout"] = Fixed(sentinel);
        ScriptTemplates.Create(_root, new("Quoted fixed values", CliContent: "fancyzones", CliBindings: fixedBindings,
            Parameters: [new("LiteralValue", new() { Type = "string", Default = JsonSerializer.SerializeToElement(sentinel) })]));
        var inputBindings = FixedDefaults("fancyzones");
        inputBindings["Monitor"] = new("input");
        inputBindings["AllMonitors"] = new("input");
        ScriptTemplates.Create(_root, new("Typed text input", Input: "text", CliContent: "fancyzones", CliBindings: inputBindings,
            Parameters: [new("LiteralValue", new() { Type = "string", Default = JsonSerializer.SerializeToElement(sentinel) })]));

        var command = """
            $ErrorActionPreference = 'Stop'
            foreach ($file in Get-ChildItem -LiteralPath $env:CLI_TEMPLATE_PARSE_ROOT -Filter script.ps1 -Recurse) {
                $tokens = $null; $errors = $null
                $ast = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors)
                if ($errors.Count) { throw ($file.FullName + ': ' + ($errors.Message -join '; ')) }
                $parameter = $ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'LiteralValue' }
                if ($parameter.DefaultValue.SafeGetValue() -cne $env:CLI_TEMPLATE_PARSE_VALUE) { throw 'Default value was not preserved as literal data.' }
            }
            """;
        var startInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
        startInfo.Environment["CLI_TEMPLATE_PARSE_ROOT"] = _root;
        startInfo.Environment["CLI_TEMPLATE_PARSE_VALUE"] = sentinel;
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(30_000), "PowerShell parsing did not finish.");
        Assert.AreEqual(0, process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    [DataTestMethod]
    [DataRow("fancyzones", "none", "text")]
    [DataRow("workspaces", "none", "text")]
    [DataRow("light-switch", "none", "text")]
    [DataRow("awake", "none", "text")]
    [DataRow("settings-module-state", "none", "text")]
    [DataRow("power-display-query", "none", "text")]
    [DataRow("power-display-adjust", "none", "text")]
    [DataRow("power-display-discrete", "none", "text")]
    [DataRow("advanced-paste-transform", "files", "text")]
    [DataRow("file-locksmith", "files", "text")]
    [DataRow("image-resizer", "files", "files")]
    public void RetiredRecipesKeepExistingSavedContracts(string id, string input, string output)
    {
        var created = ScriptTemplates.Create(_root, new("Existing " + id, Input: input, Output: output,
            CliContent: id, CliBindings: FixedDefaults(id)));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        Assert.AreEqual(0, ManifestValidator.Validate(manifest, string.Empty).Count);
        Assert.AreEqual(id, manifest.HostCli!.Id);
        Assert.IsFalse(PowerShellCliTemplates.All.Any(template => template.Id == id), "Mixed legacy contracts must not be offered for new creation.");
    }

    [DataTestMethod]
    [DataRow("advanced-paste-file-result", false)]
    [DataRow("advanced-paste-file-result", true)]
    [DataRow("advanced-paste-text-file", false)]
    [DataRow("advanced-paste-text-file", true)]
    [DataRow("advanced-paste-html-file", false)]
    [DataRow("advanced-paste-html-file", true)]
    [DataRow("advanced-paste-ai-text-file", false)]
    [DataRow("advanced-paste-ai-text-file", true)]
    [DataRow("advanced-paste-ai-files-file", false)]
    [DataRow("advanced-paste-ai-files-file", true)]
    public void GeneratedFileResultsRequireAFileAndKeepNativeProgressOffStdout(string id, bool missing)
    {
        var result = RunGeneratedFixture(id, $$"""
            $fixture = & ([ScriptBlock]::Create($builder + '; @{ arguments = $__powerscriptArguments.ToArray(); file = $__powerscriptResultFile }'))
            if ($fixture.arguments -contains '--stdout' -or $fixture.arguments -contains '--output-clipboard') { throw 'File recipes must use an explicit file output.' }
            $__powerscriptResultFile = $fixture.file
            $__powerscriptOutput = 'native progress, not primary output'
            if (-not ${{(missing ? "true" : "false")}}) { [IO.File]::WriteAllText($__powerscriptResultFile, 'fixture output') }
            try {
                & ([ScriptBlock]::Create($env:CLI_TEMPLATE_OUTPUT_CODE))
                if (${{(missing ? "true" : "false")}}) { throw 'Missing output was accepted.' }
            } catch {
                if (-not ${{(missing ? "true" : "false")}} -or $_.Exception.Message -notlike '*did not produce the requested output file*') { throw }
            }
            """);
        StringAssert.Contains(result.Stderr, "native progress, not primary output");
        if (missing)
        {
            Assert.AreEqual(string.Empty, result.Stdout, "Missing output cannot produce a success envelope.");
        }
        else
        {
            using var document = JsonDocument.Parse(result.Stdout);
            var files = document.RootElement.GetProperty("files").EnumerateArray().Select(file => file.GetString()!).ToArray();
            Assert.AreEqual(1, files.Length);
            Assert.IsTrue(File.Exists(files[0]));
            Assert.AreEqual("fixture output", File.ReadAllText(files[0]));
            Assert.IsTrue(Path.GetFullPath(files[0]).StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
    }

    [DataTestMethod]
    [DataRow("1, 2,1", true)]
    [DataRow("1,,2", false)]
    [DataRow("1,0", false)]
    [DataRow("1,--confirm-power-off", false)]
    public void DisplayBatchesRemainOneValidatedNativeArgument(string monitors, bool valid)
    {
        var result = RunGeneratedFixture("power-display-adjust-action", $$"""
            $MonitorNumbers = $env:CLI_TEMPLATE_MONITORS
            try {
                $arguments = @(& ([ScriptBlock]::Create($builder + '; $__powerscriptArguments.ToArray()')))
                if (-not ${{(valid ? "true" : "false")}}) { throw 'Invalid monitor batch was accepted.' }
                if ($arguments.Count -ne 5 -or $arguments[1] -cne '--monitor-number' -or $arguments[2] -cne '1,2,1' -or $arguments[3] -cne '--brightness') { throw 'Batch targets and setting flags must remain separate native arguments.' }
            } catch {
                if (${{(valid ? "true" : "false")}} -or $_.Exception.Message -notlike '*comma-separated positive monitor indices*') { throw }
            }
            """, monitors);
        Assert.AreEqual(string.Empty, result.Stdout);
    }

    [DataTestMethod]
    [DataRow("dimensions", "0", "768", true)]
    [DataRow("dimensions", "0", "0", false)]
    [DataRow("preset", "invalid ignored width", "invalid ignored height", true)]
    public void ResizerSupportsAutomaticDimensionsAndPresetsWithoutPassingOriginals(string sizing, string width, string height, bool valid)
    {
        var result = RunGeneratedFixture("image-resizer-options", $$"""
            $Sizing = '{{sizing}}'; $Width = '{{width}}'; $Height = '{{height}}'
            $JpegQuality = 80; $RemoveMetadata = $true
            try {
                $arguments = @(& ([ScriptBlock]::Create($builder + '; $__powerscriptArguments.ToArray()')))
                if (-not ${{(valid ? "true" : "false")}}) { throw 'Zero dimensions were accepted.' }
                if ($arguments -contains $Files[0]) { throw 'An original file was passed to the CLI.' }
                if (-not ($arguments -contains '--quality') -or -not ($arguments -contains '--remove-metadata')) { throw 'Resize options were not forwarded.' }
                if ($Sizing -eq 'preset' -and (($arguments -contains '--width') -or -not ($arguments -contains '--size'))) { throw 'Preset settings were overwritten by dimensions.' }
                $separator = [Array]::IndexOf($arguments, '--')
                if ($separator -lt 0 -or $arguments.Count -ne $separator + 2) { throw 'Expected one staged copy after the native argument separator.' }
                if ([IO.File]::ReadAllText($arguments[-1]) -cne 'original fixture') { throw 'Staged content was not preserved.' }
            } catch {
                if (${{(valid ? "true" : "false")}} -or $_.Exception.Message -notlike '*At least one dimension must be positive*') { throw }
            }
            if ([IO.File]::ReadAllText($Files[0]) -cne 'original fixture') { throw 'Original content changed.' }
            """);
        Assert.AreEqual(string.Empty, result.Stdout);
    }

    [DataTestMethod]
    [DataRow("file-locksmith-query", "files", "text")]
    [DataRow("file-locksmith-wait", "files", "none")]
    public void FileLocksmithBatchesCreateCompatiblePackagesAndNeverTerminateProcesses(string id, string input, string output)
    {
        var created = ScriptTemplates.Create(_root, new("Batch " + id, Input: input, Output: output, CliContent: id, MaxFiles: 0));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        Assert.IsNull(manifest.Input!.MaxFiles, "Batch input must not be limited to one file.");
        Assert.AreEqual(0, ManifestValidator.Validate(manifest, string.Empty).Count);
        var result = RunGeneratedFixture(id, """
            $Files = @($Files[0], [IO.Path]::GetDirectoryName($Files[0]))
            $arguments = @(& ([ScriptBlock]::Create($builder + '; $__powerscriptArguments.ToArray()')))
            if ($arguments -contains '--kill') { throw 'A query/wait must never terminate processes.' }
            if ($arguments[-2] -cne $Files[0] -or $arguments[-1] -cne $Files[1]) { throw 'Every requested path must remain a separate native argument.' }
            if ($arguments -contains '--wait' -and $arguments -contains '--json') { throw 'Native wait does not support JSON output.' }
            """);
        Assert.AreEqual(string.Empty, result.Stdout);
    }

    [TestMethod]
    public void ProfileApplicationUsesThePositiveNumericSelectorWithoutExtraFlags()
    {
        var bindings = FixedDefaults("power-display-profile");
        bindings["ProfileId"] = Fixed(0);
        Assert.ThrowsException<InvalidDataException>(() => ScriptTemplates.Create(_root,
            new("Invalid profile", Output: "none", CliContent: "power-display-profile", CliBindings: bindings)));
        var result = RunGeneratedFixture("power-display-profile", """
            $ProfileId = 3
            $arguments = @(& ([ScriptBlock]::Create($builder + '; $__powerscriptArguments.ToArray()')))
            if ($arguments.Count -ne 2 -or $arguments[0] -cne 'apply-profile' -or $arguments[1] -cne '3') { throw 'Profile application requires one numeric selector.' }
            $__powerscriptOutput = 'profile application status'
            & ([ScriptBlock]::Create($env:CLI_TEMPLATE_OUTPUT_CODE))
            """);
        Assert.AreEqual(string.Empty, result.Stdout, "Action status must not masquerade as primary text.");
        StringAssert.Contains(result.Stderr, "profile application status");
    }

    private static PowerShellCliBinding Fixed<T>(T value) => new("fixed", JsonSerializer.SerializeToElement(value));

    private static Dictionary<string, PowerShellCliBinding> FixedDefaults(string id)
    {
        var template = PowerShellCliTemplates.Get(id);
        var result = template.Parameters.ToDictionary(parameter => parameter.Name, parameter => new PowerShellCliBinding("fixed", parameter.Schema.Default));
        if (template.InputArgument is { } file)
        {
            result.Add(file.Name, new("input"));
        }

        return result;
    }

    private (string Stdout, string Stderr) RunGeneratedFixture(string id, string command, string monitors = "")
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        if (!OperatingSystem.IsWindows() || !File.Exists(executable))
        {
            Assert.Inconclusive("Generated recipe fixtures require Windows and PowerShell 7.");
        }

        var template = PowerShellCliTemplates.Get(id);
        var created = ScriptTemplates.Create(_root, new("Fixture " + id, Input: template.Input, Output: template.Output, CliContent: id));
        var input = Path.Combine(_root, "original.txt");
        File.WriteAllText(input, "original fixture");
        var body = """
            $ErrorActionPreference = 'Stop'
            $generated = [IO.File]::ReadAllText($env:CLI_TEMPLATE_ARGS_ENTRY)
            $begin = $generated.IndexOf('$__powerscriptArguments =')
            $end = $generated.IndexOf('$__powerscriptStartInfo =')
            $builder = $generated.Substring($begin, $end - $begin)
            $values = ConvertFrom-Json -InputObject $env:CLI_TEMPLATE_DEFAULTS -AsHashtable
            foreach ($name in $values.Keys) { Set-Variable -Name $name -Value $values[$name] }
            $Files = @($env:CLI_TEMPLATE_INPUT); $Content = '<p>fixture text</p>'
            """ + Environment.NewLine + command + Environment.NewLine + "exit 0";
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(body)));
        startInfo.Environment["CLI_TEMPLATE_ARGS_ENTRY"] = created.EntryPath;
        startInfo.Environment["CLI_TEMPLATE_OUTPUT_CODE"] = template.OutputScript ?? string.Empty;
        startInfo.Environment["CLI_TEMPLATE_DEFAULTS"] = JsonSerializer.Serialize(template.Parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Schema.Default));
        startInfo.Environment["CLI_TEMPLATE_INPUT"] = input;
        startInfo.Environment["CLI_TEMPLATE_MONITORS"] = monitors;
        startInfo.Environment["POWERSCRIPTS_MXC_WORKSPACE"] = _root;
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(30_000), "Generated fixture did not finish.");
        var result = (Stdout: stdout.GetAwaiter().GetResult(), Stderr: stderr.GetAwaiter().GetResult());
        Assert.AreEqual(0, process.ExitCode, result.Stdout + result.Stderr);
        return result;
    }

}
