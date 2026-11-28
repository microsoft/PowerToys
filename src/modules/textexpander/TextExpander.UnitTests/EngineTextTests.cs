// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed partial class EngineTextTests
{
    [TestMethod]
    public void EveryEngineMessageIsInTheResourcesWithTheSameEnglish()
    {
        // QA-12: the engine's prompt and messages were English only. Each call carries its
        // English as the fallback, and it has to match the resource that translators work from.
        string? engine = FindEngineFolder();
        if (engine is null)
        {
            Assert.Inconclusive("The engine's sources aren't next to the tests here.");
        }

        Dictionary<string, string> resources = XDocument.Load(Path.Combine(engine, "Strings", "en-us", "Resources.resw")).Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => ((string)data.Element("value")!).Replace("\r\n", "\n", StringComparison.Ordinal));
        var calls = Directory.EnumerateFiles(engine, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => EngineTextCall().Matches(File.ReadAllText(file)))
            .ToList();

        Assert.IsTrue(calls.Count >= 10, calls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (Match call in calls)
        {
            string key = call.Groups["key"].Value;
            Assert.IsTrue(resources.ContainsKey(key), key);
            Assert.AreEqual(Regex.Unescape(call.Groups["english"].Value), resources[key], key);
        }

        CollectionAssert.AreEquivalent(resources.Keys.ToArray(), calls.Select(call => call.Groups["key"].Value).Distinct().ToArray(), "Every resource is used.");
        Assert.AreEqual("fallback", EngineText.Get("No_Such_String", "fallback"), "A missing resource falls back to the English.");
    }

    [GeneratedRegex("""EngineText\.Get\(\s*"(?<key>[^"]+)",\s*"(?<english>(?:[^"\\]|\\.)*)"\)""")]
    private static partial Regex EngineTextCall();

    private static string? FindEngineFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string engine = Path.Combine(folder.FullName, "src", "modules", "textexpander", "TextExpander");
            if (File.Exists(Path.Combine(engine, "Strings", "en-us", "Resources.resw")))
            {
                return engine;
            }
        }

        return null;
    }
}
