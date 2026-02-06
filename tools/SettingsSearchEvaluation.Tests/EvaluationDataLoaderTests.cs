// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SettingsSearchEvaluation.Tests;

[TestClass]
public class EvaluationDataLoaderTests
{
    [TestMethod]
    public void LoadEntriesFromJson_NormalizesHeaderAndDetectsDuplicates()
    {
        const string json = """
[
  {
    "type": 0,
    "header": null,
    "pageTypeName": "ColorPickerPage",
    "elementName": "",
    "elementUid": "Activation_Shortcut",
    "parentElementName": "",
    "description": null,
    "icon": null
  },
  {
    "type": 0,
    "header": null,
    "pageTypeName": "FancyZonesPage",
    "elementName": "",
    "elementUid": "Activation_Shortcut",
    "parentElementName": "",
    "description": null,
    "icon": null
  }
]
""";

        var (entries, diagnostics) = EvaluationDataLoader.LoadEntriesFromJson(json);

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual("Activation Shortcut", entries[0].Header);
        Assert.AreEqual(1, diagnostics.DuplicateIdBucketCount);
        Assert.IsTrue(diagnostics.DuplicateIdCounts.TryGetValue("Activation_Shortcut", out var count));
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void LoadCases_GeneratesFallbackCases_WhenNoCasesFileSpecified()
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

        var (entries, _) = EvaluationDataLoader.LoadEntriesFromJson(json);
        var cases = EvaluationDataLoader.LoadCases(null, entries);

        Assert.AreEqual(1, cases.Count);
        Assert.AreEqual("Fancy Zones", cases[0].Query);
        Assert.AreEqual("FancyZones", cases[0].ExpectedIds[0]);
    }

    [TestMethod]
    public void LoadCases_LoadsAndNormalizesCasesFile()
    {
        const string entriesJson = """
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

        const string casesJson = """
[
  {
    "query": "  fancy zones  ",
    "expectedIds": [ "FancyZones", " fancyzones ", "" ],
    "notes": "normalization test"
  },
  {
    "query": "",
    "expectedIds": [ "FancyZones" ]
  },
  {
    "query": "missing expected",
    "expectedIds": [ "" ]
  }
]
""";

        var (entries, _) = EvaluationDataLoader.LoadEntriesFromJson(entriesJson);
        var casesFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(casesFile, casesJson);
            var cases = EvaluationDataLoader.LoadCases(casesFile, entries);

            Assert.AreEqual(1, cases.Count);
            Assert.AreEqual("fancy zones", cases[0].Query);
            Assert.AreEqual(1, cases[0].ExpectedIds.Count);
            Assert.AreEqual("FancyZones", cases[0].ExpectedIds[0]);
            Assert.AreEqual("normalization test", cases[0].Notes);
        }
        finally
        {
            File.Delete(casesFile);
        }
    }
}
