// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.HelpersTests;

[TestClass]
public sealed class TextCaseHelperTests
{
    [TestMethod]
    public void ToUpperCase_GermanCulture_PreservesUmlauts()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");

        Assert.AreEqual("FÜR SCHÖNE HÄUSER", TextCaseHelper.ToUpperCase("für schöne häuser", culture));
    }

    [TestMethod]
    public void ToUpperCase_TurkishCulture_UsesDottedCapitalI()
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");

        Assert.AreEqual("İSTANBUL", TextCaseHelper.ToUpperCase("istanbul", culture));
    }

    [TestMethod]
    public void ToUpperCase_ItalianCulture_UsesLatinCapitalI()
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");

        Assert.AreEqual("ISTANBUL", TextCaseHelper.ToUpperCase("istanbul", culture));
    }

    [TestMethod]
    public void ToLowerCase_TurkishCulture_UsesDotlessLowercaseI()
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");

        Assert.AreEqual("ıi", TextCaseHelper.ToLowerCase("Iİ", culture));
    }

    [TestMethod]
    public void ToUpperCase_AzeriCulture_UsesDottedCapitalI()
    {
        var culture = CultureInfo.GetCultureInfo("az-Latn-AZ");

        Assert.AreEqual("İZMİR", TextCaseHelper.ToUpperCase("izmir", culture));
    }

    [TestMethod]
    public void HumanTextCases_DefaultToCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.AreEqual("İSTANBUL", TextCaseHelper.ToUpperCase("istanbul"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void ToTitleCase_NormalizesAllUppercaseInputBeforeTitleCasing()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual("Hello World", TextCaseHelper.ToTitleCase("HELLO WORLD", culture));
    }

    [TestMethod]
    public void ToTitleCase_IsDeterministicNotGrammaticalCorrection()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");

        Assert.AreEqual("Per Anhalter Durch Die Galaxis", TextCaseHelper.ToTitleCase("PER ANHALTER DURCH DIE GALAXIS", culture));
    }

    [TestMethod]
    public void ToSentenceCase_CapitalizesAfterSentenceAndLineBoundaries()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        const string input = "HELLO WORLD. THIS IS ANOTHER SENTENCE!\nTHIRD ONE?";
        const string expected = "Hello world. This is another sentence!\nThird one?";

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [TestMethod]
    public void ToSentenceCase_RecognizesCommonUnicodeSentenceTerminators()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        const string input = "FIRST。 SECOND？ THIRD！ FOURTH";
        const string expected = "First。 Second？ Third！ Fourth";

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [TestMethod]
    public void ToSentenceCase_DoesNotAttemptGermanGrammarRecovery()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");

        Assert.AreEqual("Guten morgen. Mein name ist max.", TextCaseHelper.ToSentenceCase("GUTEN MORGEN. MEIN NAME IST MAX.", culture));
    }

    [DataTestMethod]
    [DataRow("hello. world", "Hello. World")]
    [DataRow("HELLO. WORLD", "Hello. World")]
    [DataRow("Hello. world", "Hello. World")]
    [DataRow("hello world. this is another sentence.", "Hello world. This is another sentence.")]
    [DataRow("she said \"go.\" then left.", "She said \"go.\" Then left.")]
    public void ToSentenceCase_CapitalizesLowercaseSentenceStarts(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [TestMethod]
    public void ToSentenceCase_TurkishCombiningDot_PreservesWholeStringLowercasing()
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        const string contextSensitiveText = "I\u0307STANBUL";
        var expected = "Hello " + contextSensitiveText.ToLower(culture);

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase("HELLO " + contextSensitiveText, culture));
    }

    [DataTestMethod]
    [DataRow("VERSION 3.14 IS READY.", "Version 3.14 is ready.")]
    [DataRow("USE VERSION 1.2.3 NOW.", "Use version 1.2.3 now.")]
    [DataRow("VISIT EXAMPLE.COM FOR INFO.", "Visit example.com for info.")]
    public void ToSentenceCase_DoesNotTreatPeriodsInsideTokensAsSentenceBoundaries(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("VERSION 1.2. NEXT RELEASE.", "Version 1.2. Next release.")]
    [DataRow("VISIT EXAMPLE.COM. NEXT PAGE.", "Visit example.com. Next page.")]
    [DataRow("VISIT WWW.EXAMPLE.COM. NEXT PAGE.", "Visit www.example.com. Next page.")]
    [DataRow("IP 192.168.1.1. NEXT HOP.", "Ip 192.168.1.1. Next hop.")]
    public void ToSentenceCase_DistinguishesDottedInitialismsFromOtherDottedTokens(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    // Adapted from UAX #29 section 5.1.1 rule SB8a.
    [TestMethod]
    public void ToSentenceCase_DoesNotBreakBeforeContinuationPunctuation()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual("Etc., and more.", TextCaseHelper.ToSentenceCase("ETC., AND MORE.", culture));
    }

    [DataTestMethod]
    [DataRow("WHAT!; AND MORE.", "What!; and more.")]
    [DataRow("REALLY?, NO.", "Really?, no.")]
    [DataRow("STOP! — THEN CONTINUE.", "Stop! — then continue.")]
    [DataRow("STOP! NEXT SENTENCE.", "Stop! Next sentence.")]
    [DataRow("REALLY? yes.", "Really? Yes.")]
    public void ToSentenceCase_AppliesSelectedSb8aHandlingToHardTerminators(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("AB.C.D. NEXT SENTENCE.", "Ab.c.d. Next sentence.")]
    [DataRow("EXAMPLE.C.O.M. NEXT SENTENCE.", "Example.c.o.m. Next sentence.")]
    [DataRow("1.A.B. NEXT SENTENCE.", "1.A.b. Next sentence.")]
    [DataRow("U.S.A. is here.", "U.s.a. is here.")]
    [DataRow("A.B. is here.", "A.b. is here.")]
    public void ToSentenceCase_DottedInitialismCandidateDoesNotRestartInsideToken(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("U.S.A. is here.", "U.s.a. is here.")]
    [DataRow("U.S.A. NEXT SENTENCE.", "U.s.a. Next sentence.")]
    [DataRow("U.S.A. 2026 STARTS HERE.", "U.s.a. 2026 Starts here.")]
    [DataRow("U.S.A. \u088F HELLO WORLD.", "U.s.a. \u088F Hello world.")]
    public void ToSentenceCase_CompletedDottedInitialismOnlyContinuesBeforeLower(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("1) HELLO WORLD.", "1) Hello world.")]
    [DataRow("2. HELLO WORLD.", "2. Hello world.")]
    [DataRow("2026 IS HERE.", "2026 Is here.")]
    [DataRow("42: HELLO WORLD.", "42: Hello world.")]
    [DataRow("(2026) HELLO WORLD.", "(2026) Hello world.")]
    [DataRow("123ABC", "123Abc")]
    public void ToSentenceCase_CapitalizesFirstLetterAfterLeadingNumbers(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("\u2160 HELLO WORLD.", "\u2160 Hello world.")]
    [DataRow("\u2160. HELLO WORLD.", "\u2160. Hello world.")]
    [DataRow("\u2163) HELLO WORLD.", "\u2163) Hello world.")]
    [DataRow("123 HELLO WORLD.", "123 Hello world.")]
    [DataRow("\u4E2D\u6587 HELLO WORLD.", "\u4E2D\u6587 hello world.")]
    [DataRow("\u088F HELLO WORLD.", "\u088F Hello world.")]
    public void ToSentenceCase_CapitalizesTheFirstDotNetLetter(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("HELLO\u0589 WORLD.", "Hello\u0589 World.")]
    [DataRow("HELLO.\uFF9E WORLD.", "Hello.\uFF9E World.")]
    [DataRow("HELLO!\u275B WORLD.", "Hello!\u275B World.")]
    [DataRow("VALUE 1.\u066B2 READY. NEXT.", "Value 1.\u066B2 ready. Next.")]
    public void ToSentenceCase_UsesUnicode17SentenceBreakProperties(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("HELLO.🙂NEXT", "Hello.🙂Next")]
    [DataRow("HELLO.$NEXT", "Hello.$Next")]
    [DataRow("HELLO./NEXT", "Hello./Next")]
    [DataRow("HELLO.)NEXT", "Hello.)Next")]
    public void ToSentenceCase_UnknownSymbolsEndDirectPeriodContext(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("HELLO... NEXT", "Hello... Next")]
    [DataRow("HELLO?! NEXT", "Hello?! Next")]
    [DataRow("HELLO!? NEXT", "Hello!? Next")]
    [DataRow("HELLO.\"NEXT\"", "Hello.\"Next\"")]
    [DataRow("HELLO.) NEXT", "Hello.) Next")]
    public void ToSentenceCase_HandlesPunctuationChains(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [DataTestMethod]
    [DataRow("\r")]
    [DataRow("\n")]
    [DataRow("\u0085")]
    [DataRow("\u2028")]
    [DataRow("\u2029")]
    public void ToSentenceCase_RecognizesUnicodeLineBoundaries(string boundary)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual($"Hello{boundary}World", TextCaseHelper.ToSentenceCase($"hello{boundary}world", culture));
    }

    [TestMethod]
    public void ToSentenceCase_TreatsCrLfAsOneLogicalLineBoundary()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual("Hello\r\nWorld", TextCaseHelper.ToSentenceCase("hello\r\nworld", culture));
    }

    [DataTestMethod]
    [DataRow("\u000B")]
    [DataRow("\u000C")]
    public void ToSentenceCase_TreatsClipboardVerticalTabAndFormFeedAsLineBoundaries(string boundary)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual($"Hello{boundary}World", TextCaseHelper.ToSentenceCase($"hello{boundary}world", culture));
    }

    [TestMethod]
    public void ToSentenceCase_HandlesEveryUnicode17AmbiguousTerminator()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        int[] ambiguousTerminators = [0x002E, 0x2024, 0xFE52, 0xFF0E];

        foreach (var value in ambiguousTerminators)
        {
            var terminator = char.ConvertFromUtf32(value);
            Assert.AreEqual($"Hello{terminator} World", TextCaseHelper.ToSentenceCase($"hello{terminator} world", culture));
            Assert.AreEqual($"Version 1{terminator}2 ready", TextCaseHelper.ToSentenceCase($"VERSION 1{terminator}2 READY", culture));
            Assert.AreEqual($"Example{terminator}com ready", TextCaseHelper.ToSentenceCase($"EXAMPLE{terminator}COM READY", culture));
        }
    }

    [TestMethod]
    public void ToSentenceCase_RecognizesRepresentativeUnicode17HardTerminators()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        int[] hardTerminators = [0x0021, 0x003F, 0x061F, 0x06D4, 0x0964, 0x0965, 0x3002, 0xFF01, 0xFF1F];

        foreach (var value in hardTerminators)
        {
            var terminator = char.ConvertFromUtf32(value);
            Assert.AreEqual($"Hello{terminator} World", TextCaseHelper.ToSentenceCase($"hello{terminator} world", culture));
        }
    }

    [TestMethod]
    public void ToSentenceCase_HandlesEveryUnicode17SentenceContinuation()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        int[] sentenceContinuations =
        [
            0x002C, 0x002D, 0x003A, 0x003B, 0x037E, 0x055D, 0x060C, 0x060D,
            0x07F8, 0x1802, 0x1808, 0x2013, 0x2014, 0x3001, 0xFE10, 0xFE11,
            0xFE13, 0xFE14, 0xFE31, 0xFE32, 0xFE50, 0xFE51, 0xFE54, 0xFE55,
            0xFE58, 0xFE63, 0xFF0C, 0xFF0D, 0xFF1A, 0xFF1B, 0xFF64,
        ];

        foreach (var value in sentenceContinuations)
        {
            var continuation = char.ConvertFromUtf32(value);
            Assert.AreEqual($"Stop!{continuation} and more.", TextCaseHelper.ToSentenceCase($"STOP!{continuation} AND MORE.", culture));
        }
    }

    [TestMethod]
    public void ToSentenceCase_IsIdempotentForRepresentativeInputs()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        string[] inputs =
        [
            "Ordinary prose. Another sentence.",
            "lowercase prose. another sentence.",
            "UPPERCASE PROSE. ANOTHER SENTENCE.",
            "Value 3.14. Next value.",
            "VERSION 1.2. NEXT RELEASE.",
            "VISIT WWW.EXAMPLE.COM. NEXT PAGE.",
            "IP 192.168.1.1. NEXT HOP.",
            "U.S.A\u0300. is here.",
            "SHE SAID \"GO.\" THEN LEFT.",
            "HELLO.🙂NEXT",
            "hello\r\nworld",
            "FIRST。 SECOND？ THIRD！ FOURTH",
        ];

        foreach (var input in inputs)
        {
            var transformed = TextCaseHelper.ToSentenceCase(input, culture);
            Assert.AreEqual(transformed, TextCaseHelper.ToSentenceCase(transformed, culture));
        }

        var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
        const string turkishInput = "İSTANBUL. I\u0307ZMİR.";
        var turkishTransformed = TextCaseHelper.ToSentenceCase(turkishInput, turkishCulture);
        Assert.AreEqual(turkishTransformed, TextCaseHelper.ToSentenceCase(turkishTransformed, turkishCulture));
    }

    [TestMethod]
    public void LowerAndUpperCase_MatchDotNetCultureAwareStringCasing()
    {
        string[] cultureNames = ["en-US", "tr-TR", "az-Latn-AZ", "de-DE"];
        string[] inputs =
        [
            "ASCII Text 123!",
            "Iİıi Café München",
            "Σίσυφος Привет",
            "A\u0301 I\u0307 Z\u20DD",
            "𐐀𐐨 😀 中文",
            string.Empty,
            " \t\r\n!?",
        ];

        foreach (var cultureName in cultureNames)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var input in inputs)
            {
                Assert.AreEqual(input.ToLower(culture), TextCaseHelper.ToLowerCase(input, culture));
                Assert.AreEqual(input.ToUpper(culture), TextCaseHelper.ToUpperCase(input, culture));
            }
        }
    }

    [TestMethod]
    public void ToTitleCase_MatchesDocumentedDotNetComposition()
    {
        string[] cultureNames = ["en-US", "tr-TR", "az-Latn-AZ", "de-DE"];
        string[] inputs =
        [
            "ALL UPPERCASE INPUT",
            "mIxEd input",
            "don't stop",
            "hello,world  multiple\tspaces",
            "Iİıi istanbul",
            "Cafe\u0301 ΜÜNCHEN Привет 中文",
            string.Empty,
        ];

        foreach (var cultureName in cultureNames)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var input in inputs)
            {
                var expected = culture.TextInfo.ToTitleCase(input.ToLower(culture));
                Assert.AreEqual(expected, TextCaseHelper.ToTitleCase(input, culture));
            }
        }
    }

    [TestMethod]
    public void ToggleCase_IsReversibleForOneToOneMappings()
    {
        string[] cultureNames = ["en-US", "tr-TR", "az-Latn-AZ", "de-DE"];
        string[] inputs =
        [
            "aBcDeF",
            "ΑβΓδ ПрИвЕт",
            "A\u0301e\u0300 中文 😀 𐐀𐐨",
        ];

        foreach (var cultureName in cultureNames)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            foreach (var input in inputs)
            {
                Assert.AreEqual(input, TextCaseHelper.ToggleCase(TextCaseHelper.ToggleCase(input, culture), culture));
            }
        }

        const string turkishInput = "iIİı";
        foreach (var cultureName in new[] { "tr-TR", "az-Latn-AZ" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            Assert.AreEqual(turkishInput, TextCaseHelper.ToggleCase(TextCaseHelper.ToggleCase(turkishInput, culture), culture));
        }
    }

    [TestMethod]
    public void ToggleCase_NonBijectiveCultureMappingsAreNotReversible()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        const string input = "iIİı";

        Assert.AreNotEqual(input, TextCaseHelper.ToggleCase(TextCaseHelper.ToggleCase(input, culture), culture));
    }

    [TestMethod]
    public void ToLowerCase_PreservesMalformedUtf16CodeUnits()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        foreach (var testCase in GetMalformedHumanTextCases())
        {
            Assert.AreEqual(testCase.Input.ToLower(culture), TextCaseHelper.ToLowerCase(testCase.Input, culture));
        }
    }

    [TestMethod]
    public void ToUpperCase_PreservesMalformedUtf16CodeUnits()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        foreach (var testCase in GetMalformedHumanTextCases())
        {
            Assert.AreEqual(testCase.Input.ToUpper(culture), TextCaseHelper.ToUpperCase(testCase.Input, culture));
        }
    }

    [TestMethod]
    public void ToTitleCase_PreservesMalformedUtf16CodeUnits()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        foreach (var testCase in GetMalformedHumanTextCases())
        {
            Assert.AreEqual(culture.TextInfo.ToTitleCase(testCase.Input.ToLower(culture)), TextCaseHelper.ToTitleCase(testCase.Input, culture));
        }
    }

    [TestMethod]
    public void ToSentenceCase_PreservesMalformedUtf16CodeUnits()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        foreach (var testCase in GetMalformedHumanTextCases())
        {
            Assert.AreEqual(testCase.ExpectedSentence, TextCaseHelper.ToSentenceCase(testCase.Input, culture));
        }
    }

    [TestMethod]
    public void ToggleCase_PreservesMalformedUtf16CodeUnits()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        foreach (var testCase in GetMalformedHumanTextCases())
        {
            Assert.AreEqual(testCase.ExpectedToggle, TextCaseHelper.ToggleCase(testCase.Input, culture));
        }
    }

    [TestMethod]
    public void ToSentenceCase_MalformedUtf16DoesNotConsumeCapitalizationOpportunity()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var malformedHigh = new string(['\uD800']);
        var malformedLow = new string(['\uDC00']);

        Assert.AreEqual($"{malformedHigh} Hello world.", TextCaseHelper.ToSentenceCase($"{malformedHigh} HELLO WORLD.", culture));
        Assert.AreEqual($"{malformedLow}\U00010400 hello world.", TextCaseHelper.ToSentenceCase($"{malformedLow}\U00010400 HELLO WORLD.", culture));
    }

    [DataTestMethod]
    [DataRow("123helloWorld", "123helloWorld", "123HelloWorld", "123hello_world", "123HELLO_WORLD", "123hello-world")]
    [DataRow("hello123", "hello123", "Hello123", "hello123", "HELLO123", "hello123")]
    [DataRow("123", "123", "123", "123", "123", "123")]
    [DataRow("--hello__world", "helloWorld", "HelloWorld", "hello_world", "HELLO_WORLD", "hello-world")]
    [DataRow("!😀$", "", "", "", "", "")]
    [DataRow("hello😀world", "helloWorld", "HelloWorld", "hello_world", "HELLO_WORLD", "hello-world")]
    [DataRow("Cafe\u0301 Noir", "cafe\u0301Noir", "Cafe\u0301Noir", "cafe\u0301_noir", "CAFE\u0301_NOIR", "cafe\u0301-noir")]
    [DataRow("JSON2XML", "json2Xml", "Json2Xml", "json2_xml", "JSON2_XML", "json2-xml")]
    [DataRow("你好World", "你好World", "你好World", "你好_world", "你好_WORLD", "你好-world")]
    public void IdentifierCases_HandleAdversarialTokenShapes(
        string input,
        string camel,
        string pascal,
        string snake,
        string screamingSnake,
        string kebab)
    {
        Assert.AreEqual(camel, TextCaseHelper.ToCamelCase(input));
        Assert.AreEqual(pascal, TextCaseHelper.ToPascalCase(input));
        Assert.AreEqual(snake, TextCaseHelper.ToSnakeCase(input));
        Assert.AreEqual(screamingSnake, TextCaseHelper.ToScreamingSnakeCase(input));
        Assert.AreEqual(kebab, TextCaseHelper.ToKebabCase(input));
    }

    [TestMethod]
    public void IdentifierCases_TreatMalformedUtf16CodeUnitsAsSeparators()
    {
        var isolatedHigh = new string(['\uD800']);
        var isolatedLow = new string(['\uDC00']);
        string[] inputs =
        [
            $"{isolatedHigh}hello world",
            $"{isolatedLow}hello world",
            $"hello{isolatedHigh}world",
            $"hello{isolatedLow}\U0001F642world",
        ];

        foreach (var input in inputs)
        {
            Assert.AreEqual("helloWorld", TextCaseHelper.ToCamelCase(input));
            Assert.AreEqual("HelloWorld", TextCaseHelper.ToPascalCase(input));
            Assert.AreEqual("hello_world", TextCaseHelper.ToSnakeCase(input));
            Assert.AreEqual("HELLO_WORLD", TextCaseHelper.ToScreamingSnakeCase(input));
            Assert.AreEqual("hello-world", TextCaseHelper.ToKebabCase(input));
        }
    }

    [TestMethod]
    public void IdentifierCases_AreIndependentOfCurrentCultureAcrossSupportedCultures()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        const string input = "Iİıi JSON2XML München 你好World";
        string[] cultureNames = ["en-US", "tr-TR", "az-Latn-AZ", "de-DE"];

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureNames[0]);
            string[] expected =
            [
                TextCaseHelper.ToCamelCase(input),
                TextCaseHelper.ToPascalCase(input),
                TextCaseHelper.ToSnakeCase(input),
                TextCaseHelper.ToScreamingSnakeCase(input),
                TextCaseHelper.ToKebabCase(input),
            ];

            foreach (var cultureName in cultureNames)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                CollectionAssert.AreEqual(
                    expected,
                    new[]
                    {
                        TextCaseHelper.ToCamelCase(input),
                        TextCaseHelper.ToPascalCase(input),
                        TextCaseHelper.ToSnakeCase(input),
                        TextCaseHelper.ToScreamingSnakeCase(input),
                        TextCaseHelper.ToKebabCase(input),
                    });
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    // Patterns adapted from Unicode Standard Annex #29
    // (Unicode 17.0.0, revision 47), section 5.1.1, rules SB5-SB11,
    // and the Unicode 17.0 SentenceBreakTest data.
    [DataTestMethod]
    [DataRow("CODE C.D IS VALID.", "Code c.d is valid.")]
    [DataRow("U.S.A\u0300. is here.", "U.s.a\u0300. is here.")]
    [DataRow("U.S.A\u0300? HE IS HERE.", "U.s.a\u0300? He is here.")]
    [DataRow("SHE SAID \"GO.\" THEN LEFT.", "She said \"go.\" Then left.")]
    public void ToSentenceCase_HandlesSelectedUnicodeSentenceBoundaryPatterns(string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(expected, TextCaseHelper.ToSentenceCase(input, culture));
    }

    [TestMethod]
    public void ToSentenceCase_CapitalizesAfterClosingQuoteWithoutWhitespace()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual(
            "Hello.\"Next sentence\"",
            TextCaseHelper.ToSentenceCase("HELLO.\"NEXT SENTENCE\"", culture));
    }

    [TestMethod]
    public void ToggleCase_TurkishCulture_UsesCultureSpecificMappings()
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");

        Assert.AreEqual("İıiI", TextCaseHelper.ToggleCase("iIİı", culture));
    }

    [TestMethod]
    public void ToggleCase_PreservesSupplementaryUnicodeCharacters()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        Assert.AreEqual("A😀b", TextCaseHelper.ToggleCase("a😀B", culture));
    }

    [TestMethod]
    public void HumanTextCases_HandleCyrillicAndPreserveUncasedScripts()
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");

        Assert.AreEqual("ПРИВЕТ МИР", TextCaseHelper.ToUpperCase("Привет Мир", culture));
        Assert.AreEqual("مرحبا بالعالم 中文", TextCaseHelper.ToUpperCase("مرحبا بالعالم 中文", culture));
    }

    [TestMethod]
    [DataRow("hello world", "helloWorld")]
    [DataRow("hello-world", "helloWorld")]
    [DataRow("hello_world", "helloWorld")]
    [DataRow("helloWorld", "helloWorld")]
    [DataRow("HelloWorld", "helloWorld")]
    [DataRow("XMLHttpRequest", "xmlHttpRequest")]
    [DataRow("HTTP server", "httpServer")]
    [DataRow("version2Test", "version2Test")]
    [DataRow("XML2Parser", "xml2Parser")]
    [DataRow("München Hauptbahnhof", "münchenHauptbahnhof")]
    public void ToCamelCase_TokenizesCommonIdentifierStyles(string input, string expected)
    {
        Assert.AreEqual(expected, TextCaseHelper.ToCamelCase(input));
    }

    [TestMethod]
    [DataRow("hello world", "HelloWorld")]
    [DataRow("XMLHttpRequest", "XmlHttpRequest")]
    [DataRow("version2Test", "Version2Test")]
    [DataRow("München Hauptbahnhof", "MünchenHauptbahnhof")]
    public void ToPascalCase_TokenizesCommonIdentifierStyles(string input, string expected)
    {
        Assert.AreEqual(expected, TextCaseHelper.ToPascalCase(input));
    }

    [TestMethod]
    [DataRow("hello world", "hello_world")]
    [DataRow("XMLHttpRequest", "xml_http_request")]
    [DataRow("version2Test", "version2_test")]
    [DataRow("München Hauptbahnhof", "münchen_hauptbahnhof")]
    public void ToSnakeCase_TokenizesCommonIdentifierStyles(string input, string expected)
    {
        Assert.AreEqual(expected, TextCaseHelper.ToSnakeCase(input));
    }

    [TestMethod]
    [DataRow("hello world", "HELLO_WORLD")]
    [DataRow("XMLHttpRequest", "XML_HTTP_REQUEST")]
    [DataRow("München Hauptbahnhof", "MÜNCHEN_HAUPTBAHNHOF")]
    public void ToScreamingSnakeCase_TokenizesCommonIdentifierStyles(string input, string expected)
    {
        Assert.AreEqual(expected, TextCaseHelper.ToScreamingSnakeCase(input));
    }

    [TestMethod]
    [DataRow("hello world", "hello-world")]
    [DataRow("XMLHttpRequest", "xml-http-request")]
    [DataRow("München Hauptbahnhof", "münchen-hauptbahnhof")]
    public void ToKebabCase_TokenizesCommonIdentifierStyles(string input, string expected)
    {
        Assert.AreEqual(expected, TextCaseHelper.ToKebabCase(input));
    }

    [TestMethod]
    [DataRow("\u0301foo", "foo", "Foo", "foo", "FOO", "foo")]
    [DataRow("foo-\u0301bar", "fooBar", "FooBar", "foo_bar", "FOO_BAR", "foo-bar")]
    [DataRow("foo \u0301 bar", "fooBar", "FooBar", "foo_bar", "FOO_BAR", "foo-bar")]
    [DataRow("foo-\u0301-bar", "fooBar", "FooBar", "foo_bar", "FOO_BAR", "foo-bar")]
    public void IdentifierCases_DoNotMigrateOrphanCombiningMarks(
        string input,
        string expectedCamel,
        string expectedPascal,
        string expectedSnake,
        string expectedScreamingSnake,
        string expectedKebab)
    {
        Assert.AreEqual(expectedCamel, TextCaseHelper.ToCamelCase(input));
        Assert.AreEqual(expectedPascal, TextCaseHelper.ToPascalCase(input));
        Assert.AreEqual(expectedSnake, TextCaseHelper.ToSnakeCase(input));
        Assert.AreEqual(expectedScreamingSnake, TextCaseHelper.ToScreamingSnakeCase(input));
        Assert.AreEqual(expectedKebab, TextCaseHelper.ToKebabCase(input));
    }

    [TestMethod]
    public void IdentifierCases_PreserveUncasedUnicodeScripts()
    {
        Assert.AreEqual("中文_测试", TextCaseHelper.ToSnakeCase("中文 测试"));
    }

    [TestMethod]
    public void EmptyInput_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, TextCaseHelper.ToLowerCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToUpperCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToTitleCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToSentenceCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToggleCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToCamelCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToPascalCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToSnakeCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToScreamingSnakeCase(string.Empty));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToKebabCase(string.Empty));
    }

    [TestMethod]
    public void IdentifierCases_SeparatorOnlyInput_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, TextCaseHelper.ToCamelCase(" _- / "));
        Assert.AreEqual(string.Empty, TextCaseHelper.ToSnakeCase(" _- / "));
    }

    [TestMethod]
    public void NullInput_ThrowsForAllTransformations()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToLowerCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToUpperCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToTitleCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToSentenceCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToggleCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToCamelCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToPascalCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToSnakeCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToScreamingSnakeCase(null));
        Assert.ThrowsExactly<ArgumentNullException>(() => TextCaseHelper.ToKebabCase(null));
    }

    private static (string Input, string ExpectedSentence, string ExpectedToggle)[] GetMalformedHumanTextCases()
    {
        var isolatedHigh = new string(['\uD800']);
        var isolatedLow = new string(['\uDC00']);

        return
        [
            (isolatedHigh, isolatedHigh, isolatedHigh),
            (isolatedLow, isolatedLow, isolatedLow),
            ($"HELLO{isolatedHigh}WORLD", $"Hello{isolatedHigh}world", $"hello{isolatedHigh}world"),
            ($"A\U0001F642{isolatedLow}B", $"A\U0001F642{isolatedLow}b", $"a\U0001F642{isolatedLow}b"),
        ];
    }
}
