// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class TextExpansionValidationTests
    {
        private static readonly int[] SpaceActivation = [0x20];
        private static readonly int[] LetterActivation = [0x41];
        private static readonly int[] FunctionKeyActivation = [0x87];
        private static readonly int[] OemKeyActivation = [0xBA];
        private static readonly int[] CtrlSpaceActivation = [0x11, 0x20];
        private static readonly int[] CtrlShiftEnterActivation = [0x11, 0x10, 0x0D];
        private static readonly int[] AllModifiersActivation = [0x5B, 0x11, 0x12, 0x10, 0x87];
        private static readonly int[] GenericWinActivation = [0x104, 0x41];
        private static readonly int[] ModifierOnlyActivation = [0x11];
        private static readonly int[] MultipleActionKeysActivation = [0x11, 0x41, 0x42];
        private static readonly int[] DuplicateCtrlActivation = [0x11, 0xA2, 0x41];
        private static readonly int[] DuplicateWinActivation = [0x104, 0x5B, 0x41];
        private static readonly int[] ZeroKeyActivation = [0, 0x41];
        private static readonly int[] NegativeKeyActivation = [-1];
        private static readonly int[] OutOfRangeActivation = [0x100];
        private static readonly string[] ExpectedActivationKeyNames = ["Ctrl", "Space"];

        [TestMethod]
        public void TextValidation_RequiresTextAndUsesUtf16LengthLimit()
        {
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText(null));
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText(string.Empty));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText(null));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText(string.Empty));

            Assert.IsTrue(TextExpansionValidation.IsValidSourceText(new string('a', TextExpansionValidation.MaxTextLength)));
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText(new string('a', TextExpansionValidation.MaxTextLength + 1)));

            // A supplementary Unicode scalar occupies two UTF-16 code units.
            Assert.IsTrue(TextExpansionValidation.IsValidReplacementText(string.Concat(System.Linq.Enumerable.Repeat("😀", 128))));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText(string.Concat(System.Linq.Enumerable.Repeat("😀", 128)) + "x"));
        }

        [TestMethod]
        public void TextValidation_EnforcesSourceAndReplacementControlCharacterRules()
        {
            Assert.IsTrue(TextExpansionValidation.IsValidSourceText("café 😀"));
            Assert.IsTrue(TextExpansionValidation.IsValidReplacementText("symbols: © € 中文"));
            Assert.IsTrue(TextExpansionValidation.IsValidReplacementText("line one\r\nline two\nline three\r"));

            Assert.IsFalse(TextExpansionValidation.IsValidSourceText("tab\t"));
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText("nul\0"));
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText("line\rbreak"));
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText("line\nbreak"));
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText("delete \u007F"));
            Assert.IsFalse(TextExpansionValidation.IsValidSourceText("C1 \u0085"));

            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText("tab\t"));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText("nul\0"));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText("backspace\b"));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText("delete \u007F"));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText("C1 \u0085"));

            Assert.IsFalse(TextExpansionValidation.IsValidSourceText("unpaired high: \uD83D"));
            Assert.IsFalse(TextExpansionValidation.IsValidReplacementText("unpaired low: \uDE00"));
        }

        [TestMethod]
        public void ActivationValidation_AcceptsOrdinarySingleKeysWithoutAFeatureAllowlist()
        {
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(SpaceActivation));
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(LetterActivation));
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(FunctionKeyActivation));
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(OemKeyActivation));
        }

        [TestMethod]
        public void ActivationValidation_AcceptsModifierShortcutWithOneActionKey()
        {
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(CtrlSpaceActivation));
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(CtrlShiftEnterActivation));
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(AllModifiersActivation));
            Assert.IsTrue(TextExpansionValidation.IsValidActivationKeys(GenericWinActivation));
        }

        [TestMethod]
        public void ActivationValidation_RejectsIncompleteOrChordLikeInput()
        {
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(null));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(System.Array.Empty<int>()));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(ModifierOnlyActivation));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(MultipleActionKeysActivation));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(DuplicateCtrlActivation));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(DuplicateWinActivation));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(ZeroKeyActivation));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(NegativeKeyActivation));
            Assert.IsFalse(TextExpansionValidation.IsValidActivationKeys(OutOfRangeActivation));
        }

        [TestMethod]
        public void GuidValidation_AcceptsOnlyCanonicalLowercaseDFormat()
        {
            Assert.IsTrue(TextExpansionValidation.IsCanonicalGuid("2d6e35c0-7344-47ad-b1ba-0348f41fa21f"));

            Assert.IsFalse(TextExpansionValidation.IsCanonicalGuid(null));
            Assert.IsFalse(TextExpansionValidation.IsCanonicalGuid(string.Empty));
            Assert.IsFalse(TextExpansionValidation.IsCanonicalGuid("2D6E35C0-7344-47AD-B1BA-0348F41FA21F"));
            Assert.IsFalse(TextExpansionValidation.IsCanonicalGuid("{2d6e35c0-7344-47ad-b1ba-0348f41fa21f}"));
            Assert.IsFalse(TextExpansionValidation.IsCanonicalGuid("not-a-guid"));
        }

        [TestMethod]
        public void MappingModel_KeepsGuidStableWhileEditableFieldsChange()
        {
            var mapping = new TextExpansionMapping
            {
                Id = "2d6e35c0-7344-47ad-b1ba-0348f41fa21f",
                SourceText = "brb",
                ActivationKeys = new List<int> { 0x20 },
                ActivationKeyNames = new List<string> { "Space" },
                ReplacementText = "be right back",
                IsEnabled = true,
            };

            mapping.SourceText = "omw";
            mapping.ActivationKeys = new List<int> { 0x11, 0x20 };
            mapping.ActivationKeyNames = new List<string> { "Ctrl", "Space" };
            mapping.ReplacementText = "on my way";
            mapping.IsEnabled = false;

            Assert.AreEqual("2d6e35c0-7344-47ad-b1ba-0348f41fa21f", mapping.Id);
            Assert.AreEqual("omw", mapping.SourceText);
            CollectionAssert.AreEqual(CtrlSpaceActivation, mapping.ActivationKeys);
            CollectionAssert.AreEqual(ExpectedActivationKeyNames, mapping.ActivationKeyNames);
            Assert.AreEqual("on my way", mapping.ReplacementText);
            Assert.IsFalse(mapping.IsEnabled);
        }
    }
}
