// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SettingsSearchEvaluation.Tests;

[TestClass]
public class EvaluatorTests
{
    [TestMethod]
    public async Task RunAsync_BasicEngine_ReturnsExpectedMetricsForExactSingleEntry()
    {
        const string json = """
[
  {
    "type": 0,
    "header": "Fancy Zones",
    "pageTypeName": "FancyZonesPage",
    "elementName": "",
    "elementUid": "FancyZones",
    "parentElementName": "",
    "description": "",
    "icon": null
  }
]
""";

        var (entries, diagnostics) = EvaluationDataLoader.LoadEntriesFromJson(json);
        var cases = new[]
        {
            new EvaluationCase
            {
                Query = "Fancy Zones",
                ExpectedIds = new[] { "FancyZones" },
                Notes = "Exact query should be rank 1.",
            },
        };

        var options = new RunnerOptions
        {
            IndexJsonPath = "test-index.json",
            CasesJsonPath = null,
            Engines = new[] { SearchEngineKind.Basic },
            MaxResults = 5,
            TopK = 5,
            Iterations = 1,
            WarmupIterations = 0,
            SemanticIndexTimeout = TimeSpan.FromSeconds(1),
            OutputJsonPath = null,
        };

        var report = await Evaluator.RunAsync(options, entries, diagnostics, cases);

        Assert.AreEqual(1, report.Engines.Count);
        var engine = report.Engines[0];
        Assert.AreEqual(SearchEngineKind.Basic, engine.Engine);
        Assert.IsTrue(engine.IsAvailable);
        Assert.AreEqual(1, engine.QueryCount);
        Assert.AreEqual(1.0, engine.RecallAtK, 0.0001);
        Assert.AreEqual(1.0, engine.Mrr, 0.0001);
        Assert.AreEqual(1, engine.CaseResults.Count);
        Assert.IsTrue(engine.CaseResults[0].HitAtK);
        Assert.AreEqual(1, engine.CaseResults[0].BestRank);
    }

    [TestMethod]
    public async Task RunAsync_SemanticEngine_ReturnsReportWithoutThrowing()
    {
        const string json = """
[
  {
    "type": 0,
    "header": "Fancy Zones",
    "pageTypeName": "FancyZonesPage",
    "elementName": "",
    "elementUid": "FancyZones",
    "parentElementName": "",
    "description": "",
    "icon": null
  }
]
""";

        var (entries, diagnostics) = EvaluationDataLoader.LoadEntriesFromJson(json);
        var cases = new[]
        {
            new EvaluationCase
            {
                Query = "Fancy Zones",
                ExpectedIds = new[] { "FancyZones" },
                Notes = "Semantic smoke test.",
            },
        };

        var options = new RunnerOptions
        {
            IndexJsonPath = "test-index.json",
            CasesJsonPath = null,
            Engines = new[] { SearchEngineKind.Semantic },
            MaxResults = 5,
            TopK = 5,
            Iterations = 1,
            WarmupIterations = 0,
            SemanticIndexTimeout = TimeSpan.FromSeconds(3),
            OutputJsonPath = null,
        };

        var report = await Evaluator.RunAsync(options, entries, diagnostics, cases);

        Assert.AreEqual(1, report.Engines.Count);
        var engine = report.Engines[0];
        Assert.AreEqual(SearchEngineKind.Semantic, engine.Engine);

        if (!engine.IsAvailable)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(engine.AvailabilityError));
            Assert.AreEqual(0, engine.QueryCount);
        }
        else
        {
            Assert.AreEqual(1, engine.QueryCount);
            Assert.AreEqual(1, engine.CaseResults.Count);
        }
    }
}
