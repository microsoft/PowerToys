using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Wox.Infrastructure
{
    //From:http://crossplatform.net/sublime-text-ctrl-p-fuzzy-matching-in-python/
    public class FuzzyMatcher
    {
        private Regex reg = null;
        private string rawQuery = "";

        private FuzzyMatcher(string query)
        {
            this.rawQuery = query.Trim();
            this.reg = GetPattern(query);
        }

        private Regex GetPattern(string query)
        {
            var pattern = string.Join(".*?", query.ToCharArray().Select(x => Regex.Escape(x.ToString())).ToArray());
            return new Regex(pattern, RegexOptions.IgnoreCase);
        }

        public int Score(string str)
        {
            var match = reg.Match(str);
            if (!match.Success)
                return 0;

            //a match found near the beginning of a string is scored more than a match found near the end
            //a match is scored more if the characters in the patterns are closer to each other, while the score is lower if they are more spread out
            var score = 100 * (this.rawQuery.Length + 1) / ((1 + match.Index) + (match.Length + 1));
            //a match with less characters assigning more weights
            if (str.Length - this.rawQuery.Length < 5)
                score = score + 20;
            else if (str.Length - this.rawQuery.Length < 10)
                score = score + 10;

            return score;
        }

        public static FuzzyMatcher Create(string query)
        {
            return new FuzzyMatcher(query);
        }
    }
}
