// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class AdvancedPaste
    {
        [TestMethod]
        public void WriteScriptHeader_OverwritesExistingBlankDescriptionTag()
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), $"AdvancedPaste-{Guid.NewGuid():N}.py");

            try
            {
                File.WriteAllLines(
                    scriptPath,
                    [
                        "# @advancedpaste:name   reverse text",
                        "# @advancedpaste:formats   text",
                        "# @advancedpaste:platform   windows",
                        "# @advancedpaste:desc   ",
                        "# @advancedpaste:enabled   true",
                        "print('hello')",
                    ]);

                var action = new AdvancedPastePythonScriptAction
                {
                    ScriptPath = scriptPath,
                    Name = "reverse text",
                    Description = "Updated description",
                    Platform = "windows",
                    Formats = "text",
                    IsEnabled = true,
                    RequiresAutoDetect = true,
                };

                var writeScriptHeader = typeof(AdvancedPasteViewModel)
                    .GetMethod("WriteScriptHeader", BindingFlags.NonPublic | BindingFlags.Static);

                Assert.IsNotNull(writeScriptHeader);

                writeScriptHeader.Invoke(null, [action]);

                var descLines = File.ReadAllLines(scriptPath)
                    .Where(line => line.Contains("@advancedpaste:desc", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                Assert.AreEqual(1, descLines.Length);
                Assert.AreEqual("# @advancedpaste:desc   Updated description", descLines[0]);
            }
            finally
            {
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
        }
    }
}