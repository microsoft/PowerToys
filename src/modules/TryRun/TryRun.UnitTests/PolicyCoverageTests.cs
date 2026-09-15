// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class PolicyCoverageTests
{
    private static readonly string[] Statuses = ["mapped", "fixed", "gap", "backend-gap", "structural", "metadata", "testing-only"];

    [TestMethod]
    public void EveryNativeSchemaFieldAndBackendHasAnExplicitCoverageClassification()
    {
        using var coverage = ReadCoverage();
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "mxc-contract.schema.json");
        Assert.IsTrue(File.Exists(schemaPath), "Build TryRun.slnx with MxcRoot to include the matching native contract.");
        var bytes = File.ReadAllBytes(schemaPath);
        var root = coverage.RootElement;
        Assert.AreEqual(root.GetProperty("revision").GetString(), File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "mxc-version.txt")).Trim());
        Assert.AreEqual(root.GetProperty("schemaSha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)), "The native schema changed. Review the coverage matrix before claiming support.");
        using var schema = JsonDocument.Parse(bytes);
        var definitions = schema.RootElement.GetProperty("definitions");
        var expected = definitions.EnumerateObject().SelectMany(definition => Properties(definition.Value).Select(property => definition.Name + "." + property)).Distinct().ToArray();
        var rows = root.GetProperty("fields").EnumerateArray().ToArray();
        var actual = rows.Select(row => row.GetProperty("native").GetString()!).ToArray();
        Assert.AreEqual(actual.Length, actual.Distinct().Count(), "Duplicate native field classification.");
        CollectionAssert.AreEquivalent(expected, actual, "Every native field needs an explicit classification, including gaps and metadata.");
        foreach (var row in rows)
        {
            var status = row.GetProperty("status").GetString();
            CollectionAssert.Contains(Statuses, status);
            Assert.IsFalse(string.IsNullOrWhiteSpace(row.GetProperty("note").GetString()));
            Assert.AreEqual(status == "mapped", row.GetProperty("controls").GetArrayLength() > 0);
        }

        var backends = definitions.GetProperty("OneShotContainment").GetProperty("oneOf").EnumerateArray().SelectMany(variant => variant.GetProperty("enum").EnumerateArray().Select(value => value.GetString()!)).ToArray();
        CollectionAssert.AreEquivalent(backends, root.GetProperty("backends").EnumerateArray().Select(row => row.GetProperty("native").GetString()!).ToArray());
    }

    [TestMethod]
    public async Task EveryPolicyControlHasANativeDestinationAndEvidenceReferencesResolve()
    {
        using var coverage = ReadCoverage();
        var rows = coverage.RootElement.GetProperty("fields").EnumerateArray().ToArray();
        var controls = rows.SelectMany(row => row.GetProperty("controls").EnumerateArray().Select(value => value.GetString()!)).Distinct().ToArray();
        CollectionAssert.AreEquivalent(PolicySettings.Fields.Select(field => field.Key).ToArray(), controls.Where(control => !control.StartsWith('@')).ToArray());
        foreach (var reference in rows.SelectMany(row => row.GetProperty("evidence").EnumerateArray().Select(value => value.GetString()!)).Distinct())
        {
            var split = reference.LastIndexOf('.');
            Assert.IsTrue(split > 0);
            var type = typeof(PolicyCoverageTests).Assembly.GetType("PowerToys.TryRun.UnitTests." + reference[..split]);
            Assert.IsNotNull(type, reference);
            Assert.IsNotNull(type.GetMethod(reference[(split + 1)..]), reference);
        }

        await WorkflowTests.OnDispatcherAsync(() =>
        {
            var window = new MainWindow([]);
            try
            {
                foreach (var control in controls.Where(control => control.StartsWith('@')))
                {
                    Assert.IsNotNull(window.FindName(control[1..]), control);
                }
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }

    private static JsonDocument ReadCoverage() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "policy-coverage.json")));

    private static IEnumerable<string> Properties(JsonElement definition)
    {
        if (definition.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                yield return property.Name;
            }
        }

        foreach (var keyword in new[] { "oneOf", "anyOf", "allOf" })
        {
            if (definition.TryGetProperty(keyword, out var variants))
            {
                foreach (var variant in variants.EnumerateArray())
                {
                    foreach (var property in Properties(variant))
                    {
                        yield return property;
                    }
                }
            }
        }
    }
}
