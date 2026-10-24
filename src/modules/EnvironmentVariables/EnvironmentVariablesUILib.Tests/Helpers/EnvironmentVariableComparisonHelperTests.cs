// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.UnitTests.Helpers;

[TestClass]
public class EnvironmentVariableComparisonHelperTests
{
    private static readonly string[] TrimmedExpansionInputs =
    {
        "%ROOT%", "c:/TOOLS/", @"C:\Other\", "%SECOND%", string.Empty, string.Empty, string.Empty,
    };

    private static readonly string[] InputsBeforeExpansionFailure = { @"C:\A", "%FAIL%" };

    [TestMethod]
    [DataRow(null, null, true)]
    [DataRow("PATH", null, false)]
    [DataRow(null, "PATH", false)]
    [DataRow("é", "e\u0301", false)]
    public void NamesEqual_NullAndDistinctUnicode_ReturnsExpected(string? left, string? right, bool expected)
    {
        Assert.AreEqual(expected, EnvironmentVariableComparisonHelper.NamesEqual(left, right));
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void EntriesEqual_NullOperands_AreNotEqual(bool nullLeft, bool nullRight)
    {
        var entry = new Variable("NAME", "value", VariablesSetType.User);

        Assert.IsFalse(EnvironmentVariableComparisonHelper.EntriesEqual(nullLeft ? null : entry, nullRight ? null : entry));
    }

    [TestMethod]
    public void EntriesEqual_CanonicallyEquivalentValues_AreOrdinallyDifferentAcrossScopes()
    {
        var user = new Variable("NAME", "é", VariablesSetType.User);
        var profile = new Variable("name", "e\u0301", VariablesSetType.Profile);

        Assert.IsFalse(EnvironmentVariableComparisonHelper.EntriesEqual(user, profile));
        profile.Values = "é";
        Assert.IsTrue(EnvironmentVariableComparisonHelper.EntriesEqual(user, profile));
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow(";C:\\Tools;;c:\\tools;", ";C:\\Tools")]
    [DataRow(" C:\\Tools ;C:\\Tools;C:\\Other ", " C:\\Tools ;C:\\Other ")]
    public void RemoveDuplicatePathEntries_EmptyAndWhitespaceSegments_PreservesFirstLiteral(string value, string expected)
    {
        Assert.AreEqual(expected, EnvironmentVariableComparisonHelper.RemoveDuplicatePathEntries(value));
    }

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
    public void RemoveDuplicatePathEntries_UsesTrimmedInjectedExpansionAndPreservesFirstRawSegments()
    {
        var inputs = new List<string>();
        var expansions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["%ROOT%"] = @"C:\Tools\",
            ["%SECOND%"] = @"c:\tools",
        };

        var result = EnvironmentVariableComparisonHelper.RemoveDuplicatePathEntries(
            @" %ROOT% ;c:/TOOLS/; C:\Other\ ;%SECOND%;;;",
            entry =>
            {
                inputs.Add(entry);
                return expansions.GetValueOrDefault(entry, entry);
            });

        Assert.AreEqual(@" %ROOT% ; C:\Other\ ;", result);
        CollectionAssert.AreEqual(TrimmedExpansionInputs, inputs);
    }

    [TestMethod]
    public void RemoveDuplicatePathEntries_PropagatesInjectedExpansionFailureWithoutContinuing()
    {
        var failure = new InvalidOperationException("Scripted expansion failure.");
        var inputs = new List<string>();

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
            EnvironmentVariableComparisonHelper.RemoveDuplicatePathEntries(
                @"C:\A;%FAIL%;C:\B",
                entry =>
                {
                    inputs.Add(entry);
                    if (entry == "%FAIL%")
                    {
                        throw failure;
                    }

                    return entry;
                }));

        Assert.AreSame(failure, actual);
        CollectionAssert.AreEqual(InputsBeforeExpansionFailure, inputs);
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
