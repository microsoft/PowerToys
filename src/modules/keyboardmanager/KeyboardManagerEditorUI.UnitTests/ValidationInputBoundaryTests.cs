// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Tests the dialog's cheap validation gates without initializing the native
    /// keyboard-mapping service. Invalid user input must be diagnosed before
    /// asking Windows to resolve key names or query shortcut mappings.
    /// </summary>
    [TestClass]
    public class ValidationInputBoundaryTests
    {
        // A negative length represents a null list, zero an unassigned trigger,
        // and one a single key. None is a valid URL/program shortcut trigger.
        [DataTestMethod]
        [DataRow(-1, ValidationErrorType.EmptyOriginalKeys)]
        [DataRow(0, ValidationErrorType.EmptyOriginalKeys)]
        [DataRow(1, ValidationErrorType.OneKeyMapping)]
        public void UrlActionRejectsMissingOrIncompleteTrigger(
            int triggerLength,
            ValidationErrorType expected)
        {
            var result = ValidationHelper.ValidateUrlMapping(
                MakeKeys(triggerLength),
                "https://example.com",
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(-1, ValidationErrorType.EmptyOriginalKeys)]
        [DataRow(0, ValidationErrorType.EmptyOriginalKeys)]
        [DataRow(1, ValidationErrorType.OneKeyMapping)]
        public void ProgramActionRejectsMissingOrIncompleteTrigger(
            int triggerLength,
            ValidationErrorType expected)
        {
            var result = ValidationHelper.ValidateAppMapping(
                MakeKeys(triggerLength),
                "notepad.exe",
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t")]
        [DataRow("\r\n")]
        public void EmptyUrlIsReportedBeforeTriggerChecks(string? value)
        {
            var result = ValidationHelper.ValidateUrlMapping(
                originalKeys: null!,
                url: value!,
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyUrl, result);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t")]
        [DataRow("\r\n")]
        public void EmptyProgramPathIsReportedBeforeTriggerChecks(string? value)
        {
            var result = ValidationHelper.ValidateAppMapping(
                originalKeys: null!,
                programPath: value!,
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyProgramPath, result);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        public void KeyMappingReportsEmptyOriginalBeforeReadingTarget(int triggerLength)
        {
            var result = ValidationHelper.ValidateKeyMapping(
                originalKeys: MakeKeys(triggerLength),
                remappedKeys: null!,
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyOriginalKeys, result);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        public void KeyMappingReportsEmptyTargetBeforeNativeLookup(int targetLength)
        {
            var result = ValidationHelper.ValidateKeyMapping(
                originalKeys: MakeKeys(1),
                remappedKeys: MakeKeys(targetLength),
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyRemappedKeys, result);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        public void DisabledMappingReportsMissingTriggerWithoutNativeLookup(int triggerLength)
        {
            var result = ValidationHelper.ValidateDisableMapping(
                originalKeys: MakeKeys(triggerLength),
                isAppSpecific: true,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyOriginalKeys, result);
        }

        [DataTestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        public void TextMappingReportsMissingTriggerWithoutNativeLookup(int triggerLength)
        {
            var result = ValidationHelper.ValidateTextMapping(
                keys: MakeKeys(triggerLength),
                textContent: "a literal",
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyOriginalKeys, result);
        }

        [DataTestMethod]
        [DataRow("")]
        public void TextMappingReportsEmptyTargetBeforeNativeLookup(string? text)
        {
            var result = ValidationHelper.ValidateTextMapping(
                keys: MakeKeys(1),
                textContent: text!,
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyTargetText, result);
        }

        [TestMethod]
        public void TextMappingAcceptsWhitespaceAsLiteralTextForLaterValidation()
        {
            // Whitespace is meaningful text; verify the validation does not reject
            // it as empty before examining the rest of the shortcut.
            var result = ValidationHelper.ValidateTextMapping(
                keys: null!,
                textContent: " ",
                isAppSpecific: false,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyOriginalKeys, result);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ProgramUrlTriggerValidationWorksForBothApplicationScopes(bool isAppSpecific)
        {
            var urlResult = ValidationHelper.ValidateUrlMapping(
                originalKeys: new List<string>(),
                url: "https://example.com",
                isAppSpecific: isAppSpecific,
                appName: string.Empty,
                mappingService: null!);
            var appResult = ValidationHelper.ValidateAppMapping(
                originalKeys: new List<string>(),
                programPath: "notepad.exe",
                isAppSpecific: isAppSpecific,
                appName: string.Empty,
                mappingService: null!);

            Assert.AreEqual(ValidationErrorType.EmptyOriginalKeys, urlResult);
            Assert.AreEqual(ValidationErrorType.EmptyOriginalKeys, appResult);
        }

        [TestMethod]
        public void NullPayloadsReportFieldSpecificErrorsBeforeLookingAtKeys()
        {
            // Null is intentionally passed for both arguments. Validation should
            // not dereference the native service for malformed dialog input.
            Assert.AreEqual(
                ValidationErrorType.EmptyUrl,
                ValidationHelper.ValidateUrlMapping(
                    originalKeys: null!,
                    url: null!,
                    isAppSpecific: false,
                    appName: string.Empty,
                    mappingService: null!));
            Assert.AreEqual(
                ValidationErrorType.EmptyProgramPath,
                ValidationHelper.ValidateAppMapping(
                    originalKeys: null!,
                    programPath: null!,
                    isAppSpecific: false,
                    appName: string.Empty,
                    mappingService: null!));
            Assert.AreEqual(
                ValidationErrorType.EmptyTargetText,
                ValidationHelper.ValidateTextMapping(
                    keys: MakeKeys(1),
                    textContent: null!,
                    isAppSpecific: false,
                    appName: string.Empty,
                    mappingService: null!));
        }

        private static List<string> MakeKeys(int count)
        {
            if (count < 0)
            {
                return null!;
            }

            var keys = new List<string>();
            for (int i = 0; i < count; i++)
            {
                keys.Add("A");
            }

            return keys;
        }
    }
}
