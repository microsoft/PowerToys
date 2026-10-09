// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegistryPreviewUILib
{
    internal static class ParseHelper
    {
        private const string ERRORIMAGE = "ms-appx:///Assets/RegistryPreview/error32.png";

        /// <summary>
        /// Checks a Key line for the closing bracket and treat it as an error if it cannot be found
        /// </summary>
        internal static void CheckKeyLineForBrackets(ref string registryLine, ref string imageName)
        {
            // following the current behavior of the registry editor, find the last ] and treat everything else as ignorable
            int lastBracket = registryLine.LastIndexOf(']');
            if (lastBracket == -1)
            {
                // since we don't have a last bracket yet, add an extra space and continue processing
                registryLine += " ";
                imageName = ERRORIMAGE;
            }
            else
            {
                // having found the last ] and there is text after it, drop the rest of the string on the floor
                if (lastBracket < registryLine.Length - 1)
                {
                    registryLine = registryLine.Substring(0, lastBracket + 1);
                }

                if (CheckForKnownGoodBranches(registryLine) == false)
                {
                    imageName = ERRORIMAGE;
                }
            }
        }

        /// <summary>
        /// Make sure the root of a full path start with one of the five "hard coded" roots.  Throw an error for the branch if it doesn't.
        /// </summary>
        private static bool CheckForKnownGoodBranches(string key)
        {
            if ((key.StartsWith("[HKEY_CLASSES_ROOT]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKEY_CURRENT_USER]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKEY_USERS]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKEY_LOCAL_MACHINE]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKEY_CURRENT_CONFIG]", StringComparison.InvariantCultureIgnoreCase) == false)
                &&
                (key.StartsWith(@"[HKEY_CLASSES_ROOT\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKEY_CURRENT_USER\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKEY_USERS\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKEY_LOCAL_MACHINE\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKEY_CURRENT_CONFIG\", StringComparison.InvariantCultureIgnoreCase) == false)
                &&
                (key.StartsWith("[HKCR]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKCU]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKU]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKLM]", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith("[HKCC]", StringComparison.InvariantCultureIgnoreCase) == false)
                &&
                (key.StartsWith(@"[HKCR\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKCU\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKU\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKLM\", StringComparison.InvariantCultureIgnoreCase) == false &&
                key.StartsWith(@"[HKCC\", StringComparison.InvariantCultureIgnoreCase) == false))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Rip the first and last character off a string,
        /// checking that the string is at least 2 characters long to avoid errors
        /// </summary>
        internal static string StripFirstAndLast(string line)
        {
            if (line.Length > 1)
            {
                line = line.Remove(line.Length - 1, 1);
                line = line.Remove(0, 1);
            }

            return line;
        }

        /// <summary>
        /// Replace any escaped characters in the REG file with their counterparts, for the UX
        /// </summary>
        internal static string StripEscapedCharacters(string value)
        {
            value = value.Replace("\\\\", "\\");    // Replace \\ with \ in the UI
            value = value.Replace("\\\"", "\"");    // Replace \" with " in the UI
            return value;
        }

        // special case for when the registryLine begins with a @ - make some tweaks and
        // let the regular processing handle the rest.
        internal static string ProcessRegistryLine(string registryLine)
        {
            if (registryLine.StartsWith("@=-", StringComparison.InvariantCulture))
            {
                // REG file has a callout to delete the @ Value which won't work *but* the Registry Editor will
                // clear the value of the @ Value instead, so it's still a valid line.
                registryLine = registryLine.Replace("@=-", "\"(Default)\"=\"\"");
            }
            else if (registryLine.StartsWith("@=", StringComparison.InvariantCulture))
            {
                // This is the Value called "(Default)" so we tweak the line for the UX
                registryLine = registryLine.Replace("@=", "\"(Default)\"=");
            }

            return registryLine;
        }
    }
}
