// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests
{
    [TestClass]
    public sealed class TextExpanderBackupTests
    {
        private const string SnippetsKey = "\\TextExpander\\PowerToysTextExpanderSnippets.json";

        [TestMethod]
        [DataRow("[]", 0)]
        [DataRow("[{\"trigger\":\"!x\",\"replacement\":\"text\",},]", 1)]
        public void ExportAcceptsTheSnippetLibrarysSupportedJson(string snippets, int expectedCount)
        {
            WithFile("{/* user's comment */\"version\":1,\"snippets\":" + snippets + ",}", path =>
            {
                string exported = SettingsBackupAndRestoreUtils.GetExportVersion(new JsonObject(), SnippetsKey, path);
                using JsonDocument document = JsonDocument.Parse(exported);
                Assert.AreEqual(expectedCount, document.RootElement.GetProperty("snippets").GetArrayLength());
                Assert.AreEqual(1, document.RootElement.GetProperty("version").GetInt32());
            });
        }

        [TestMethod]
        public void OtherSettingsKeepTheirExistingStrictJsonRules()
        {
            WithFile("{/* comment */\"value\":1,}", path =>
                Assert.Throws<JsonException>(() => SettingsBackupAndRestoreUtils.GetExportVersion(new JsonObject(), "\\Other\\settings.json", path)));
        }

        [TestMethod]
        public void MalformedSnippetJsonIsNotExported()
        {
            WithFile("{\"snippets\":[}", path =>
                Assert.Throws<JsonException>(() => SettingsBackupAndRestoreUtils.GetExportVersion(new JsonObject(), SnippetsKey, path)));
        }

        private static void WithFile(string contents, Action<string> test)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, contents);
                test(path);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
