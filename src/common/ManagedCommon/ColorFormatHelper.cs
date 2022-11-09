// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ManagedCommon
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class ColorFormatHelper
    {
        public static string GetStringRepresentation(Color? color, string formatString)
        {
            if (color == null)
            {
                color = Color.Moccasin;
            }

            // convert all %?? expressions to strings
            int formatterPosition = formatString.IndexOf('%', 0);
            while (formatterPosition != -1)
            {
                if (formatterPosition >= formatString.Length - 1)
                {
                    // the formatter % was the last character, we are done
                    break;
                }

                char paramFormat = formatString[formatterPosition + 1];
                char paramType;
                int paramCount = 2;
                if (paramFormat >= '1' && paramFormat <= '9')
                {
                    // no parameter formatter, just param type defined. (like %2). Using the default formatter -> decimal
                    paramType = paramFormat;
                    paramFormat = 'd';
                    paramCount = 1; // we have only one parameter after the formatter char
                }
                else
                {
                    // need to check the next char, which should be between 1 and 9. Plus the parameter formatter should be valid.
                    if (formatterPosition >= formatString.Length - 2)
                    {
                        // not enough characters, end of string, we are done
                        break;
                    }

                    paramType = formatString[formatterPosition + 2];
                }

                if (paramType >= '1' && paramType <= '9' &&
                    (paramFormat == 'd' || paramFormat == 'p' || paramFormat == 'h' || paramFormat == 'f'))
                {
                    formatString = string.Concat(formatString.AsSpan(0, formatterPosition), GetStringRepresentation(color.Value, paramFormat, paramType), formatString.AsSpan(formatterPosition + paramCount + 1));
                }

                // search for the next occurence of the formatter char
                formatterPosition = formatString.IndexOf('%', formatterPosition + 1);
            }

            return formatString;
        }

        private static string GetStringRepresentation(Color color, char paramFormat, char paramType)
        {
            if (paramType < '1' || paramType > '9' || (paramFormat != 'd' && paramFormat != 'p' && paramFormat != 'h' && paramFormat != 'f'))
            {
                return string.Empty;
            }

            switch (paramType)
            {
                case '1': return color.R.ToString(CultureInfo.InvariantCulture);
                case '2': return color.G.ToString(CultureInfo.InvariantCulture);
                case '3': return color.B.ToString(CultureInfo.InvariantCulture);
                case '4': return color.A.ToString(CultureInfo.InvariantCulture);
                default: return string.Empty;
            }
        }
    }
}
