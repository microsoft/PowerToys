// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Templates;
using PowerScripts.Core.Templates.CliModules;
using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class CliCatalogueTests
{
    [TestMethod]
    public void ReorganizationPreservesSavedContractsGeneratedBodiesAndHostPermissions()
    {
        using var stream = typeof(CliCatalogueTests).Assembly.GetManifestResourceStream(
            "PowerScripts.Core.Tests.CliCatalogueCompatibility.json")!;
        using var baseline = JsonDocument.Parse(stream);
        foreach (var item in baseline.RootElement.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString()!;
            var recipe = PowerShellCliTemplates.Get(id);
            var options = PowerShellCliTemplates.Configure(new("Compatibility", Input: recipe.Input, Output: recipe.Output, CliContent: id));
            var metadata = JsonSerializer.SerializeToNode(recipe, ToolDescriptorParser.JsonOptions)!;
            metadata.AsObject().Remove("iconName");
            // The old module-wide batch hint was also set on a query with no files input.
            if (recipe.Input != "files")
            {
                metadata["supportsFileBatch"] = false;
            }

            var payload = metadata.ToJsonString(ToolDescriptorParser.JsonOptions);
            foreach (var hostExecution in new[] { false, true })
            {
                payload += "\n" + PowerShellCliTemplates.CreateScript(options, hostExecution, @"C:\PowerToys-Compatibility");
            }

            payload += "\n" + string.Join(",", PowerShellCliTemplates.HostCapabilities(id));
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.Replace("\r\n", "\n", StringComparison.Ordinal))));
            Assert.AreEqual(item.GetProperty("sha256").GetString(), fingerprint,
                $"{id}: reviewed argument schemas, generated behavior or Host permissions changed.");
        }
    }

    [TestMethod]
    public void ModuleDefinitionsAreDiscoveredWithoutARegistrationOrMetadataSwitch()
    {
        var definitions = typeof(PowerShellCliTemplates).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(PowerShellCliModule).IsAssignableFrom(type)).ToArray();
        CollectionAssert.AreEquivalent(definitions, PowerShellCliTemplates.Modules.Select(module => module.GetType()).ToArray());
        var catalogue = PowerShellCliTemplates.CreateCatalogue([new ExampleCliModule()]);
        var recipe = catalogue.Single();
        Assert.AreEqual("example", recipe.ModuleId);
        Assert.AreEqual("Example module", recipe.Group);
        Assert.AreEqual("ExampleIcon", recipe.IconName);
        Assert.IsTrue(recipe.SupportsFileBatch);
        Assert.AreEqual("integer", recipe.Parameters.Single(parameter => parameter.Name == "Count").Schema.Type);
        Assert.AreEqual("boolean", recipe.Parameters.Single(parameter => parameter.Name == "Json").Schema.Type);
        var wire = JsonSerializer.SerializeToElement(recipe, ToolDescriptorParser.JsonOptions);
        Assert.IsFalse(wire.TryGetProperty("commandSynopsis", out _), "Documentation internals must not alter discovery's wire contract.");
    }

    [DataTestMethod]
    [DataRow("duplicate-module")]
    [DataRow("duplicate-recipe")]
    [DataRow("missing-documentation")]
    [DataRow("unhandled-file-output")]
    [DataRow("invalid-batch")]
    public void InvalidDefinitionsFailBeforeEnteringTheCatalogue(string problem)
    {
        var module = new ExampleCliModule(problem);
        Assert.ThrowsException<InvalidOperationException>(() => PowerShellCliTemplates.CreateCatalogue(
            problem == "duplicate-module" ? [module, module] : [module]));
    }

    [TestMethod]
    public void CheckedInScenarioTableMatchesTheCompiledCatalogue()
    {
        using var stream = typeof(CliCatalogueTests).Assembly.GetManifestResourceStream("CliTemplateScenarioMatrix.md")!;
        using var reader = new StreamReader(stream);
        var document = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        const string begin = "<!-- BEGIN GENERATED CLI SCENARIOS -->\n";
        const string end = "<!-- END GENERATED CLI SCENARIOS -->";
        var start = document.IndexOf(begin, StringComparison.Ordinal);
        var finish = document.IndexOf(end, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0 && finish > start, "The matrix requires generated-section markers.");
        start += begin.Length;
        Assert.AreEqual(PowerShellCliTemplates.CreateScenarioMatrix(), document[start..finish],
            "Build the Host, run Update-CliTemplateMatrix.ps1, then rebuild the tests.");
    }

    [TestMethod]
    public void TypedArgumentHelpersPreserveValuesWithoutShellInterpolation()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PowerShell\7\pwsh.exe");
        if (!OperatingSystem.IsWindows() || !File.Exists(executable))
        {
            Assert.Inconclusive("The generated argument fixture requires Windows and PowerShell 7.");
        }

        const string value = "spaces ' quotes; $(throw 'must not run') & |";
        var recipe = new ExampleCliModule().CreateRecipes().Single();
        var body = """
            $ErrorActionPreference = 'Stop'
            $Label = $env:CLI_ARGUMENT_TEST_VALUE; $Count = 7; $Json = $true
            $Files = @('C:\fixtures\one file.txt', 'C:\fixtures\two.txt')
            $__powerscriptArguments = [Collections.Generic.List[string]]::new()
            """ + "\n" + recipe.Arguments + "\nConvertTo-Json -Compress -InputObject ($__powerscriptArguments.ToArray())";
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(body)));
        start.Environment["CLI_ARGUMENT_TEST_VALUE"] = value;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(30_000), "Argument fixture timed out.");
        Assert.AreEqual(0, process.ExitCode, stderr.GetAwaiter().GetResult());
        var actual = JsonSerializer.Deserialize<string[]>(stdout.GetAwaiter().GetResult())!;
        CollectionAssert.AreEqual(new[] { "inspect", "--label", value, "--count", "7", "--json", "--", @"C:\fixtures\one file.txt", @"C:\fixtures\two.txt" }, actual);
        Assert.ThrowsException<ArgumentException>(() => CliArgument.Switch("--bad", Text("Bad", "Not boolean.")));
        Assert.ThrowsException<ArgumentException>(() => CliArgument.Option("--bad", Flag("Bad", "Not a value option.")));
    }

    private sealed class ExampleCliModule(string? problem = null) : PowerShellCliModule
    {
        public override string Id => "example";

        public override string Name => "Example module";

        public override string IconName => "ExampleIcon";

        public override string Executable => @"WinUI3Apps\Example.Cli.exe";

        public override int Order => 90;

        public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes()
        {
            var recipe = Describe(Query("example-inspect", "Inspect selected files", "Return a report without modifying files.", "files",
                CliArgument.Literal("inspect"),
                CliArgument.Option("--label", Text("Label", "Report label.")),
                CliArgument.Option("--count", Integer("Count", "Maximum results.", 10, 1, 100)),
                CliArgument.Switch("--json", Flag("Json", "Structured report.", true)),
                CliArgument.Literal("--"),
                CliArgument.InputFiles()) with
            { SupportsFileBatch = true }, "inspect [--label] [--count] [--json] -- <files...>");
            recipe = problem switch
            {
                "missing-documentation" => recipe with { CommandSynopsis = string.Empty },
                "unhandled-file-output" => recipe with { Output = "files" },
                "invalid-batch" => recipe with { Input = "none" },
                _ => recipe,
            };
            return problem == "duplicate-recipe" ? [recipe, recipe] : [recipe];
        }
    }
}
