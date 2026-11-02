// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Exercises the shared app-specific name guard across all action types
    /// that can be checked without initializing the native mapping library.
    /// Empty and whitespace-only app names must never be persisted as scoped
    /// rules and must not be mistaken for global rules.
    /// </summary>
    [TestClass]
    public class ValidationAppNameTests
    {
        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("\r\n")]
        [DataRow("\u00a0")]
        public void KeyMappingRejectsEmptyOrWhitespaceApplication(string application)
        {
            var error = ValidationHelper.ValidateKeyMapping(
                originalKeys: OneKey(),
                remappedKeys: AnotherKey(),
                isAppSpecific: true,
                appName: application,
                mappingService: null!);

            Assert.AreEqual(
                ValidationErrorType.EmptyAppName,
                error,
                $"An invalid application name '{application}' reached native validation.");
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t")]
        [DataRow("\r\n")]
        [DataRow("\u00a0")]
        public void DisableMappingRejectsEmptyOrWhitespaceApplication(string application)
        {
            var error = ValidationHelper.ValidateDisableMapping(
                originalKeys: OneKey(),
                isAppSpecific: true,
                appName: application,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyAppName, error);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t")]
        [DataRow("\r\n")]
        [DataRow("\u00a0")]
        public void TextMappingRejectsEmptyOrWhitespaceApplication(string application)
        {
            var error = ValidationHelper.ValidateTextMapping(
                keys: OneKey(),
                textContent: "meaningful text",
                isAppSpecific: true,
                appName: application,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyAppName, error);
        }

        [TestMethod]
        public void NullApplicationIsReportedForKeyMapping()
        {
            Assert.AreEqual(
                ValidationErrorType.EmptyAppName,
                ValidationHelper.ValidateKeyMapping(
                    originalKeys: OneKey(),
                    remappedKeys: AnotherKey(),
                    isAppSpecific: true,
                    appName: null!,
                    mappingService: null!));
        }

        [TestMethod]
        public void NullApplicationIsReportedForDisableMapping()
        {
            Assert.AreEqual(
                ValidationErrorType.EmptyAppName,
                ValidationHelper.ValidateDisableMapping(
                    originalKeys: OneKey(),
                    isAppSpecific: true,
                    appName: null!,
                    mappingService: null!));
        }

        [TestMethod]
        public void NullApplicationIsReportedForTextMapping()
        {
            Assert.AreEqual(
                ValidationErrorType.EmptyAppName,
                ValidationHelper.ValidateTextMapping(
                    keys: OneKey(),
                    textContent: "meaningful text",
                    isAppSpecific: true,
                    appName: null!,
                    mappingService: null!));
        }

        [TestMethod]
        public void EmptyTriggerTakesPriorityOverEmptyApplication()
        {
            Assert.AreEqual(
                ValidationErrorType.EmptyOriginalKeys,
                ValidationHelper.ValidateKeyMapping(
                    originalKeys: new List<string>(),
                    remappedKeys: AnotherKey(),
                    isAppSpecific: true,
                    appName: string.Empty,
                    mappingService: null!));

            Assert.AreEqual(
                ValidationErrorType.EmptyOriginalKeys,
                ValidationHelper.ValidateDisableMapping(
                    originalKeys: new List<string>(),
                    isAppSpecific: true,
                    appName: string.Empty,
                    mappingService: null!));

            Assert.AreEqual(
                ValidationErrorType.EmptyOriginalKeys,
                ValidationHelper.ValidateTextMapping(
                    keys: new List<string>(),
                    textContent: "meaningful text",
                    isAppSpecific: true,
                    appName: string.Empty,
                    mappingService: null!));
        }

        [TestMethod]
        public void EmptyKeyActionTakesPriorityOverEmptyApplication()
        {
            Assert.AreEqual(
                ValidationErrorType.EmptyRemappedKeys,
                ValidationHelper.ValidateKeyMapping(
                    originalKeys: OneKey(),
                    remappedKeys: new List<string>(),
                    isAppSpecific: true,
                    appName: string.Empty,
                    mappingService: null!));
        }

        [TestMethod]
        public void EmptyTextTakesPriorityOverEmptyApplication()
        {
            Assert.AreEqual(
                ValidationErrorType.EmptyTargetText,
                ValidationHelper.ValidateTextMapping(
                    keys: OneKey(),
                    textContent: string.Empty,
                    isAppSpecific: true,
                    appName: string.Empty,
                    mappingService: null!));
        }

        [TestMethod]
        public void GlobalDisableIgnoresBlankApplicationWithoutNativeLookup()
        {
            // A global mapping with a nonempty trigger is allowed to omit the
            // target app; any later validation requires native shortcut data.
            // Verify the empty-trigger fast path is independent of app scope.
            Assert.AreEqual(
                ValidationErrorType.EmptyOriginalKeys,
                ValidationHelper.ValidateDisableMapping(
                    originalKeys: new List<string>(),
                    isAppSpecific: false,
                    appName: string.Empty,
                    mappingService: null!));
        }

        private static List<string> OneKey() => new List<string> { "A" };

        private static List<string> AnotherKey() => new List<string> { "B" };
    }
}
