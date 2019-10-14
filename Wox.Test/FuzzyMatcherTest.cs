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
        public List<string> GetSearchStrings() 
            => new List<string>
            {
                "Chrome",
                "Choose which programs you want Windows to use for activities like web browsing, editing photos, sending e-mail, and playing music.",
                "Help cure hope raise on mind entity Chrome ",
                "Candy Crush Saga from King",
                "Uninstall or change programs on your computer",
                "Add, change, and manage fonts on your computer",
                "Last is chrome",
                "1111"
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
                    Score = StringMatcher.FuzzySearch("inst", str, new MatchOption()).Score
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

            var scoreResult = StringMatcher.FuzzySearch(searchString, compareString, new MatchOption()).Score;

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
                    Score = StringMatcher.FuzzySearch(searchTerm, str, new MatchOption()).Score
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

        [TestCase("chrome")]
        public void WhenGivenStringsForCalScoreMethodThenShouldReturnCurrentScoring(string searchTerm)
        {
            var searchStrings = new List<string>
            {
                "Chrome",//SCORE: 107
                "Last is chrome",//SCORE: 53
                "Help cure hope raise on mind entity Chrome",//SCORE: 21
                "Uninstall or change programs on your computer", //SCORE: 15
                "Candy Crush Saga from King"//SCORE: 0
            }
            .OrderByDescending(x => x)
            .ToList();

            var results = new List<Result>();

            foreach (var str in searchStrings)
            {
                results.Add(new Result
                {
                    Title = str,
                    Score = StringMatcher.FuzzySearch(searchTerm, str, new MatchOption()).Score
                });
            }

            var orderedResults = results.OrderByDescending(x => x.Title).ToList();

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("SEARCHTERM: " + searchTerm);
            foreach (var item in orderedResults)
            {
                Debug.WriteLine("SCORE: " + item.Score.ToString() + ", FoundString: " + item.Title);
            }
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");
                       
            Assert.IsTrue(orderedResults[0].Score == 15 && orderedResults[0].Title == searchStrings[0]);
            Assert.IsTrue(orderedResults[1].Score == 53 && orderedResults[1].Title == searchStrings[1]);
            Assert.IsTrue(orderedResults[2].Score == 21 && orderedResults[2].Title == searchStrings[2]);
            Assert.IsTrue(orderedResults[3].Score == 107 && orderedResults[3].Title == searchStrings[3]);
            Assert.IsTrue(orderedResults[4].Score == 0 && orderedResults[4].Title == searchStrings[4]);
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
        public void WhenGivenDesiredPrecisionThenShouldReturnAllResultsGreaterOrEqual(string queryString, string compareString, 
                                                                                                        int expectedPrecisionScore, bool expectedPrecisionResult)
        {
            var expectedPrecisionString = (StringMatcher.SearchPrecisionScore)expectedPrecisionScore;            
            StringMatcher.UserSettingSearchPrecision = expectedPrecisionString.ToString();
            var matchResult = StringMatcher.FuzzySearch(queryString, compareString, new MatchOption());

            Debug.WriteLine("");
            Debug.WriteLine("###############################################");
            Debug.WriteLine($"SearchTerm: {queryString} PrecisionLevelSetAt: {expectedPrecisionString} ({expectedPrecisionScore})");
            Debug.WriteLine($"SCORE: {matchResult.Score.ToString()}, ComparedString: {compareString}");
            Debug.WriteLine("###############################################");
            Debug.WriteLine("");

            var matchPrecisionResult = matchResult.IsSearchPrecisionScoreMet();            
            Assert.IsTrue(matchPrecisionResult == expectedPrecisionResult);
        }
    }
}
