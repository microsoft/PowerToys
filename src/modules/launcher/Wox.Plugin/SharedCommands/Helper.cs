using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Wox.Plugin.SharedCommands
{
    class Helper
    {
        public static string RemoveNewLineFromString(string s)
        {
            s = Regex.Replace(s, @"\r\n|\n|\r", " ");
            return s;
        }
    }
}
