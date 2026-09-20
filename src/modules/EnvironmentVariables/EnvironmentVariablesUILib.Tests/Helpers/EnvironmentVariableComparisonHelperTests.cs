// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.UnitTests.Helpers;

[TestClass]
public class EnvironmentVariableComparisonHelperTests
{
    [TestMethod]
    public void NamesEqual_IgnoresCase()
    {
        Assert.IsTrue(EnvironmentVariableComparisonHelper.NamesEqual("PATH", "path"));
    }

    [TestMethod]
    public void EntriesEqual_NameIsCaseInsensitiveButValueIsOrdinal()
    {
        var upperName = new Variable("PATH", "Value", VariablesSetType.User);
        var lowerNameSameValue = new Variable("path", "Value", VariablesSetType.System);
        var lowerNameDifferentValueCase = new Variable("path", "value", VariablesSetType.System);

        Assert.IsTrue(EnvironmentVariableComparisonHelper.EntriesEqual(upperName, lowerNameSameValue));
        Assert.IsFalse(EnvironmentVariableComparisonHelper.EntriesEqual(upperName, lowerNameDifferentValueCase));
    }

    [TestMethod]
    public void GetDuplicateNameGroups_ReturnsLegacyEntriesThatDifferOnlyByCase()
    {
        var first = new Variable("PATH", "SystemValue", VariablesSetType.System);
        var second = new Variable("path", "UserValue", VariablesSetType.User);
        var variables = new[]
        {
            first,
            second,
            new Variable("TEMP", "TempValue", VariablesSetType.User),
        };

        var duplicates = EnvironmentVariableComparisonHelper.GetDuplicateNameGroups(variables).ToList();

        Assert.AreEqual(1, duplicates.Count);
        CollectionAssert.AreEquivalent(new[] { first, second }, duplicates[0].ToList());
    }

    [TestMethod]
    public void RemoveDuplicatePathEntries_PreservesFirstOccurrenceAndOrder()
    {
        var result = EnvironmentVariableComparisonHelper.RemoveDuplicatePathEntries(
            @"C:\Tools;C:\Windows;c:\tools;C:\Program Files;C:\WINDOWS");

        Assert.AreEqual(@"C:\Tools;C:\Windows;C:\Program Files", result);
    }

    [TestMethod]
    public void RemoveDuplicatePathEntries_NormalizesSeparatorsAndTrailingSeparators()
    {
        var result = EnvironmentVariableComparisonHelper.RemoveDuplicatePathEntries(
            @"C:\Tools\;C:/Tools;C:\Other");

        Assert.AreEqual(@"C:\Tools\;C:\Other", result);
    }

    [TestMethod]
    public void RemoveDuplicatePathEntries_ExpandsEnvironmentVariablesForComparison()
    {
        const string variableName = "POWERTOYS_PATH_DUPLICATE_TEST";
        Environment.SetEnvironmentVariable(variableName, @"C:\Tools");

        try
        {
            var result = EnvironmentVariableComparisonHelper.RemoveDuplicatePathEntries(
                $@"%{variableName}%;C:\Tools;C:\Other");

            Assert.AreEqual($@"%{variableName}%;C:\Other", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }
}
