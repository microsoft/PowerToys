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
        public void CreateActionFromScript_ExtractsConversionTypes()
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), $"AdvancedPaste-{Guid.NewGuid():N}.py");

            try
            {
                File.WriteAllLines(
                    scriptPath,
                    [
                        "# @advancedpaste:name   transcribe audio",
                        "# @advancedpaste:desc   Transcribes audio to text",
                        string.Empty,
                        "def advanced_paste_from_audio_to_text(audio_path):",
                        "    return 'transcribed'",
                    ]);

                var createActionFromScript = typeof(AdvancedPasteViewModel)
                    .GetMethod("CreateActionFromScript", BindingFlags.NonPublic | BindingFlags.Static);

                Assert.IsNotNull(createActionFromScript);

                var action = (AdvancedPastePythonScriptAction)createActionFromScript.Invoke(
                    null, [scriptPath, new System.Collections.Generic.List<AdvancedPastePythonScriptAction>()]);

                Assert.AreEqual("transcribe audio", action.Name);
                Assert.AreEqual("Transcribes audio to text", action.Description);
                Assert.AreEqual("audio", action.InputType);
                Assert.AreEqual("text", action.OutputType);
                Assert.AreEqual("audio → text", action.ConversionSummary);
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
