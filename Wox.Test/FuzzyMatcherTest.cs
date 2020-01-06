using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;
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

        [TestCase]
        public void WhenGivenStringsForCalScoreMethodThenShouldReturnCurrentScoring()
        {
            // Arrange
            string searchTerm = "chrome"; // since this looks for specific results it will always be one case
            var searchStrings = new List<string>
            {
                Chrome,//SCORE: 107
                LastIsChrome,//SCORE: 53
                HelpCureHopeRaiseOnMindEntityChrome,//SCORE: 21
                UninstallOrChangeProgramsOnYourComputer, //SCORE: 15
                CandyCrushSagaFromKing//SCORE: 0
            }
            .OrderByDescending(x => x)
            .ToList();

            // Act
            var results = new List<Result>();
            foreach (var str in searchStrings)
            {
                results.Add(new Result
                {
                    Title = str,
                    Score = StringMatcher.FuzzySearch(searchTerm, str).RawScore
                });
            }

            // Assert
            VerifyResult(147, Chrome);
            VerifyResult(93, LastIsChrome);
            VerifyResult(41, HelpCureHopeRaiseOnMindEntityChrome);
            VerifyResult(35, UninstallOrChangeProgramsOnYourComputer);
            VerifyResult(0, CandyCrushSagaFromKing);

            void VerifyResult(int expectedScore, string expectedTitle)
            {
                var result = results.FirstOrDefault(x => x.Title == expectedTitle);
                if (result == null)
                {
                    Assert.Fail($"Fail to find result: {expectedTitle} in result list");
                }

                Assert.AreEqual(expectedScore, result.Score, $"Expected score for {expectedTitle}: {expectedScore}, Actual: {result.Score}");
            }
        }

        [TestCase("goo", "Google Chrome", (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Google Chrome", (int)StringMatcher.SearchPrecisionScore.Low, true)]
        [TestCase("chr", "Chrome", (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Help cure hope raise on mind entity Chrome", (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Help cure hope raise on mind entity Chrome", (int)StringMatcher.SearchPrecisionScore.Low, true)]
        [TestCase("chr", "Candy Crush Saga from King", (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Candy Crush Saga from King", (int)StringMatcher.SearchPrecisionScore.None, true)]
        [TestCase("ccs", "Candy Crush Saga from King", (int)StringMatcher.SearchPrecisionScore.Low, true)]
        [TestCase("cand", "Candy Crush Saga from King", (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("cand", "Help cure hope raise on mind entity Chrome", (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        public void WhenGivenDesiredPrecisionThenShouldReturnAllResultsGreaterOrEqual(
            string queryString,
            string compareString,
            int expectedPrecisionScore,
            bool expectedPrecisionResult)
        {
            // Arrange
            var expectedPrecisionString = (StringMatcher.SearchPrecisionScore)expectedPrecisionScore;
            StringMatcher.UserSettingSearchPrecision = expectedPrecisionScore; // this is why static state is evil...

            // Act
            var matchResult = StringMatcher.FuzzySearch(queryString, compareString);

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine($"RAW SCORE: {matchResult.RawScore.ToString()}, PrecisionLevelSetAt: {expectedPrecisionString} ({expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            // Assert
            Assert.AreEqual(expectedPrecisionResult, matchResult.IsSearchPrecisionScoreMet(),
                $"Query:{queryString}{Environment.NewLine} " +
                $"Compare:{compareString}{Environment.NewLine}" +
                $"Raw Score: {matchResult.RawScore}{Environment.NewLine}" +
                $"Precision Level: {expectedPrecisionString}={expectedPrecisionScore}");
        }

        [TestCase("exce", "OverLeaf-Latex: An online LaTeX editor", (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("term", "Windows Terminal (Preview)", (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql s managa", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("sql' s manag", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("sql s manag", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql manag", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql serv", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("sql studio", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("mic", MicrosoftSqlServerManagementStudio, (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("chr", "Shutdown", (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("chr", "Change settings for text-to-speech and for speech recognition (if installed).", (int)StringMatcher.SearchPrecisionScore.Regular, false)]
        [TestCase("a test", "This is a test", (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        [TestCase("test", "This is a test", (int)StringMatcher.SearchPrecisionScore.Regular, true)]
        public void WhenGivenQueryShouldReturnResultsContainingAllQuerySubstrings(
            string queryString,
            string compareString,
            int expectedPrecisionScore,
            bool expectedPrecisionResult)
        {
            // Arrange
            var expectedPrecisionString = (StringMatcher.SearchPrecisionScore)expectedPrecisionScore;
            StringMatcher.UserSettingSearchPrecision = expectedPrecisionScore; // this is why static state is evil...

            // Act
            var matchResult = StringMatcher.FuzzySearch(queryString, compareString);

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"QueryString: {queryString}     CompareString: {compareString}");
            Debug.WriteLine($"RAW SCORE: {matchResult.RawScore.ToString()}, PrecisionLevelSetAt: {expectedPrecisionString} ({expectedPrecisionScore})");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            // Assert
            Assert.AreEqual(expectedPrecisionResult, matchResult.IsSearchPrecisionScoreMet(),
                $"Query:{queryString}{Environment.NewLine} " +
                $"Compare:{compareString}{Environment.NewLine}" +
                $"Raw Score: {matchResult.RawScore}{Environment.NewLine}" +
                $"Precision Level: {expectedPrecisionString}={expectedPrecisionScore}");
        }
    }
}