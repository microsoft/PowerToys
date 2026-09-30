// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

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
                  "format": {
                    "type": "string",
                    "description": "Output format",
                    "enum": ["short", "long"],
                    "default": "short"
                  }
                },
                "required": ["format"]
              },
              "x-powerscript": {}
            }
            """);

        // A Python data transform: I/O read from the powerscript_from_* function name.  io = text -> text
        Write("py_upper.py", "def powerscript_from_text_to_text(text):\n    return text.upper()\n");
        WriteDescriptor("py_upper.py", string.Empty);

        // A Python transform with an explicitly-named entry function (not the naming convention): its
        // I/O comes from the descriptor's declared input/output, proving the "kind"/name is not needed
        // to type a script.  io = html -> text
        Write("py_named.py", "def shout(value):\n    return value.upper()\n");
        WriteDescriptor("py_named.py", "\"function\":\"shout\",\"input\":\"html\",\"output\":\"text\"");

        // A PowerShell data transform: no naming convention, so I/O is declared.  io = text -> html
        Write("md2html.ps1", "param([string]$Markdown)\nWrite-Output \"<p>$Markdown</p>\"");
        WriteDescriptor("md2html.ps1", "\"input\":\"text\",\"output\":\"html\"");

        // A file script: acts on selected files (file-driven because it declares extensions).  io = files -> none
        Write("wc.ps1", "param([string]$file)\n(Get-Content $file).Length");
        WriteDescriptor("wc.ps1", "\"extensions\":[\".md\"]");
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
    public void List_Json_ReturnsScripts_WithResolvedIo()
    {
        var (exit, stdout, _) = RunHost("list", "--json", "--root", _root);
        Assert.AreEqual(0, exit);

        var byId = ParseList(stdout);
        Assert.AreEqual(5, byId.Count, "expected all five temp scripts to be listed");

        AssertIo(byId, "act_hello", "none", "text");
        AssertIo(byId, "py_upper", "text", "text");
        AssertIo(byId, "py_named", "html", "text");
        AssertIo(byId, "md2html", "text", "html");
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
        CollectionAssert.AreEquivalent(
            new[] { "filesystem", "network", "ui", "leastPrivilege" },
            byId["act_hello"].GetProperty("mxc").GetProperty("recommendedPolicies")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray());
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
        var ids = Ids(RunHostOk("list", "--json", "--input", "text", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "py_upper", "md2html" }, ids);
    }

    [TestMethod]
    public void List_OutputHtml_FiltersToHtmlProducers()
    {
        var ids = Ids(RunHostOk("list", "--json", "--output", "html", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "md2html" }, ids);
    }

    [TestMethod]
    public void List_InputFiles_FiltersToFileScripts()
    {
        var ids = Ids(RunHostOk("list", "--json", "--input", "files", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "wc" }, ids);
    }

    [TestMethod]
    public void List_ExplicitEntryFunction_TypesFromDescriptor()
    {
        // py_named's function is 'shout' (not the powerscript_from_* convention); its I/O must come
        // from the descriptor, not the name.  This is the "declare function + I/O in the descriptor" path.
        var byId = ParseList(RunHostOk("list", "--json", "--root", _root));
        AssertIo(byId, "py_named", "html", "text");
    }

    [TestMethod]
    public void List_ComposedFilters_Intersect()
    {
        // input text AND output text -> only the python transform, not the text->html one.
        var ids = Ids(RunHostOk("list", "--json", "--input", "text", "--output", "text", "--root", _root));
        CollectionAssert.AreEquivalent(new[] { "py_upper" }, ids);
    }

    [TestMethod]
    public void List_UnknownFilterToken_FailsWithUsageError()
    {
        var (exit, _, stderr) = RunHost("list", "--json", "--input", "bogus", "--root", _root);
        Assert.AreEqual(1, exit, "an unknown --input token must be a usage failure");
        StringAssert.Contains(stderr, "unknown --input");
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

    private void WriteDescriptor(string scriptFile, string xPowerScriptBody)
    {
        var name = Path.GetFileNameWithoutExtension(scriptFile);
        var json =
            "{ \"name\":\"" + name + "\", \"description\":\"" + name + "\", " +
            "\"inputSchema\":{\"type\":\"object\",\"properties\":{}}, " +
            "\"x-powerscript\":{" + xPowerScriptBody + "} }";
        File.WriteAllText(Path.Combine(_root, scriptFile + ".tool.json"), json);
    }

    private (int Exit, string StdOut, string StdErr) RunHost(params string[] args)
    {
        var psi = new ProcessStartInfo(_hostPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
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
