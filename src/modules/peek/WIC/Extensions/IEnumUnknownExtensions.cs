using System.Collections.Generic;
using System.ComponentModel;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IEnumUnknownExtensions
    {
        public static IEnumerable<object> AsEnumerable(this IEnumUnknown enumUnknown)
        {
            var buffer = new object[1];
            for (;;)
            {
                int length;
                enumUnknown.Next(1, buffer, out length);
                if (length != 1) break;
                yield return buffer[0];
            }
        }
    }
}
