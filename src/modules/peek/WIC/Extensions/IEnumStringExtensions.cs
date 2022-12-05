using System.Collections.Generic;
using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IEnumStringExtensions
    {
        public static IEnumerable<string> AsEnumerable(this IEnumString enumString)
        {
            var buffer = new string[1];
            for (;;)
            {
                int length;
                enumString.Next(1, buffer, out length);
                if (length != 1) break;
                yield return buffer[0];
            }
        }
    }
}
