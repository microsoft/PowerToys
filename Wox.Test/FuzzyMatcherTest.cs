using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Wox.Test
{
    [TestFixture]
    public class FuzzyMatcherTest
    {
        private const string Chrome = "Chrome";
        private const string CandyCrushSagaFromKing = "Candy Crush Saga from King";
        private const string HelpCureHopeRaiseOnMindEntityChrome = "Help cure hope raise on mind entity Chrome";
        private const string UninstallOrChangeProgramsOnYourComputer = "Uninstall or change programs on your computer";
        private const string LastIsChrome = "Last is chrome";
        private const string OneOneOneOne = "1111";
        private const string MicrosoftSqlServerManagementStudio = "Microsoft SQL Server Management Studio";

        public List<string> GetSearchStrings()
            => new List<string>
            {
                Chrome,
                "Choose which programs you want Windows to use for activities like web browsing, editing photos, sending e-mail, and playing music.",
                HelpCureHopeRaiseOnMindEntityChrome,
                CandyCrushSagaFromKing,
                UninstallOrChangeProgramsOnYourComputer,
                "Add, change, and manage fonts on your computer",
                LastIsChrome,
                OneOneOneOne
            };

        public List<int> GetPrecisionScores()
        {
            var listToReturn = new List<int>();

            Enum.GetValues(typeof(StringMatcher.SearchPrecisionScore))
                .Cast<StringMatcher.SearchPrecisionScore>()
                .ToList()
                .ForEach(x => listToReturn.Add((int)x));

            return listToReturn;
        }

        [Test]
        public void MatchTest()
        {
            var sources = new List<string>
            {
                "file open in browser-test",
                "Install Package",
                "add new bsd",
                "Inste",
                "aac"
            };

            var results = new List<Result>();
            foreach (var str in sources)
            {
                results.Add(new Result
                {
                    Title = str,
                    Score = StringMatcher.FuzzySearch("inst", str).RawScore
                });
            }

            results = results.Where(x => x.Score > 0).OrderByDescending(x => x.Score).ToList();

            Assert.IsTrue(results.Count == 3);
            Assert.IsTrue(results[0].Title == "Inste");
            Assert.IsTrue(results[1].Title == "Install Package");
            Assert.IsTrue(results[2].Title == "file open in browser-test");
        }

        [TestCase("Chrome")]
        public void WhenGivenNotAllCharactersFoundInSearchStringThenShouldReturnZeroScore(string searchString)
        {
            var compareString = "Can have rum only in my glass";

            var scoreResult = StringMatcher.FuzzySearch(searchString, compareString).RawScore;

            Assert.True(scoreResult == 0);
        }

        [TestCase("chr")]
        [TestCase("chrom")]
        [TestCase("chrome")]
        [TestCase("cand")]
        [TestCase("cpywa")]
        [TestCase("ccs")]
        public void WhenGivenStringsAndAppliedPrecisionFilteringThenShouldReturnGreaterThanPrecisionScoreResults(string searchTerm)
        {
            var results = new List<Result>();

            foreach (var str in GetSearchStrings())
            {
                results.Add(new Result
                {
                    Title = str,
                    Score = StringMatcher.FuzzySearch(searchTerm, str).Score
                });
            }

            foreach (var precisionScore in GetPrecisionScores())
            {
                var filteredResult = results.Where(result => result.Score >= precisionScore).Select(result => result).OrderByDescending(x => x.Score).ToList();

                Debug.WriteLine("");
                Debug.WriteLine("###############################################");
                Debug.WriteLine("SEARCHTERM: " + searchTerm + ", GreaterThanSearchPrecisionScore: " + precisionScore);
                foreach (var item in filteredResult)
                {
                    Debug.WriteLine("SCORE: " + item.Score.ToString() + ", FoundString: " + item.Title);
                }
                Debug.WriteLine("###############################################");
                Debug.WriteLine("");

                Assert.IsFalse(filteredResult.Any(x => x.Score < precisionScore));
            }
        }

        [TestCase(Chrome, Chrome, 167)]
        [TestCase(Chrome, LastIsChrome, 113)]
        [TestCase(Chrome, HelpCureHopeRaiseOnMindEntityChrome, 21)]
        [TestCase(Chrome, UninstallOrChangeProgramsOnYourComputer, 15)]
        [TestCase(Chrome, CandyCrushSagaFromKing, 0)]
        [TestCase("sql", MicrosoftSqlServerManagementStudio, 56)]
        [TestCase("sql  manag", MicrosoftSqlServerManagementStudio, 119)]//double spacing intended
        public void WhenGivenQueryStringThenShouldReturnCurrentScoring(string queryString, string compareString, int expectedScore)
        {
            // When, Given
            var rawScore = StringMatcher.FuzzySearch(queryString, compareString).RawScore;

            // Should
            Assert.AreEqual(expectedScore, rawScore, $"Expected score for compare string '{compareString}': {expectedScore}, Actual: {rawScore}");
        }

        [TestCase("goo", "Google Chrome", StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Google Chrome", StringMatcher.SearchPrecisionScore.Low, true)]
        [TestCase("chr", "Chrome", StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Help cure hope raise on mind entity Chrome", StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Help cure hope raise on mind entity Chrome", StringMatcher.SearchPrecisionScore.Low, true)]
        [TestCase("chr", "Candy Crush Saga from King", StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Candy Crush Saga from King", StringMatcher.SearchPrecisionScore.None, true)]
        [TestCase("ccs", "Candy Crush Saga from King", StringMatcher.SearchPrecisionScore.Low, true)]
        [TestCase("cand", "Candy Crush Saga from King",StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("cand", "Help cure hope raise on mind entity Chrome", StringMatcher.SearchPrecisionScore.Regular, false)]
        public void WhenGivenDesiredPrecisionThenShouldReturnAllResultsGreaterOrEqual(
            string queryString,
            string compareString,
            StringMatcher.SearchPrecisionScore expectedPrecisionScore,
            bool expectedPrecisionResult)
        {
            // When            
            StringMatcher.UserSettingSearchPrecision = expectedPrecisionScore;

            // Given
            var matchResult = StringMatcher.FuzzySearch(queryString, compareString);

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine($"RAW SCORE: {matchResult.RawScore.ToString()}, PrecisionLevelSetAt: {expectedPrecisionScore} ({(int)expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            // Should
            Assert.AreEqual(expectedPrecisionResult, matchResult.IsSearchPrecisionScoreMet(),
                $"Query:{queryString}{Environment.NewLine} " +
                $"Compare:{compareString}{Environment.NewLine}" +
                $"Raw Score: {matchResult.RawScore}{Environment.NewLine}" +
                $"Precision Score: {(int)expectedPrecisionScore}");
        }

        [TestCase("exce", "OverLeaf-Latex: An online LaTeX editor", StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("term", "Windows Terminal (Preview)", StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql s managa", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("sql' s manag", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("sql s manag", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql manag", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql serv", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql studio", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("mic", MicrosoftSqlServerManagementStudio, StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Shutdown", StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Change settings for text-to-speech and for speech recognition (if installed).", StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("a test", "This is a test", StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("test", "This is a test", StringMatcher.SearchPrecisionScore.Regular, true)]
        public void WhenGivenQueryShouldReturnResultsContainingAllQuerySubstrings(
            string queryString,
            string compareString,
            StringMatcher.SearchPrecisionScore expectedPrecisionScore,
            bool expectedPrecisionResult)
        {
            // When
            StringMatcher.UserSettingSearchPrecision = expectedPrecisionScore;

            // Given
            var matchResult = StringMatcher.FuzzySearch(queryString, compareString);

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine($"RAW SCORE: {matchResult.RawScore.ToString()}, PrecisionLevelSetAt: {expectedPrecisionScore} ({(int)expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            // Should
            Assert.AreEqual(expectedPrecisionResult, matchResult.IsSearchPrecisionScoreMet(),
                $"Query:{queryString}{Environment.NewLine} " +
                $"Compare:{compareString}{Environment.NewLine}" +
                $"Raw Score: {matchResult.RawScore}{Environment.NewLine}" +
                $"Precision Score: {(int)expectedPrecisionScore}");
        }

        [TestCase("sql servman", MicrosoftSqlServerManagementStudio, false)]
        [TestCase("sql serv man", MicrosoftSqlServerManagementStudio, true)]
        [TestCase("sql", MicrosoftSqlServerManagementStudio, true)]
        [TestCase("sqlserv", MicrosoftSqlServerManagementStudio, false)]
        [TestCase("mssms", MicrosoftSqlServerManagementStudio, false)]
        [TestCase("chr", "Change settings for text-to-speech and for speech recognition (if installed).", false)]
        [TestCase("ch r", "Change settings for text-to-speech and for speech recognition (if installed).", true)]
        public void WhenGivenQueryShouldEvaluateTrueFalseIfCompareStringContainsAllSubstrings(string queryString, string compareString, bool expectedResult)
        {
            // When, Given
            var matchResult = StringMatcher.FuzzySearch(queryString, compareString).AllSubstringsContainedInCompareString;

            // Should
            Assert.AreEqual(matchResult, expectedResult);
        }
    }
}