// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Wox.Test
{
    [TestClass]
    public class FuzzyMatcherTest
    {
        private const string Chrome = "Chrome";
        private const string CandyCrushSagaFromKing = "Candy Crush Saga from King";
        private const string HelpCureHopeRaiseOnMindEntityChrome = "Help cure hope raise on mind entity Chrome";
        private const string UninstallOrChangeProgramsOnYourComputer = "Uninstall or change programs on your computer";
        private const string LastIsChrome = "Last is chrome";
        private const string OneOneOneOne = "1111";
        private const string MicrosoftSqlServerManagementStudio = "Microsoft SQL Server Management Studio";

        public static List<string> GetSearchStrings()
            => new List<string>
            {
                Chrome,
                "Choose which programs you want Windows to use for activities like web browsing, editing photos, sending e-mail, and playing music.",
                HelpCureHopeRaiseOnMindEntityChrome,
                CandyCrushSagaFromKing,
                UninstallOrChangeProgramsOnYourComputer,
                "Add, change, and manage fonts on your computer",
                LastIsChrome,
                OneOneOneOne,
            };

        public static List<int> GetPrecisionScores()
        {
            var listToReturn = new List<int>();

            Enum.GetValues(typeof(StringMatcher.SearchPrecisionScore))
                .Cast<StringMatcher.SearchPrecisionScore>()
                .ToList()
                .ForEach(x => listToReturn.Add((int)x));

            return listToReturn;
        }

        [TestMethod]
        public void MatchTest()
        {
            var sources = new List<string>
            {
                "file open in browser-test",
                "Install Package",
                "add new bsd",
                "Inste",
                "aac",
            };

            var results = new List<Result>();
            var matcher = new StringMatcher();
            foreach (var str in sources)
            {
                results.Add(new Result
                {
                    Title = str,
                    Score = matcher.FuzzyMatch("inst", str).RawScore,
                });
            }

            results = results.Where(x => x.Score > 0).OrderByDescending(x => x.Score).ToList();

            Assert.IsTrue(results.Count == 3);
            Assert.IsTrue(results[0].Title == "Inste");
            Assert.IsTrue(results[1].Title == "Install Package");
            Assert.IsTrue(results[2].Title == "file open in browser-test");
        }

        [DataTestMethod]
        [DataRow("Chrome")]
        public void WhenGivenNotAllCharactersFoundInSearchStringThenShouldReturnZeroScore(string searchString)
        {
            var compareString = "Can have rum only in my glass";
            var matcher = new StringMatcher();
            var scoreResult = matcher.FuzzyMatch(searchString, compareString).RawScore;

            Assert.IsTrue(scoreResult == 0);
        }

        [DataTestMethod]
        [DataRow("chr")]
        [DataRow("chrom")]
        [DataRow("chrome")]
        [DataRow("cand")]
        [DataRow("cpywa")]
        [DataRow("ccs")]
        public void WhenGivenStringsAndAppliedPrecisionFilteringThenShouldReturnGreaterThanPrecisionScoreResults(string searchTerm)
        {
            var results = new List<Result>();
            var matcher = new StringMatcher();
            foreach (var str in GetSearchStrings())
            {
                results.Add(new Result
                {
                    Title = str,
                    Score = matcher.FuzzyMatch(searchTerm, str).Score,
                });
            }

            foreach (var precisionScore in GetPrecisionScores())
            {
                var filteredResult = results.Where(result => result.Score >= precisionScore).Select(result => result).OrderByDescending(x => x.Score).ToList();

                Debug.WriteLine(string.Empty);
                Debug.WriteLine("###############################################");
                Debug.WriteLine("SEARCHTERM: " + searchTerm + ", GreaterThanSearchPrecisionScore: " + precisionScore);
                foreach (var item in filteredResult)
                {
                    // Using InvariantCulture since this is used for testing
                    Debug.WriteLine("SCORE: " + item.Score.ToString(CultureInfo.InvariantCulture) + ", FoundString: " + item.Title);
                }

                Debug.WriteLine("###############################################");
                Debug.WriteLine(string.Empty);

                Assert.IsFalse(filteredResult.Any(x => x.Score < precisionScore));
            }
        }

        [DataTestMethod]
        [DataRow("vim", "Vim", "ignoreDescription", "ignore.exe", "Vim Diff", "ignoreDescription", "ignore.exe")]
        public void WhenMultipleResultsExactMatchingResultShouldHaveGreatestScore(string queryString, string firstName, string firstDescription, string firstExecutableName, string secondName, string secondDescription, string secondExecutableName)
        {
            // Act
            var matcher = new StringMatcher();
            var firstNameMatch = matcher.FuzzyMatch(queryString, firstName).RawScore;
            var firstDescriptionMatch = matcher.FuzzyMatch(queryString, firstDescription).RawScore;
            var firstExecutableNameMatch = matcher.FuzzyMatch(queryString, firstExecutableName).RawScore;

            var secondNameMatch = matcher.FuzzyMatch(queryString, secondName).RawScore;
            var secondDescriptionMatch = matcher.FuzzyMatch(queryString, secondDescription).RawScore;
            var secondExecutableNameMatch = matcher.FuzzyMatch(queryString, secondExecutableName).RawScore;

            var firstScore = new[] { firstNameMatch, firstDescriptionMatch, firstExecutableNameMatch }.Max();
            var secondScore = new[] { secondNameMatch, secondDescriptionMatch, secondExecutableNameMatch }.Max();

            // Assert
            Assert.IsTrue(firstScore > secondScore);
        }

        [DataTestMethod]
        [DataRow("goo", "Google Chrome", StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("chr", "Google Chrome", StringMatcher.SearchPrecisionScore.Low, true)]
        [DataRow("chr", "Chrome", StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("chr", "Help cure hope raise on mind entity Chrome", StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("chr", "Help cure hope raise on mind entity Chrome", StringMatcher.SearchPrecisionScore.Low, true)]
        [DataRow("chr", "Candy Crush Saga from King", StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("chr", "Candy Crush Saga from King", StringMatcher.SearchPrecisionScore.None, true)]
        [DataRow("ccs", "Candy Crush Saga from King", StringMatcher.SearchPrecisionScore.Low, true)]
        [DataRow("cand", "Candy Crush Saga from King", StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("cand", "Help cure hope raise on mind entity Chrome", StringMatcher.SearchPrecisionScore.Regular, false)]
        public void WhenGivenDesiredPrecisionThenShouldReturnAllResultsGreaterOrEqual(
            string queryString,
            string compareString,
            StringMatcher.SearchPrecisionScore expectedPrecisionScore,
            bool expectedPrecisionResult)
        {
            // When
            var matcher = new StringMatcher { UserSettingSearchPrecision = expectedPrecisionScore };

            // Given
            var matchResult = matcher.FuzzyMatch(queryString, compareString);

            Debug.WriteLine(string.Empty);
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine($"RAW SCORE: {matchResult.RawScore}, PrecisionLevelSetAt: {expectedPrecisionScore} ({(int)expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine(string.Empty);

            // Should
            Assert.AreEqual(
                expectedPrecisionResult,
                matchResult.IsSearchPrecisionScoreMet(),
                $"{$"Query:{queryString}{Environment.NewLine} "}{$"Compare:{compareString}{Environment.NewLine}"}{$"Raw Score: {matchResult.RawScore}{Environment.NewLine}"}{$"Precision Score: {(int)expectedPrecisionScore}"}");
        }

        [DataTestMethod]
        [DataRow("exce", "OverLeaf-Latex: An online LaTeX editor", StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("term", "Windows Terminal (Preview)", StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("sql s managa", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("sql' s manag", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("sql s manag", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("sql manag", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("sql", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("sql serv", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("sql serv man", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("sql studio", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("mic", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("chr", "Shutdown", StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("mssms", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("chr", "Change settings for text-to-speech and for speech recognition (if installed).", StringMatcher.SearchPrecisionScore.Regular, false)]
        [DataRow("ch r", "Change settings for text-to-speech and for speech recognition (if installed).", StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("a test", "This is a test", StringMatcher.SearchPrecisionScore.Regular, true)]
        [DataRow("test", "This is a test", StringMatcher.SearchPrecisionScore.Regular, true)]
        public void WhenGivenQueryShouldReturnResultsContainingAllQuerySubstrings(
            string queryString,
            string compareString,
            StringMatcher.SearchPrecisionScore expectedPrecisionScore,
            bool expectedPrecisionResult)
        {
            // When
            var matcher = new StringMatcher { UserSettingSearchPrecision = expectedPrecisionScore };

            // Given
            var matchResult = matcher.FuzzyMatch(queryString, compareString);

            Debug.WriteLine(string.Empty);
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine($"RAW SCORE: {matchResult.RawScore}, PrecisionLevelSetAt: {expectedPrecisionScore} ({(int)expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine(string.Empty);

            // Should
            Assert.AreEqual(
                expectedPrecisionResult,
                matchResult.IsSearchPrecisionScoreMet(),
                $"{$"Query:{queryString}{Environment.NewLine} "}{$"Compare:{compareString}{Environment.NewLine}"}{$"Raw Score: {matchResult.RawScore}{Environment.NewLine}"}{$"Precision Score: {(int)expectedPrecisionScore}"}");
        }

        [DataTestMethod]
        [DataRow("Windows Terminal", "Windows_Terminal", "term")]
        [DataRow("Windows Terminal", "WindowsTerminal", "term")]
        public void FuzzyMatchingScoreShouldBeHigherWhenPreceedingCharacterIsSpace(string firstCompareStr, string secondCompareStr, string query)
        {
            // Arrange
            var matcher = new StringMatcher();

            // Act
            var firstScore = matcher.FuzzyMatch(query, firstCompareStr).Score;
            var secondScore = matcher.FuzzyMatch(query, secondCompareStr).Score;

            // Assert
            Assert.IsTrue(firstScore > secondScore);
        }
    }
}
