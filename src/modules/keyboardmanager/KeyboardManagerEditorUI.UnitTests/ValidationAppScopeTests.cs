// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Global and application-specific shortcut mappings belong to separate
    /// engine tables. Application names within the same table are compared
    /// ordinally and case-insensitively, independent of the current culture.
    /// </summary>
    [TestClass]
    public class ValidationAppScopeTests
    {
        [DataTestMethod]
        [DataRow("", "", true)]
        [DataRow("", "notepad.exe", false)]
        [DataRow("notepad.exe", "", false)]
        [DataRow("notepad.exe", "notepad.exe", true)]
        [DataRow("notepad.exe", "NOTEPAD.EXE", true)]
        [DataRow("NOTEPAD.EXE", "notepad.exe", true)]
        [DataRow("Notepad.Exe", "nOtEpAd.eXe", true)]
        [DataRow("PowerToys.KeyboardManagerEditorUI", "powertoys.keyboardmanagereditorui", true)]
        [DataRow("notepad.exe", "calc.exe", false)]
        [DataRow("calc.exe", "notepad.exe", false)]
        [DataRow("notepad", "notepad.exe", false)]
        [DataRow("notepad.exe", "notepad", false)]
        [DataRow("notepad.exe", "notepad.exe.bak", false)]
        [DataRow("notepad.exe.bak", "notepad.exe", false)]
        [DataRow("notepad.exe", " notepad.exe", false)]
        [DataRow("notepad.exe ", "notepad.exe", false)]
        public void ApplicationScopeEqualityUsesWindowsExecutableSemantics(
            string existingApp,
            string requestedApp,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                DuplicateMappingHelper.IsSameAppScope(existingApp, requestedApp));
        }

        [TestMethod]
        public void NullExistingApplicationRepresentsAGlobalMapping()
        {
            Assert.IsFalse(
                DuplicateMappingHelper.IsSameAppScope(null, "notepad.exe"),
                "Global and app-specific shortcut mappings use separate engine tables.");
        }

        [TestMethod]
        public void NullRequestedApplicationRepresentsAGlobalMapping()
        {
            Assert.IsFalse(
                DuplicateMappingHelper.IsSameAppScope("notepad.exe", null),
                "An app-specific shortcut mapping does not duplicate a global override.");
        }

        [TestMethod]
        public void NullAndEmptyNamesAreEquivalentGlobalScopes()
        {
            Assert.IsTrue(DuplicateMappingHelper.IsSameAppScope(null, string.Empty));
            Assert.IsTrue(DuplicateMappingHelper.IsSameAppScope(string.Empty, null));
        }

        [TestMethod]
        public void TwoNullApplicationsRepresentTheSameGlobalScope()
        {
            Assert.IsTrue(
                DuplicateMappingHelper.IsSameAppScope(null, null),
                "Two global scopes have the same effective scope.");
        }

        [DataTestMethod]
        [DataRow("NOTEPAD.EXE", "notepad.exe")]
        [DataRow("PowerToys.Settings", "powertoys.settings")]
        [DataRow("Calc.Exe", "CALC.EXE")]
        [DataRow("wordpad.exe", "notepad.exe")]
        [DataRow("", "notepad.exe")]
        public void ScopeEqualityIsSymmetricForEveryApplicationScope(string first, string second)
        {
            var forwards = DuplicateMappingHelper.IsSameAppScope(first, second);
            var backwards = DuplicateMappingHelper.IsSameAppScope(second, first);
            Assert.AreEqual(forwards, backwards, "Scope equality must be symmetric.");
        }

        [DataTestMethod]
        [DataRow("notepad.exe", "NOTEPAD.EXE")]
        [DataRow("PowerToys.Settings", "powertoys.settings")]
        [DataRow("ExampleTool.exe", "exampletool.EXE")]
        public void CaseVariantMappingsCannotBypassDuplicateDetection(string existing, string candidate)
        {
            Assert.IsTrue(
                DuplicateMappingHelper.IsSameAppScope(existing, candidate),
                "Case differences must not create a different application scope.");
        }

        [DataTestMethod]
        [DataRow("notepad.exe", "calc.exe")]
        [DataRow("app.exe", "app2.exe")]
        [DataRow("powershell.exe", "pwsh.exe")]
        public void DistinctExecutableNamesRemainIndependent(string existing, string candidate)
        {
            Assert.IsFalse(
                DuplicateMappingHelper.IsSameAppScope(existing, candidate),
                "Unrelated app-specific mappings should be able to share the same shortcut.");
        }

        [DataTestMethod]
        [DataRow("tr-TR")]
        [DataRow("az-Latn-AZ")]
        public void ProcessNameComparisonIgnoresCurrentCulture(string cultureName)
        {
            // Turkish and Azerbaijani casing rules treat 'I' and 'i' as
            // different letters. Windows process-name matching must not.
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUICulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo testCulture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentCulture = testCulture;
                CultureInfo.CurrentUICulture = testCulture;

                Assert.IsTrue(
                    DuplicateMappingHelper.IsSameAppScope("FILE.EXE", "file.exe"),
                    "Executable matching must not vary with the user's regional settings.");

                Assert.IsFalse(
                    DuplicateMappingHelper.IsSameAppScope("FILE.EXE", "filter.exe"),
                    "Different executables must remain distinct in every culture.");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUICulture;
            }
        }
    }
}
