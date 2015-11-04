using System;
using System.Linq;

namespace Wox.Infrastructure
{
    public static partial class Unidecoder
    {
        public static string Unidecode(this string self)
        {
            if (string.IsNullOrEmpty(self))
                return "";

            if (self.All(x => x < 128))
                return self;

            return String.Join("", self.Select(c => c.Unidecode()).ToArray());
        }

        public static string Unidecode(this char c)
        {
            string result;
            if (c < 128)
                return char.ToString(c);

            int high = c >> 8;
            int low = c & 0xff;
            string[] t;

            if (characters.TryGetValue(high, out t))
                return t[low];

            return string.Empty;
        }
    }
}
