// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// App-specific duplicates must compare process names the way Windows does:
    /// ordinally and case-insensitively. The overlap predicate is deliberately
    /// independently testable without loading the native keyboard hook library.
    /// </summary>
    [TestClass]
    public class ValidationAppScopeTests
    {
        [DataTestMethod]
        [DataRow("", "", true)]
        [DataRow("", "notepad.exe", true)]
        [DataRow("notepad.exe", "", true)]
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
        public void ApplicationScopeOverlapUsesWindowsExecutableSemantics(
            string existingApp,
            string requestedApp,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                ValidationHelper.DoApplicationScopesOverlap(existingApp, requestedApp));
        }

        [TestMethod]
        public void NullExistingApplicationRepresentsAGlobalMapping()
        {
            Assert.IsTrue(
                ValidationHelper.DoApplicationScopesOverlap(null, "notepad.exe"),
                "An absent existing app must overlap any candidate app.");
        }

        [TestMethod]
        public void NullRequestedApplicationRepresentsAGlobalMapping()
        {
            Assert.IsTrue(
                ValidationHelper.DoApplicationScopesOverlap("notepad.exe", null),
                "An absent requested app must overlap any existing app.");
        }

        [TestMethod]
        public void TwoNullApplicationsOverlap()
        {
            Assert.IsTrue(
                ValidationHelper.DoApplicationScopesOverlap(null, null),
                "Two global scopes must conflict.");
        }

        [DataTestMethod]
        [DataRow("NOTEPAD.EXE", "notepad.exe")]
        [DataRow("PowerToys.Settings", "powertoys.settings")]
        [DataRow("Calc.Exe", "CALC.EXE")]
        [DataRow("wordpad.exe", "notepad.exe")]
        [DataRow("", "notepad.exe")]
        public void OverlapIsSymmetricForEveryApplicationScope(string first, string second)
        {
            var forwards = ValidationHelper.DoApplicationScopesOverlap(first, second);
            var backwards = ValidationHelper.DoApplicationScopesOverlap(second, first);
            Assert.AreEqual(forwards, backwards, "Reversing the mapping's existing/candidate roles changed overlap.");
        }

        [DataTestMethod]
        [DataRow("notepad.exe", "NOTEPAD.EXE")]
        [DataRow("PowerToys.Settings", "powertoys.settings")]
        [DataRow("ExampleTool.exe", "exampletool.EXE")]
        public void CaseVariantMappingsCannotBypassDuplicateDetection(string existing, string candidate)
        {
            Assert.IsTrue(
                ValidationHelper.DoApplicationScopesOverlap(existing, candidate),
                "Application scope matching must reject duplicate executables with different casing.");
        }

        [DataTestMethod]
        [DataRow("notepad.exe", "calc.exe")]
        [DataRow("app.exe", "app2.exe")]
        [DataRow("powershell.exe", "pwsh.exe")]
        public void DistinctExecutableNamesRemainIndependent(string existing, string candidate)
        {
            Assert.IsFalse(
                ValidationHelper.DoApplicationScopesOverlap(existing, candidate),
                "Unrelated app-specific mappings should be able to share the same shortcut.");
        }
    }
}
