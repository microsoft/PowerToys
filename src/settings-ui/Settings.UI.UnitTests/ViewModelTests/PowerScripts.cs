// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class PowerScripts
    {
        private static readonly string[] DisabledNetworkPolicy = { PowerScriptListItem.NetworkPolicy };
        private static readonly string[] UnknownPolicy = { "futurePolicy" };

        [DataTestMethod]
        [DataRow("Python")]
        [DataRow("python")]
        public void PythonUsesADistinctFallbackWhilePowerShellKeepsItsIcon(string runtime)
        {
            var powershell = new PowerScriptListItem { Runtime = "PowerShell" };
            var python = new PowerScriptListItem { Runtime = runtime };

            Assert.AreEqual("\uE756", powershell.DefaultIconGlyph);
            Assert.AreNotEqual(powershell.DefaultIconGlyph, python.DefaultIconGlyph);
            Assert.AreEqual(new PowerScriptListItem { Runtime = "Python" }.DefaultIconGlyph, python.DefaultIconGlyph);
        }

        [TestMethod]
        public void AuthorIconTakesPrecedenceAndDecodeFailureRestoresPythonFallback()
        {
            var script = new PowerScriptListItem { Runtime = "Python", IconPath = @"C:\scripts\assets\script.ico" };
            Assert.IsTrue(script.HasIcon);
            Assert.IsNotNull(script.IconSourcePath);

            script.MarkIconUnavailable();

            Assert.IsFalse(script.HasIcon);
            Assert.IsNull(script.IconSourcePath);
            Assert.IsTrue(script.HasIconWarning);
            Assert.AreEqual(new PowerScriptListItem { Runtime = "Python" }.DefaultIconGlyph, script.DefaultIconGlyph);
            Assert.AreNotEqual(new PowerScriptListItem { Runtime = "PowerShell" }.DefaultIconGlyph, script.DefaultIconGlyph);
        }

        [DataTestMethod]
        [DataRow(".mp3", ".mp3")]
        [DataRow("mp3", ".mp3")]
        public void CatalogueSummaryShowsAcceptedFileType(string extension, string expected)
        {
            var script = new PowerScriptListItem
            {
                Runtime = "Python",
                Io = new PowerScriptIo { Input = "files", Output = "text" },
                Input = new PowerScriptInput { Extensions = new() { extension } },
            };

            StringAssert.StartsWith(script.CatalogueSummary, $"Python \u00b7 {expected} \u2192 ");
            Assert.AreEqual("files", script.Io.Input);
        }

        [TestMethod]
        public void CatalogueSummaryListsMultipleFileTypesWithoutDuplicates()
        {
            var script = new PowerScriptListItem
            {
                Io = new PowerScriptIo { Input = "files" },
                Input = new PowerScriptInput { Extensions = new() { ".mp3", "wav", ".MP3" } },
            };

            Assert.AreEqual(".mp3, .wav", script.InputDisplay);
            StringAssert.Contains(script.CatalogueSummary, "\u00b7 .mp3, .wav \u2192 ");
        }

        [TestMethod]
        public void WildcardInputShowsAllFilesEvenAlongsideSpecificExtensions()
        {
            var script = new PowerScriptListItem
            {
                Io = new PowerScriptIo { Input = "files" },
                Input = new PowerScriptInput { Extensions = new() { "*", ".mp3" } },
            };
            var allFiles = script.InputDisplay;

            Assert.AreNotEqual("*", allFiles);
            Assert.IsFalse(script.CatalogueSummary.Contains(".mp3", StringComparison.Ordinal));
            script.Input.Extensions.Remove(".mp3");
            Assert.AreEqual(allFiles, script.InputDisplay);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void UnspecifiedExtensionsKeepTheGenericFileLabel(bool hasInputMetadata)
        {
            var script = new PowerScriptListItem
            {
                Runtime = "Python",
                Io = new PowerScriptIo { Input = "files", Output = "text" },
                Input = hasInputMetadata ? new PowerScriptInput() : null,
            };

            Assert.AreEqual($"Python \u00b7 {script.IoDisplay}", script.CatalogueSummary);
        }

        [DataTestMethod]
        [DataRow("none")]
        [DataRow("text")]
        public void NonFileInputKeepsItsTypeInsteadOfExtensionMetadata(string input)
        {
            var script = new PowerScriptListItem
            {
                Runtime = "Python",
                Io = new PowerScriptIo { Input = input, Output = "text" },
                Input = new PowerScriptInput { Extensions = new() { ".mp3" } },
            };

            Assert.AreEqual($"Python \u00b7 {script.IoDisplay}", script.CatalogueSummary);
        }

        [TestMethod]
        [DataRow("Python", false)]
        [DataRow("python", true)]
        public void DisabledPythonIsHiddenAndRestoredWithoutRemovingInstalledScripts(string runtime, bool explicitExecution)
        {
            var scripts = CreateCatalogue();
            scripts[1].Runtime = runtime;
            scripts[1].HasExplicitExecution = explicitExecution;
            var visible = PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 0, pythonEnabled: false);
            Assert.AreEqual("action,text", string.Join(",", visible.Select(script => script.Id)));
            Assert.AreEqual(3, scripts.Length);
            Assert.AreSame(scripts[0], visible[0]);
            Assert.AreSame(scripts[1], PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 0, pythonEnabled: true)[^1]);
        }

        [TestMethod]
        public void RuntimeVisibilityCombinesWithSearchAndIoFilters()
        {
            var scripts = CreateCatalogue();
            Assert.AreEqual(0, PowerScriptsViewModel.FilterAndSortScripts(scripts, 1, 2, 1, "zeb", pythonEnabled: false).Count);
            Assert.AreSame(scripts[1], PowerScriptsViewModel.FilterAndSortScripts(scripts, 1, 2, 1, "zeb", pythonEnabled: true).Single());
            Assert.AreSame(scripts[2], PowerScriptsViewModel.FilterAndSortScripts(scripts, 1, 3, 2, "br", pythonEnabled: false).Single());
        }

        [TestMethod]
        public void CatalogueFiltersByPrimaryInputWithoutChangingScriptInstances()
        {
            var scripts = CreateCatalogue();
            scripts[0].Parameters.Add(new PowerScriptParameterItem { Name = "source", Type = "file", IsRequired = true });
            var noInput = PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 1);
            Assert.AreEqual(1, noInput.Count);
            Assert.AreSame(scripts[0], noInput[0], "Filtering must preserve each script's policy state.");
            Assert.AreEqual("action", noInput[0].Id);
            Assert.AreEqual(3, scripts.Length);

            var files = PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 2);
            Assert.AreEqual("files", files.Single().Id);
        }

        [DataTestMethod]
        [DataRow(0, "action", "text", "files")]
        [DataRow(1, "files", "text", "action")]
        [DataRow(2, "files", "action", "text")]
        [DataRow(3, "text", "files", "action")]
        [DataRow(4, "action", "text", "files")]
        public void CatalogueSortsBySelectedMetadata(int sortIndex, string first, string second, string third)
        {
            var sorted = PowerScriptsViewModel.FilterAndSortScripts(CreateCatalogue(), sortIndex, 0);
            CollectionAssert.AreEqual(new[] { first, second, third }, sorted.Select(script => script.Id).ToArray());
        }

        [TestMethod]
        public void CatalogueCanRecoverFromAnEmptyFilter()
        {
            var scripts = CreateCatalogue().Take(2).ToArray();
            Assert.AreEqual(0, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 3).Count);
            Assert.AreEqual(2, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 0).Count);
        }

        [DataTestMethod]
        [DataRow(0, "action,text,files")]
        [DataRow(1, "files")]
        [DataRow(2, "text")]
        [DataRow(3, "action")]
        public void CatalogueFiltersByOutput(int outputFilter, string expectedIds)
        {
            var visible = PowerScriptsViewModel.FilterAndSortScripts(CreateCatalogue(), 0, 0, outputFilter);
            Assert.AreEqual(expectedIds, string.Join(",", visible.Select(script => script.Id)));
        }

        [TestMethod]
        public void CatalogueCombinesNameInputAndOutputWithoutReplacingScriptInstances()
        {
            var scripts = CreateCatalogue();
            var visible = PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 2, 1, "  eBR  ");
            Assert.AreSame(scripts[1], visible.Single());
            Assert.AreEqual(0, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 3, 1, "ebr").Count);
            Assert.AreEqual(0, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 2, 3, "ebr").Count);
            Assert.AreEqual(0, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 2, 1, "files").Count, "Search matches the display name, not the action ID.");
        }

        [TestMethod]
        public void CatalogueSortsNameSearchResultsAndRecoversWhenSearchIsCleared()
        {
            var scripts = CreateCatalogue();
            var visible = PowerScriptsViewModel.FilterAndSortScripts(scripts, 1, 0, 0, "a");
            Assert.AreEqual("files,text,action", string.Join(",", visible.Select(script => script.Id)));
            Assert.AreEqual(0, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 0, 3, "no matching name").Count);
            Assert.AreSame(scripts[0], PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 0, 3, "   ").Single(), "Clearing search must preserve the active output filter.");
            Assert.AreEqual(3, PowerScriptsViewModel.FilterAndSortScripts(scripts, 0, 0, 0, null).Count);
        }

        private static PowerScriptListItem[] CreateCatalogue() => new[]
        {
            new PowerScriptListItem { Id = "action", Name = "alpha", Runtime = "PowerShell", Io = new PowerScriptIo { Input = "none", Output = "text" } },
            new PowerScriptListItem { Id = "files", Name = "Zebra", Runtime = "Python", Io = new PowerScriptIo { Input = "files", Output = "none" } },
            new PowerScriptListItem { Id = "text", Name = "Bravo", Runtime = "PowerShell", Io = new PowerScriptIo { Input = "text", Output = "files" } },
        };

        [TestMethod]
        public void MxcSafeSettingsDoNotRequireRiskAcceptance()
        {
            Assert.IsFalse(PowerScriptsViewModel.RequiresMxcRiskAcceptance(true, Array.Empty<string>()));
            Assert.IsFalse(PowerScriptsViewModel.RequiresMxcRiskAcceptance(null, Array.Empty<string>()));
        }

        [TestMethod]
        public void MxcDisabledExecutionRequiresRiskAcceptance()
        {
            Assert.IsTrue(PowerScriptsViewModel.RequiresMxcRiskAcceptance(false, Array.Empty<string>()));
        }

        [TestMethod]
        public void MxcDisabledKnownPolicyRequiresRiskAcceptance()
        {
            Assert.IsTrue(PowerScriptsViewModel.RequiresMxcRiskAcceptance(
                true,
                DisabledNetworkPolicy));
        }

        [TestMethod]
        public void MxcUnknownPolicyDoesNotWeakenIsolation()
        {
            Assert.IsFalse(PowerScriptsViewModel.RequiresMxcRiskAcceptance(
                true,
                UnknownPolicy));
        }

        [TestMethod]
        public void MxcUnspecifiedPreferenceFollowsPlatformSupport()
        {
            Assert.IsTrue(PowerScriptsViewModel.ResolveMxcEnabled(null, true));
            Assert.IsFalse(PowerScriptsViewModel.ResolveMxcEnabled(null, false));
        }

        [TestMethod]
        public void MxcExplicitPreferenceOverridesPlatformDefault()
        {
            Assert.IsTrue(PowerScriptsViewModel.ResolveMxcEnabled(true, false));
            Assert.IsFalse(PowerScriptsViewModel.ResolveMxcEnabled(false, true));
        }
    }
}
