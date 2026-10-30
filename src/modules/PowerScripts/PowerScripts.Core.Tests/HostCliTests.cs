// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;
using PowerScripts.Core;
using PowerScripts.Core.Storage;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Tests;

/// <summary>
/// Guards the <c>PowerScripts.Host.exe</c> CLI contract that every consuming module (Keyboard
/// Manager, Command Palette, Advanced Paste, the C++ Explorer menu) depends on. These tests spawn the
/// real host against a throw-away scripts folder (<c>--root</c>) and assert the shape of <c>list</c>
/// and its discovery filters, so a change that breaks basic enumeration fails here rather than in a
/// shipped surface. The host is force-built via a build-order ProjectReference, so a missing exe is a
/// hard failure — never a silent skip.
/// </summary>
[TestClass]
public class HostCliTests
{
    private static string _hostPath = string.Empty;
    private string _root = string.Empty;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _hostPath = PowerScriptsClient.ResolveHostPath() ?? string.Empty;
        Assert.IsFalse(
            string.IsNullOrEmpty(_hostPath),
            "PowerScripts.Host.exe was not found. The test project references the Host project for " +
            "build order, so this should not happen — ensure the Host builds.");
    }

    [TestInitialize]
    public void Init()
    {
        _root = Path.Combine(Path.GetTempPath(), "ps-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        // A system action: consumes nothing, produces text (its stdout).  io = none -> text
        Write("act_hello.ps1", "Write-Output 'hi'");
        Write("act_hello.ps1.tool.json", """
            {
              "name": "act_hello",
              "description": "act_hello",
              "inputSchema": {
                "type": "object",
                "properties": {
                  "parameters": {
                    "type": "object",
                    "properties": {
                      "format": {
                        "type": "string",
                        "description": "Output format",
                        "enum": [
                          "short",
                          "long"
                        ],
                        "default": "short"
                      }
                    },
                    "required": [
                      "format"
                    ],
                    "additionalProperties": false
                  }
                },
                "required": [],
                "additionalProperties": false
              },
              "outputSchema": {
                "type": "object",
                "properties": {
                  "text": {
                    "type": "string"
                  }
                },
                "required": [
                  "text"
                ],
                "additionalProperties": false
              }
            }
            """);

        Write("py_upper.py", "def uppercase(text):\n    return text.upper()\n");
        Write("py_upper.py.tool.json", "{\"name\": \"py_upper\", \"description\": \"py_upper\", \"inputSchema\": {\"type\": \"object\", \"properties\": {\"text\": {\"type\": \"string\"}}, \"required\": [\"text\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {\"text\": {\"type\": \"string\"}}, \"required\": [\"text\"], \"additionalProperties\": false}}");

        // The same PNG file contract applies to clipboard temporary files and Explorer selections.
        Write("py_named.py", "def inspect_files(paths):\n    return '\\n'.join(paths)\n");
        Write("py_named.py.tool.json", "{\"name\": \"py_named\", \"description\": \"py_named\", \"inputSchema\": {\"type\": \"object\", \"properties\": {\"files\": {\"type\": \"array\", \"items\": {\"type\": \"string\", \"x-powerscript\": {\"extensions\": [\".png\"]}}, \"minItems\": 1, \"maxItems\": 1}}, \"required\": [\"files\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {\"text\": {\"type\": \"string\"}}, \"required\": [\"text\"], \"additionalProperties\": false}}");

        // Rich HTML is a file output; literal markup would be a text output.
        Write("md2html.ps1", """
            param([string]$Text)
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            $output = Join-Path $env:POWERSCRIPTS_MXC_WORKSPACE 'result.html'
            Set-Content -LiteralPath $output -Value "<p>$Text</p>" -Encoding UTF8
            @{ files = @($output) } | ConvertTo-Json -Compress
            """);
        Write("md2html.ps1.tool.json", "{\"name\": \"md2html\", \"description\": \"md2html\", \"inputSchema\": {\"type\": \"object\", \"properties\": {\"text\": {\"type\": \"string\"}}, \"required\": [\"text\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {\"files\": {\"type\": \"array\", \"items\": {\"type\": \"string\"}}}, \"required\": [\"files\"], \"additionalProperties\": false}}");

        // A file script: acts on selected files (file-driven because it declares extensions).  io = files -> none
        Write("wc.ps1", "param([string]$file)\n(Get-Content $file).Length");
        Write("wc.ps1.tool.json", "{\"name\": \"wc\", \"description\": \"wc\", \"inputSchema\": {\"type\": \"object\", \"properties\": {\"files\": {\"type\": \"array\", \"items\": {\"type\": \"string\", \"x-powerscript\": {\"extensions\": [\".md\"]}}, \"minItems\": 1}}, \"required\": [\"files\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {}, \"required\": [], \"additionalProperties\": false}}");
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
    [DataRow("linux-container-status")]
    [DataRow("LINUX-CONTAINER-SETUP")]
    public void LinuxEnvironmentCommandsRejectUnexpectedArgumentsBeforePreparation(string command)
    {
        var (exit, _, error) = RunHost(command, "unexpected");
        Assert.AreEqual(1, exit);
        StringAssert.Contains(error, "do not accept arguments");
    }

    [TestMethod]
    [DataRow("""{"python":{"mode":"wsl","wsl":{"containerImage":"--invalid-image"}}}""", "--invalid-image", "linux-container-status")]
    [DataRow("""{"python":{"mode":"wsl"}}""", "", "linux-container-status")]
    [DataRow("""{"python":{"mode":"wsl"}}""", "", "linux-container-setup")]
    [DoNotParallelize]
    public void LinuxEnvironmentCommandsReportTheConfiguredImageAndDoNotFallback(string configuration, string image, string command)
    {
        var previous = SettingsStore.Current;
        var previousError = Console.Error;
        using var error = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            SettingsStore.Current = new FileSettingsStore(Path.Combine(_root, "settings"));
            SettingsStore.Current.WriteBlob(PowerScriptsPaths.ConfigFileName, configuration);
            Console.SetError(error);
            var (exit, stdout) = RunHostInProcess(command);
            if (command == "linux-container-setup")
            {
                Assert.AreEqual(2, exit);
                Assert.AreEqual(string.Empty, stdout);
                StringAssert.Contains(error.ToString(), "image reference");
            }
            else
            {
                Assert.AreEqual(0, exit);
                using var status = JsonDocument.Parse(stdout);
                Assert.IsFalse(status.RootElement.GetProperty("isReady").GetBoolean());
                Assert.AreEqual(image, status.RootElement.GetProperty("image").GetString());
                StringAssert.Contains(status.RootElement.GetProperty("message").GetString()!, "image reference");
            }
        }
        finally
        {
            Console.SetError(previousError);
            SettingsStore.Current = previous;
        }
    }

    [TestMethod]
    public void CatalogueDistinguishesDescriptorLaunchCommandsFromBuiltInPython()
    {
        Write("recipe.py", "print('custom interpreter')");
        Write("recipe.py.tool.json", """
            {
              "name": "recipe",
              "description": "Explicit Python command",
              "inputSchema": {
                "type": "object",
                "properties": {},
                "required": [],
                "additionalProperties": false
              },
              "x-execute": {
                "command": [
                  "python",
                  "recipe.py"
                ]
              },
              "outputSchema": {
                "type": "object",
                "properties": {
                  "text": {
                    "type": "string"
                  }
                },
                "required": [
                  "text"
                ],
                "additionalProperties": false
              }
            }
            """);
        using var json = JsonDocument.Parse(RunHostOk("list", "--json", "--include-disabled", "--root", _root));
        var scripts = json.RootElement.EnumerateArray().ToDictionary(row => row.GetProperty("id").GetString()!);
        Assert.IsTrue(scripts["recipe"].GetProperty("hasExplicitExecution").GetBoolean());
        Assert.IsFalse(scripts["recipe"].TryGetProperty("transform", out _));
        Assert.IsFalse(scripts["py_upper"].GetProperty("hasExplicitExecution").GetBoolean());
        Assert.AreEqual(JsonValueKind.Object, scripts["py_upper"].GetProperty("inputSchema").ValueKind);
    }

    [TestMethod]
    public void TemplateConfigurationFlowsFromStdinIntoDiscoverableMetadata()
    {
        var (exit, stdout, error) = RunHostWithInput(
            """{"projectName":"My input files","description":"Configured from the wizard.","fileExtensions":"TXT; *.md"}""",
            "template", "create", "file-info", "--configure", "--root", _root);
        Assert.AreEqual(0, exit, error);
        using var created = JsonDocument.Parse(stdout);
        var entry = created.RootElement.GetProperty("entryPath").GetString()!;
        var manifest = ToolDescriptorParser.TryParseFile(entry + ".tool.json")!;
        Assert.AreEqual("My input files", manifest.Name);
        CollectionAssert.AreEqual(new[] { ".txt", ".md" }, manifest.Input!.Extensions);

        var before = Directory.GetDirectories(_root);
        (exit, _, error) = RunHostWithInput("""{"projectName":"Invalid","fileExtensions":"../txt"}""",
            "template", "create", "file-info", "--configure", "--root", _root);
        Assert.AreNotEqual(0, exit);
        StringAssert.Contains(error, "extensions");
        CollectionAssert.AreEquivalent(before, Directory.GetDirectories(_root));
    }

    [TestMethod]
    public void SharedCatalogueRetainsIoFilteringWithoutModuleAudience()
    {
        var all = RunHostOk("list", "--json", "--include-disabled", "--root", _root);
        Assert.AreEqual(5, ParseList(all).Count);
        var text = RunHostOk("list", "--json", "--include-disabled", "--input", "text", "--output", "text", "--root", _root);
        CollectionAssert.AreEqual(new[] { "py_upper" }, Ids(text));
        text = RunHostOk("list", "--json", "--include-disabled", "--input", "text", "--root", _root);
        CollectionAssert.AreEquivalent(new[] { "py_upper", "md2html" }, Ids(text));
        foreach (var script in ParseList(all).Values)
        {
            Assert.IsFalse(script.TryGetProperty("consumers", out _));
        }
    }

    [TestMethod]
    public void RemovedModuleFilterIsRejectedInsteadOfSilentlyFilteringScripts()
    {
        var (exit, _, error) = RunHost("list", "--json", "--consumer", "color-picker", "--root", _root);
        Assert.AreEqual(1, exit);
        StringAssert.Contains(error, "unknown option '--consumer'");
    }

    [TestMethod]
    public void RemovedScriptSettingsCliIsNotAvailable()
    {
        var (exit, _, error) = RunHost("script-settings", "get", "--root", _root);
        Assert.AreNotEqual(0, exit);
        StringAssert.Contains(error, "Unknown command");
    }

    [TestMethod]
    public void List_Json_ReturnsScripts_WithResolvedIo()
    {
        var (exit, stdout, _) = RunHost("list", "--json", "--include-disabled", "--root", _root);
        Assert.AreEqual(0, exit);

        var byId = ParseList(stdout);
        Assert.AreEqual(5, byId.Count, "expected all five temp scripts to be listed");

        AssertIo(byId, "act_hello", "none", "text");
        AssertIo(byId, "py_upper", "text", "text");
        AssertIo(byId, "py_named", "files", "text");
        AssertIo(byId, "md2html", "text", "files");
        AssertIo(byId, "wc", "files", "none");

        // The system/file "kind" is gone from the contract; consumers derive it from io.input.
        Assert.IsFalse(byId["act_hello"].TryGetProperty("kind", out _), "the 'kind' field must no longer be emitted");
        Assert.IsFalse(byId["act_hello"].TryGetProperty("surfaces", out _), "consumers must filter on the 'io' block");
        Assert.IsFalse(byId["act_hello"].TryGetProperty("promptForParameters", out _), "the Host must not project prompt ownership");
        var parameter = byId["act_hello"].GetProperty("parameters")[0];
        Assert.AreEqual("format", parameter.GetProperty("name").GetString());
        Assert.AreEqual("choice", parameter.GetProperty("type").GetString());
        Assert.IsTrue(parameter.GetProperty("isRequired").GetBoolean());
        Assert.AreEqual("Output format", parameter.GetProperty("description").GetString());
        Assert.AreEqual("short", parameter.GetProperty("default").GetString());
        CollectionAssert.AreEqual(
            new[] { "short", "long" },
            parameter.GetProperty("options").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.AreEqual("Python", byId["py_upper"].GetProperty("runtime").GetString());
        Assert.IsFalse(byId["py_upper"].TryGetProperty("transform", out _));
        Assert.IsFalse(byId["act_hello"].TryGetProperty("mxc", out _));
        Assert.IsFalse(byId["act_hello"].TryGetProperty("hasLocalSettings", out _));
        Assert.AreEqual("string", byId["py_upper"].GetProperty("inputSchema").GetProperty("properties").GetProperty("text").GetProperty("type").GetString());
        Assert.AreEqual("array", byId["py_named"].GetProperty("inputSchema").GetProperty("properties").GetProperty("files").GetProperty("type").GetString());
    }

    [DataTestMethod]
    [DataRow(null, false)]
    [DataRow("Disabled", false)]
    [DataRow("Windows", true)]
    [DataRow("Wsl", true)]
    [DoNotParallelize]
    public void Discovery_RespectsPythonMode_WhileConfigurationCanListAll(string? mode, bool pythonEnabled)
    {
        Write("py_files.py", "def inspect_files(files):\n    pass\n");
        Write("py_files.py.tool.json", "{\"name\": \"py_files\", \"description\": \"py_files\", \"inputSchema\": {\"type\": \"object\", \"properties\": {\"files\": {\"type\": \"array\", \"items\": {\"type\": \"string\", \"x-powerscript\": {\"extensions\": [\".md\"]}}, \"minItems\": 1}}, \"required\": [\"files\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {}, \"required\": [], \"additionalProperties\": false}}");
        var previousStore = SettingsStore.Current;
        try
        {
            SettingsStore.Current = new FileSettingsStore(Path.Combine(_root, "settings"));
            PowerScriptsPaths.SaveEnabled(true);
            if (mode is not null)
            {
                new PythonSettings { Mode = Enum.Parse<PythonRuntimeMode>(mode) }.Save();
            }

            var expected = pythonEnabled
                ? new[] { "act_hello", "md2html", "wc", "py_upper", "py_named", "py_files" }
                : new[] { "act_hello", "md2html", "wc" };
            var (exit, json) = RunHostInProcess("list", "--json", "--root", _root);
            Assert.AreEqual(0, exit);
            CollectionAssert.AreEquivalent(expected, Ids(json));

            (exit, json) = RunHostInProcess("list", "--json", "--input", "text", "--root", _root);
            Assert.AreEqual(0, exit);
            CollectionAssert.AreEquivalent(
                pythonEnabled ? new[] { "md2html", "py_upper" } : new[] { "md2html" },
                Ids(json));

            var (textExit, text) = RunHostInProcess("list", "--root", _root);
            Assert.AreEqual(0, textExit);
            StringAssert.Contains(text, "act_hello");
            if (pythonEnabled)
            {
                StringAssert.Contains(text, "py_upper");
            }
            else
            {
                Assert.IsFalse(text.Contains("py_", StringComparison.Ordinal));
            }

            (exit, json) = RunHostInProcess("list", "--json", "--include-disabled", "--root", _root);
            Assert.AreEqual(0, exit);
            CollectionAssert.AreEquivalent(
                new[] { "act_hello", "md2html", "wc", "py_upper", "py_named", "py_files" },
                Ids(json));

        }
        finally
        {
            SettingsStore.Current = previousStore;
        }
    }

    [TestMethod]
    public void Catalogue_ProjectsResolvedIcons_ToJsonClient()
    {
        var icon = Path.Combine(_root, "script icon.ico");
        File.WriteAllBytes(icon, Convert.FromBase64String(
            "AAABAAEAAQEAAAEAIAAwAAAAFgAAACgAAAABAAAAAgAAAAEAIAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAD///8AAAAAAA=="));
        Write("wc.ps1.tool.json", "{\"name\": \"wc\", \"description\": \"wc\", \"x-powerscript\": {\"icon\": \"script icon.ico\"}, \"inputSchema\": {\"type\": \"object\", \"properties\": {\"files\": {\"type\": \"array\", \"items\": {\"type\": \"string\", \"x-powerscript\": {\"extensions\": [\".md\"]}}, \"minItems\": 1}}, \"required\": [\"files\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {}, \"required\": [], \"additionalProperties\": false}}");
        var json = RunHostOk("list", "--json", "--root", _root);
        Assert.AreEqual(icon, ParseList(json)["wc"].GetProperty("iconPath").GetString());
        Assert.AreEqual(icon, PowerScriptInfo.ParseList(json).Single(script => script.Id == "wc").IconPath);
        Assert.IsNull(PowerScriptInfo.ParseList(json).Single(script => script.Id == "act_hello").IconPath);

        File.Delete(icon);
        json = RunHostOk("list", "--json", "--root", _root);
        var missing = PowerScriptInfo.ParseList(json).Single(script => script.Id == "wc");
        Assert.IsNull(missing.IconPath);
        Assert.IsFalse(string.IsNullOrEmpty(missing.IconWarning));
    }

    [TestMethod]
    public void Host_Bundles_Pinned_Mxc_Without_User_Configuration()
    {
        var hostDirectory = Path.GetDirectoryName(_hostPath)!;
        var moduleDirectory = new DirectoryInfo(hostDirectory);
        while (moduleDirectory is not null && !File.Exists(Path.Combine(moduleDirectory.FullName, "Mxc.Runtime.targets")))
        {
            moduleDirectory = moduleDirectory.Parent;
        }

        Assert.IsNotNull(moduleDirectory, "The Host must be built from the PowerScripts project.");
        var metadata = XDocument.Load(Path.Combine(moduleDirectory.FullName, "Mxc.Runtime.targets"));
        var version = metadata.Root!.Element("PropertyGroup")!.Element("MxcVersion")!.Value;
        using var hostStream = File.OpenRead(_hostPath);
        using var hostImage = new PEReader(hostStream);
        var architecture = hostImage.PEHeaders.CoffHeader.Machine == Machine.Arm64 ? "arm64" : "x64";
        var runtimeDirectory = Path.Combine(moduleDirectory.FullName, "PowerScripts.Host", "obj", "mxc", version, architecture);
        foreach (var name in new[] { "wxc-exec.exe", "plm.exe" })
        {
            var deployed = Path.Combine(hostDirectory, name);
            Assert.IsTrue(File.Exists(deployed), $"The Host is missing its automatic MXC dependency: {name}");
            CollectionAssert.AreEqual(
                SHA256.HashData(File.ReadAllBytes(Path.Combine(runtimeDirectory, name))),
                SHA256.HashData(File.ReadAllBytes(deployed)),
                $"The bundled {name} differs from the pinned runtime, possibly due to stale output timestamps.");
            using var stream = File.OpenRead(deployed);
            using var image = new PEReader(stream);
            Assert.AreEqual(hostImage.PEHeaders.CoffHeader.Machine, image.PEHeaders.CoffHeader.Machine);
        }

        Assert.IsTrue(File.Exists(Path.Combine(hostDirectory, "Mxc.LICENSE.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(hostDirectory, "Mxc.spdx.json")));
    }

    [TestMethod]
    public void MxcSupport_Json_ReturnsPlatformStatus()
    {
        var (exit, stdout, _) = RunHost("mxc-support", "--json");
        Assert.AreEqual(0, exit);

        using var document = JsonDocument.Parse(stdout);
        var root = document.RootElement;
        Assert.IsTrue(root.GetProperty("isSupported").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.AreEqual(JsonValueKind.String, root.GetProperty("reason").ValueKind);
        Assert.AreEqual(JsonValueKind.Number, root.GetProperty("windowsBuild").ValueKind);
        Assert.AreEqual(JsonValueKind.Array, root.GetProperty("warnings").ValueKind);
    }

    [TestMethod]
    public void List_NoInput_ReturnsOnlyActions()
    {
        var ids = Ids(RunHostOk("list", "--json", "--no-input", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "act_hello" }, ids);
    }

    [TestMethod]
    public void List_InputText_FiltersToTextConsumers()
    {
        var ids = Ids(RunHostOk("list", "--json", "--include-disabled", "--input", "text", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "py_upper", "md2html" }, ids);
    }

    [TestMethod]
    public void List_OutputFiles_FiltersToFileProducers()
    {
        var ids = Ids(RunHostOk("list", "--json", "--output", "files", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "md2html" }, ids);
    }

    [TestMethod]
    public void List_InputFiles_FiltersToFileScripts()
    {
        var ids = Ids(RunHostOk("list", "--json", "--include-disabled", "--input", "files", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "wc", "py_named" }, ids);
    }

    [TestMethod]
    public void ListUsesSchemaIoWithoutAnEntryFunction()
    {
        var byId = ParseList(RunHostOk("list", "--json", "--include-disabled", "--root", _root));
        AssertIo(byId, "py_named", "files", "text");
    }

    [TestMethod]
    public void List_ComposedFilters_Intersect()
    {
        // input text AND output text -> only the python transform, not the file producer.
        var ids = Ids(RunHostOk("list", "--json", "--include-disabled", "--input", "text", "--output", "text", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "py_upper" }, ids);
    }

    [DataTestMethod]
    [DataRow("bogus")]
    [DataRow("html")]
    [DataRow("video")]
    public void List_UnsupportedInputFilter_FailsWithUsageError(string input)
    {
        var (exit, _, stderr) = RunHost("list", "--json", "--input", input, "--root", _root);
        Assert.AreEqual(1, exit, "an unknown --input token must be a usage failure");
        StringAssert.Contains(stderr, "unknown --input");
        StringAssert.Contains(stderr, "none, text, files");
    }

    [TestMethod]
    [DataRow("image")]
    [DataRow("audio")]
    public void MediaDiscoveryFiltersUseTheDeclaredSchemas(string category)
    {
        var created = PowerScripts.Core.Templates.ScriptTemplates.Create(_root,
            new PowerScripts.Core.Templates.ScriptTemplateOptions("Media discovery", Input: category, Output: category));
        var manifest = ToolDescriptorParser.TryParseFile(created.EntryPath + ".tool.json")!;
        var input = PowerScriptInfo.ParseList(RunHostOk("list", "--json", "--input", category, "--root", _root));
        Assert.AreEqual(1, input.Count);
        Assert.AreEqual(manifest.Id, input[0].Id);
        var output = PowerScriptInfo.ParseList(RunHostOk("list", "--json", "--output", category, "--root", _root));
        Assert.AreEqual(manifest.Id, output.Single().Id);
    }

    [DataTestMethod]
    [DataRow("bogus")]
    [DataRow("html")]
    [DataRow("video")]
    public void List_UnsupportedOutputFilter_FailsWithUsageError(string output)
    {
        var (exit, _, stderr) = RunHost("list", "--json", "--output", output, "--root", _root);
        Assert.AreEqual(1, exit);
        StringAssert.Contains(stderr, "unknown --output");
        StringAssert.Contains(stderr, "none, text, files");
    }

    [DataTestMethod]
    [DataRow("a-path.png")]
    [DataRow("{\"image_path\":\"a-path.png\"}")]
    [DataRow("{\"text\":\"a-path.png\"}")]
    [DataRow("{\"text\":\"mixed\",\"file_paths\":[]}")]
    [DataRow("{\"file_paths\":[null]}")]
    public void Transform_FileOutputRejectsLegacyAndMismatchedPayloads(string payload)
    {
        Write("bad-output.ps1", "Write-Output '" + payload + "'");
        Write("bad-output.ps1.tool.json", "{\"name\": \"bad-output\", \"description\": \"bad-output\", \"inputSchema\": {\"type\": \"object\", \"properties\": {}, \"required\": [], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {\"files\": {\"type\": \"array\", \"items\": {\"type\": \"string\"}}}, \"required\": [\"files\"], \"additionalProperties\": false}}");
        var client = new PowerScriptsClient(_hostPath, _root);
        try
        {
            Assert.IsTrue(client.TrustApprove("bad-output"));
            var result = client.TransformAsync("bad-output", new PowerScriptTransformInput(), noConsent: true)
                .GetAwaiter().GetResult();
            Assert.AreEqual(PowerScriptsProtocol.ExitCode.Error, result.ExitCode);
            StringAssert.Contains(result.StdErr, "Invalid script output");
            Assert.IsNull(result.Text);
            Assert.IsNull(result.FilePaths);
        }
        finally
        {
            client.TrustRevoke("bad-output");
        }
    }

    [TestMethod]
    public void Transform_NoneReturnsNoDataAndPreservesActionDiagnostics()
    {
        Write("no-output.ps1", "Write-Output 'action completed'");
        Write("no-output.ps1.tool.json", "{\"name\": \"no-output\", \"description\": \"no-output\", \"inputSchema\": {\"type\": \"object\", \"properties\": {}, \"required\": [], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {}, \"required\": [], \"additionalProperties\": false}}");
        var client = new PowerScriptsClient(_hostPath, _root);
        try
        {
            Assert.IsTrue(client.TrustApprove("no-output"));
            var result = client.TransformAsync("no-output", new PowerScriptTransformInput(), noConsent: true)
                .GetAwaiter().GetResult();
            Assert.IsTrue(result.Succeeded, result.StdErr);
            Assert.IsNull(result.Text);
            Assert.IsNull(result.FilePaths);
            StringAssert.Contains(result.StdErr, "action completed");
        }
        finally
        {
            client.TrustRevoke("no-output");
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Transform_Utf8TextPreservesUnicode(bool escapeUnicode)
    {
        Write("unicode-text.ps1", """
            param([string]$Text)
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            Write-Output $Text
            """);
        Write("unicode-text.ps1.tool.json", "{\"name\": \"unicode-text\", \"description\": \"unicode-text\", \"inputSchema\": {\"type\": \"object\", \"properties\": {\"text\": {\"type\": \"string\"}}, \"required\": [\"text\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {\"text\": {\"type\": \"string\"}}, \"required\": [\"text\"], \"additionalProperties\": false}}");
        const string text = "\u6D4B\u8BD5 caf\u00E9 \U0001F600";
        var payload = JsonSerializer.Serialize(new { text }, new JsonSerializerOptions
        {
            Encoder = escapeUnicode ? JavaScriptEncoder.Default : JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var client = new PowerScriptsClient(_hostPath, _root);
        try
        {
            Assert.IsTrue(client.TrustApprove("unicode-text"));
            var (exit, stdout, stderr) = RunHostWithInput(payload, "transform", "unicode-text", "--no-consent", "--root", _root);
            Assert.AreEqual(0, exit, stderr);
            using var result = JsonDocument.Parse(stdout);
            Assert.AreEqual(text, result.RootElement.GetProperty("text").GetString()!.TrimEnd('\r', '\n'));
        }
        finally
        {
            client.TrustRevoke("unicode-text");
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Transform_Utf8FilePathsRemainReadable(bool escapeUnicode)
    {
        Write("unicode-files.ps1", """
            param([string[]]$Files)
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            [IO.File]::ReadAllText($Files[0])
            """);
        Write("unicode-files.ps1.tool.json", "{\"name\": \"unicode-files\", \"description\": \"unicode-files\", \"inputSchema\": {\"type\": \"object\", \"properties\": {\"files\": {\"type\": \"array\", \"items\": {\"type\": \"string\", \"x-powerscript\": {\"extensions\": [\".txt\"]}}, \"minItems\": 1}}, \"required\": [\"files\"], \"additionalProperties\": false}, \"outputSchema\": {\"type\": \"object\", \"properties\": {\"text\": {\"type\": \"string\"}}, \"required\": [\"text\"], \"additionalProperties\": false}}");
        var path = Path.Combine(_root, "input \u6D4B\u8BD5 \U0001F600.txt");
        const string content = "selected Unicode file";
        File.WriteAllText(path, content);
        var payload = JsonSerializer.Serialize(new { files = new[] { path } }, new JsonSerializerOptions
        {
            Encoder = escapeUnicode ? JavaScriptEncoder.Default : JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var client = new PowerScriptsClient(_hostPath, _root);
        try
        {
            Assert.IsTrue(client.TrustApprove("unicode-files"));
            var (exit, stdout, stderr) = RunHostWithInput(payload, "transform", "unicode-files", "--no-consent", "--root", _root);
            Assert.AreEqual(0, exit, stderr);
            using var result = JsonDocument.Parse(stdout);
            Assert.AreEqual(content, result.RootElement.GetProperty("text").GetString()!.TrimEnd('\r', '\n'));
        }
        finally
        {
            client.TrustRevoke("unicode-files");
        }
    }

    [TestMethod]
    public void List_Text_ShowsIoColumn()
    {
        var (exit, stdout, _) = RunHost("list", "--root", _root);
        Assert.AreEqual(0, exit);
        StringAssert.Contains(stdout, "none->text");
        StringAssert.Contains(stdout, "files->none");
    }

    // ---- helpers ----

    private void Write(string name, string body) => File.WriteAllText(Path.Combine(_root, name), body);

    private (int Exit, string StdOut, string StdErr) RunHost(params string[] args) => RunHostWithInput(null, args);

    private (int Exit, string StdOut, string StdErr) RunHostWithInput(string? stdin, params string[] args)
    {
        var psi = new ProcessStartInfo(_hostPath)
        {
            RedirectStandardInput = stdin is not null,
            StandardInputEncoding = stdin is null ? null : new UTF8Encoding(false),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        psi.Environment[ModuleState.IgnoreEnabledEnvironmentVariable] = "1";

        using var p = Process.Start(psi)!;
        if (stdin is not null)
        {
            p.StandardInput.Write(stdin);
            p.StandardInput.Close();
        }

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(15000);
        return (p.ExitCode, stdout, stderr);
    }

    private string RunHostOk(params string[] args)
    {
        var (exit, stdout, stderr) = RunHost(args);
        Assert.AreEqual(0, exit, $"host exited {exit}: {stderr}");
        return stdout;
    }

    private static (int Exit, string StdOut) RunHostInProcess(params string[] args)
    {
        // Keep runtime-policy tests hermetic without adding a production override for security settings.
        var entryPoint = Assembly.LoadFrom(Path.ChangeExtension(_hostPath, ".dll")).EntryPoint;
        Assert.IsNotNull(entryPoint);
        var previousOutput = Console.Out;
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            var exit = (int)entryPoint.Invoke(null, new object[] { args })!;
            return (exit, output.ToString());
        }
        finally
        {
            Console.SetOut(previousOutput);
        }
    }

    private static Dictionary<string, JsonElement> ParseList(string json)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            map[el.GetProperty("id").GetString()!] = el.Clone();
        }

        return map;
    }

    private static string[] Ids(string json) => ParseList(json).Keys.ToArray();

    private static void AssertIo(Dictionary<string, JsonElement> byId, string id, string input, string output)
    {
        Assert.IsTrue(byId.ContainsKey(id), $"missing script '{id}'");
        var io = byId[id].GetProperty("io");
        Assert.AreEqual(input, io.GetProperty("input").GetString(), $"{id} input");
        Assert.AreEqual(output, io.GetProperty("output").GetString(), $"{id} output");
    }
}
