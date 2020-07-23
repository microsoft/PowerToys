using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Plugin.VSCodeWorkspaces.SshConfigParser
{
    // Based on code from https://github.com/dotnil/ssh-config
    public static class Globber
    {
        static readonly Regex _patternSplitter =  new Regex("[,\\s]+");
        
        private static bool Match(string pattern, string str)
        {
            pattern = pattern.Replace(".", "\\.")
                .Replace("*", ".*")
                .Replace("?", ".?");

            return new Regex("^(?:" + pattern + ")$").IsMatch(str);
        }

        /// <summary>
        /// A helper function to match input against [pattern-list](https://www.freebsd.org/cgi/man.cgi?query=ssh_config&sektion=5#PATTERNS).
        /// According to `man ssh_config`, negated patterns shall be matched first.
        /// </summary>
        /// <param name="patternList"></param>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool Glob(string patternList, string str)
        {
           var patterns = _patternSplitter.Split(patternList)
                .OrderByDescending(a => a.StartsWith("!"));

            foreach (var pattern in patterns)
            {
                var negate = pattern[0] == '!';
                if (negate && Match(pattern.Substring(1), str))
                {
                    return false;
                }

                if (Match(pattern, str))
                {
                    return true;
                }
            }

            return false;
        }
    }
}