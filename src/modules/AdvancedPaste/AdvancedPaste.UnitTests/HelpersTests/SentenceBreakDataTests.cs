// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class SentenceBreakDataTests
{
    private const string ExpectedSha256 = "871c0c985ad95125e25b302414065a10839d068970bceb383ecec138f22a0a18";

    private static readonly IReadOnlyDictionary<SentenceBreakType, int> ExpectedCounts =
        new Dictionary<SentenceBreakType, int>
        {
            [SentenceBreakType.Other] = 962115,
            [SentenceBreakType.ATerm] = 4,
            [SentenceBreakType.Close] = 195,
            [SentenceBreakType.Format] = 60,
            [SentenceBreakType.Lower] = 2548,
            [SentenceBreakType.Numeric] = 785,
            [SentenceBreakType.OLetter] = 141501,
            [SentenceBreakType.Sep] = 3,
            [SentenceBreakType.Sp] = 20,
            [SentenceBreakType.STerm] = 166,
            [SentenceBreakType.Upper] = 1991,
            [SentenceBreakType.CR] = 1,
            [SentenceBreakType.Extend] = 2643,
            [SentenceBreakType.LF] = 1,
            [SentenceBreakType.SContinue] = 31,
        };

    [TestMethod]
    public void LookupMatchesVendoredSourceForEveryValidUnicodeScalar()
    {
        byte[] sourceBytes = File.ReadAllBytes(SourcePath);
        Assert.AreEqual(ExpectedSha256, Convert.ToHexStringLower(SHA256.HashData(sourceBytes)));
        var expected = ParseSource(File.ReadAllLines(SourcePath));
        var actualCounts = Enum.GetValues<SentenceBreakType>().ToDictionary(static property => property, static _ => 0);
        int tested = 0;
        int mismatches = 0;

        for (int scalar = 0; scalar <= 0x10FFFF; scalar++)
        {
            if (scalar is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            SentenceBreakType actual = SentenceBreakData.GetSentenceBreakType(scalar);
            if (actual != expected[scalar])
            {
                mismatches++;
            }

            actualCounts[actual]++;
            tested++;
        }

        Assert.AreEqual(1_112_064, tested);
        Assert.AreEqual(0, mismatches);
        foreach ((SentenceBreakType property, int count) in ExpectedCounts)
        {
            Assert.AreEqual(count, actualCounts[property], $"Unexpected scalar count for {property}.");
        }
    }

    [TestMethod]
    public void LookupReturnsExpectedPropertyForRepresentativeScalars()
    {
        (int Scalar, SentenceBreakType Property)[] cases =
        [
            (0x0000, SentenceBreakType.Other),
            (0x002E, SentenceBreakType.ATerm),
            (0x2024, SentenceBreakType.ATerm),
            (0xFE52, SentenceBreakType.ATerm),
            (0xFF0E, SentenceBreakType.ATerm),
            (0xFF9E, SentenceBreakType.Extend),
            (0xFF9F, SentenceBreakType.Extend),
            (0x275B, SentenceBreakType.Close),
            (0x275C, SentenceBreakType.Close),
            (0x000B, SentenceBreakType.Sp),
            (0x000C, SentenceBreakType.Sp),
            (0x000D, SentenceBreakType.CR),
            (0x000A, SentenceBreakType.LF),
            (0x0085, SentenceBreakType.Sep),
            (0x2028, SentenceBreakType.Sep),
            (0x2029, SentenceBreakType.Sep),
            (0x066B, SentenceBreakType.Numeric),
            (0x066C, SentenceBreakType.Numeric),
            (0x2160, SentenceBreakType.Upper),
            (0x00BD, SentenceBreakType.Other),
            (0x00B2, SentenceBreakType.Other),
            (0x0021, SentenceBreakType.STerm),
            (0x002C, SentenceBreakType.SContinue),
            (0x002F, SentenceBreakType.Other),
            (0x10FFFF, SentenceBreakType.Other),
        ];

        foreach ((int scalar, SentenceBreakType expected) in cases)
        {
            Assert.AreEqual(expected, SentenceBreakData.GetSentenceBreakType(scalar), $"Unexpected property for U+{scalar:X4}.");
        }
    }

    private static string SourcePath => Path.Combine(AppContext.BaseDirectory, "UnicodeData", "SentenceBreakProperty.txt");

    private static SentenceBreakType[] ParseSource(IEnumerable<string> lines)
    {
        var result = new SentenceBreakType[0x110000];
        bool missingOther = false;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("# @missing:", StringComparison.Ordinal))
            {
                missingOther |= trimmed[11..].Trim().Equals("0000..10FFFF; Other", StringComparison.Ordinal);
            }

            string data = line.Split('#', 2)[0].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            string[] fields = data.Split(';', StringSplitOptions.TrimEntries);
            string[] endpoints = fields[0].Split("..", StringSplitOptions.None);
            int start = int.Parse(endpoints[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            int end = int.Parse(endpoints[^1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            SentenceBreakType property = Enum.Parse<SentenceBreakType>(fields[1], ignoreCase: false);
            Array.Fill(result, property, start, end - start + 1);
        }

        Assert.IsTrue(missingOther, "The source must declare @missing: Other for the Unicode scalar domain.");
        return result;
    }
}
