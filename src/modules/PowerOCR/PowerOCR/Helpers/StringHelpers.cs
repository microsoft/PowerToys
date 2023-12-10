// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerOCR.Helpers;

internal static class StringHelpers
{
    public static string MakeStringSingleLine(this string textToEdit)
    {
        if (!textToEdit.Contains('\n')
            && !textToEdit.Contains('\r'))
        {
            return textToEdit;
        }

        StringBuilder workingString = new(textToEdit);

        workingString.Replace("\r\n", " ");
        workingString.Replace(Environment.NewLine, " ");
        workingString.Replace('\n', ' ');
        workingString.Replace('\r', ' ');

        Regex regex = new("[ ]{2,}");
        string temp = regex.Replace(workingString.ToString(), " ");
        workingString.Clear();
        workingString.Append(temp);
        if (workingString[0] == ' ')
        {
            workingString.Remove(0, 1);
        }

        if (workingString[workingString.Length - 1] == ' ')
        {
            workingString.Remove(workingString.Length - 1, 1);
        }

        return workingString.ToString();
    }
}
