using System;
using System.Collections.Generic;
using System.Text;

namespace Wox.Infrastructure
{
    public static class ChineseToPinYin
    {
        [Obsolete]
        public static string ToPinYin(string txt)
        {
            return txt.Unidecode();
        }
    }
}
