// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public class PowerScriptMetadataTests
{
    [TestMethod]
    public void ReadableDefaultsKeepZeroFalseEmptyAndNullDistinctFromMissing()
    {
        var descriptor = JsonNode.Parse("""
            {
              "title":"Example", "name":"example",
              "inputSchema":{"type":"object","additionalProperties":false,"properties":{"parameters":{
                "type":"object","additionalProperties":false,"required":["count"],"properties":{
                "count":{"type":"integer","default":0,"minimum":0,"maximum":5},
                "enabled":{"type":"boolean","default":false},
                "label":{"type":"string","default":""},
                "nullable":{"type":"string","default":null},
                "absent":{"type":"string"},
                "ratio":{"type":"number","default":1.5}
              }}}},
              "outputSchema":{"type":"object","additionalProperties":false,"properties":{"text":{"type":"string"}},"required":["text"]}
            }
            """)!.AsObject();
        var original = descriptor.ToJsonString();
        var rows = PowerScriptMetadataDetails.Create(descriptor, key => key).SelectMany(section => section.Rows).ToDictionary(row => row.Key);
        Assert.AreEqual("0", rows["Parameter0_Default"].Value);
        Assert.AreEqual("PowerScripts_DetailsInteger", rows["Parameter0_Type"].Value);
        Assert.AreEqual("PowerScripts_DetailsYes", rows["Parameter0_Required"].Value);
        Assert.AreEqual("PowerScripts_DetailsNo", rows["Parameter1_Default"].Value);
        Assert.AreEqual("PowerScripts_DetailsEmptyText", rows["Parameter2_Default"].Value);
        Assert.AreEqual("PowerScripts_DetailsNull", rows["Parameter3_Default"].Value);
        Assert.AreEqual("PowerScripts_DetailsNotSpecified", rows["Parameter4_Default"].Value);
        Assert.AreEqual("PowerScripts_DetailsNumber", rows["Parameter5_Type"].Value);
        Assert.AreEqual("1.5", rows["Parameter5_Default"].Value);
        Assert.AreEqual(original, descriptor.ToJsonString(), "Presentation must never mutate authoritative metadata.");
    }

    [TestMethod]
    public void FileFiltersPrimaryInputAndScalarFileParametersHaveClearDifferentMeanings()
    {
        var descriptor = JsonNode.Parse("""
            {
              "inputSchema":{"type":"object","additionalProperties":false,"required":["files"],"properties":{
                "files":{"type":"array","minItems":1,"items":{"type":"string","x-powerscript":{"extensions":["*"]}}},
                "parameters":{"type":"object","additionalProperties":false,"properties":{
                  "destination":{"type":"string","format":"file-path"},
                  "mode":{"type":"string","enum":["fast","precise"],"default":"precise"}
                }}
              }},
              "outputSchema":{"type":"object","additionalProperties":false,"properties":{"files":{"type":"array","items":{"type":"string"}}},"required":["files"]}
            }
            """)!.AsObject();
        var rows = PowerScriptMetadataDetails.Create(descriptor, key => key).SelectMany(section => section.Rows).ToDictionary(row => row.Key);
        Assert.AreEqual("PowerScripts_DetailsAllFiles", rows["Extensions"].Value);
        Assert.AreEqual("PowerScripts_DetailsNoLimit", rows["MaxFiles"].Value);
        Assert.AreEqual("PowerScripts_DetailsFiles", rows["Input"].Value);
        Assert.AreEqual("PowerScripts_DetailsFilePath", rows["Parameter0_Type"].Value);
        Assert.IsFalse(rows.ContainsKey("Parameter0_Role"));
        StringAssert.Contains(rows["Parameter1_Options"].Value, "fast");
        StringAssert.Contains(rows["Parameter1_Options"].Value, "precise");
    }

    [TestMethod]
    public void AdvancedExecutionAndHintsRemainReadableWithoutDroppingArgumentBoundaries()
    {
        var descriptor = JsonNode.Parse("""
            {
              "inputSchema":{"type":"object","additionalProperties":false,"properties":{}},
              "outputSchema":{"type":"object","additionalProperties":false,"properties":{"text":{"type":"string","title":"Result text"}},"required":["text"]},
              "annotations":{"readOnlyHint":false,"destructiveHint":true},
              "x-execute":{"command":["python","script with spaces.py"],"stdin":"json","argMap":{"name":"--name"},"exitCodes":{"2":"Invalid input"}},
              "x-powerscript":{"consumers":["advanced-paste"]}
            }
            """)!.AsObject();
        var rows = PowerScriptMetadataDetails.Create(descriptor, key => key, ".py").SelectMany(section => section.Rows).ToDictionary(row => row.Key);
        Assert.AreEqual("Python", rows["Runtime"].Value);
        Assert.IsFalse(rows.ContainsKey("Function"));
        StringAssert.Contains(rows["Command"].Value, "python");
        StringAssert.Contains(rows["Command"].Value, "script with spaces.py");
        StringAssert.Contains(rows["Command"].Value, System.Environment.NewLine);
        Assert.AreEqual("PowerScripts_DetailsJsonInput", rows["StandardInput"].Value);
        Assert.AreEqual("name: --name", rows["ArgumentMap"].Value);
        Assert.AreEqual("PowerScripts_DetailsNo", rows["Annotation_ReadOnlyHint"].Value);
        Assert.AreEqual("PowerScripts_DetailsYes", rows["Annotation_DestructiveHint"].Value);
        Assert.AreEqual("text", rows["Result0_Name"].Value);
        Assert.AreEqual("PowerScripts_DetailsYes", rows["Result0_Required"].Value);
    }

    [DataTestMethod]
    [DataRow(@"C:\scripts\my project\script.ps1")]
    [DataRow(@"C:\scripts\my project\script.py")]
    [DataRow(@"C:\scripts\my project\script.cmd")]
    public void ScriptEditorAlwaysRequestsEditNeverItsExecutableOpenAssociation(string path)
    {
        var start = PowerScriptFileEditor.CreateStartInfo(path, isMetadata: false);
        Assert.AreEqual(path, start.FileName);
        Assert.AreEqual("edit", start.Verb);
        Assert.IsTrue(start.UseShellExecute);
        Assert.AreEqual(string.Empty, start.Arguments);
    }

    [TestMethod]
    public void MetadataEditorRequestsEditInsteadOfTheDefaultJsonOpenAssociation()
    {
        var path = @"C:\scripts\my project\script.py.tool.json";
        var start = PowerScriptFileEditor.CreateStartInfo(path, isMetadata: true);
        Assert.AreEqual(path, start.FileName);
        Assert.AreEqual("edit", start.Verb);
        Assert.IsTrue(start.UseShellExecute);
        Assert.ThrowsException<System.ArgumentException>(() => PowerScriptFileEditor.CreateStartInfo(@"C:\scripts\script.py", isMetadata: true));
    }
}
