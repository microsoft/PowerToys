// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerAccent.Common;
using WinRtLetterKey = PowerToys.PowerAccentKeyboardService.LetterKey;

namespace PowerAccent.Common.UnitTests;

[TestClass]
public sealed class LetterKeyTests
{
    // Verifies that the managed LetterKey enum in PowerAccent.Common stays in sync with the
    // WinRT LetterKey enum defined in KeyboardListener.idl. The adapter in PowerAccent.Core
    // casts between them via their integer values, so any divergence would silently produce
    // wrong character mappings at runtime.
    [TestMethod]
    public void ManagedLetterKey_MatchesWinRtLetterKey_AllNamesPresent()
    {
        foreach (WinRtLetterKey winRtValue in Enum.GetValues<WinRtLetterKey>())
        {
            var name = winRtValue.ToString();
            Assert.IsTrue(
                Enum.TryParse<LetterKey>(name, out _),
                $"WinRT LetterKey.{name} has no corresponding value in the managed LetterKey enum. Update PowerAccent.Common/LetterKey.cs.");
        }
    }

    [TestMethod]
    public void ManagedLetterKey_MatchesWinRtLetterKey_ValuesMatch()
    {
        foreach (WinRtLetterKey winRtValue in Enum.GetValues<WinRtLetterKey>())
        {
            var name = winRtValue.ToString();
            if (Enum.TryParse<LetterKey>(name, out var managedValue))
            {
                Assert.AreEqual(
                    (int)(object)winRtValue,
                    (int)managedValue,
                    $"LetterKey.{name} has value {(int)(object)winRtValue} in WinRT but {(int)managedValue} in the managed enum. Update PowerAccent.Common/LetterKey.cs.");
            }
        }
    }
}
