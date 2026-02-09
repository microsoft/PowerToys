// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SettingsSearchEvaluation.Tests;

[TestClass]
public class EvaluationDataLoaderTests
{
    [TestMethod]
    public void LoadEntriesFromJson_UsesResourceStrings_WhenHeaderAndDescriptionAreMissing()
    {
        const string json = """
[
  {
    "type": 0,
    "header": null,
    "pageTypeName": "FancyZonesPage",
    "elementName": "",
    "elementUid": "FancyZones",
    "parentElementName": "",
    "description": null,
    "icon": null
  },
  {
    "type": 1,
    "header": null,
    "pageTypeName": "PowerRenamePage",
    "elementName": "PowerRenameToggle",
    "elementUid": "PowerRename_Toggle_Enable",
    "parentElementName": "",
    "description": null,
    "icon": null
  }
]
""";

        var resourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FancyZones.ModuleTitle"] = "FancyZones",
            ["FancyZones.ModuleDescription"] = "Create and manage zone layouts.",
            ["PowerRename_Toggle_Enable.Header"] = "PowerRename",
            ["PowerRename_Toggle_Enable.Description"] = "Enable bulk rename integration.",
        };

        var (entries, _) = EvaluationDataLoader.LoadEntriesFromJson(json, resourceMap);

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual("FancyZones", entries[0].Header);
        Assert.AreEqual("Create and manage zone layouts.", entries[0].Description);
        Assert.AreEqual("PowerRename", entries[1].Header);
        Assert.AreEqual("Enable bulk rename integration.", entries[1].Description);
    }

    [TestMethod]
    public void WriteNormalizedTextCorpusFile_EmitsOnlyNormalizedText()
    {
        var entries = new[]
        {
            new SettingEntry(
                EntryType.SettingsCard,
                "Activation shortcut",
                PageTypeName: "MeasureToolPage",
                ElementName: "MeasureToolActivationShortcut",
                ElementUid: "MeasureTool_ActivationShortcut",
                ParentElementName: string.Empty,
                Description: "Customize the shortcut to bring up the command bar",
                Icon: string.Empty),
        };

        var outputPath = Path.GetTempFileName();
        try
        {
            EvaluationDataLoader.WriteNormalizedTextCorpusFile(outputPath, entries);
            var line = File.ReadAllLines(outputPath).Single();

            Assert.AreEqual(
                "activation shortcut customize the shortcut to bring up the command bar",
                line);
            Assert.IsFalse(line.Contains("MeasureTool_ActivationShortcut", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

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
