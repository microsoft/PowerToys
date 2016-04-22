using System;
using JetBrains.Annotations;

namespace Wox.Infrastructure
{
    public class StringMatcher
    {
        /// <summary>
        /// Check if a candidate is match with the source
        /// </summary>
        /// <param name="source"></param>
        /// <param name="candidate"></param>
        /// <returns>Match score</returns>
        public static int Match(string source, string candidate)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(candidate)) return 0;

            FuzzyMatcher matcher = FuzzyMatcher.Create(candidate);
            int score = matcher.Evaluate(source).Score;
            if (score > 0) return score;

            score = matcher.Evaluate(source.Unidecode()).Score;
            return score;
        }

        public static bool IsMatch(string source, string candidate)
        {
            return Match(source, candidate) > 0;
        }
    }
}
