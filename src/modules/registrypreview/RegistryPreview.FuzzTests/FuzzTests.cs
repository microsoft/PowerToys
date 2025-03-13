// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using RegistryPreviewUILib;

namespace RegistryPreview.FuzzTests
{
    public class FuzzTests
    {
        private const string REGISTRYHEADER4 = "regedit4";
        private const string REGISTRYHEADER5 = "windows registry editor version 5.00";
        private const string KEYIMAGE = "ms-appx:///Assets/RegistryPreview/folder32.png";
        private const string DELETEDKEYIMAGE = "ms-appx:///Assets/RegistryPreview/deleted-folder32.png";

        // Case 1: Fuzz test for CheckKeyLineForBrackets
        public static void FuzzCheckKeyLineForBrackets(ReadOnlySpan<byte> input)
        {
            string registryLine;

            // Simulate registry file content as registryContent
            var registryContent = GenerateRegistryHeader(input);

            string[] registryLines = registryContent.Split("\r");

            if (registryLines.Length <= 1)
            {
                return;
            }

            // REG files have to start with one of two headers and it's case-insensitive
            // The header in the registry file is either REGISTRYHEADER4 or REGISTRYHEADER5
            registryLine = registryLines[0];

            // Check if the registry header is valid
            if (!IsValidRegistryHeader(registryLine))
            {
                return;
            }

            int index = 1;
            registryLine = registryLines[index]; // Extract content after the header

            ParseHelper.ProcessRegistryLine(registryLine);
            if (registryLine.StartsWith("[-", StringComparison.InvariantCulture))
            {
                // remove the - as we won't need it but it will get special treatment in the UI
                registryLine = registryLine.Remove(1, 1);

                string imageName = DELETEDKEYIMAGE;

                // Fuzz test for the CheckKeyLineForBrackets method
                ParseHelper.CheckKeyLineForBrackets(ref registryLine, ref imageName);
            }
            else if (registryLine.StartsWith('['))
            {
                string imageName = KEYIMAGE;

                // Fuzz test for the CheckKeyLineForBrackets method
                ParseHelper.CheckKeyLineForBrackets(ref registryLine, ref imageName);
            }
            else
            {
                return;
            }
        }

        // Case 2: Fuzz test for StripFirstAndLast
        public static void FuzzStripFirstAndLast(ReadOnlySpan<byte> input)
        {
            string registryLine;

            var registryContent = GenerateRegistryHeader(input);

            registryContent = registryContent.Replace("\r\n", "\r");
            string[] registryLines = registryContent.Split("\r");

            if (registryLines.Length <= 1)
            {
                return;
            }

            // REG files have to start with one of two headers and it's case-insensitive
            registryLine = registryLines[0];

            if (!IsValidRegistryHeader(registryLine))
            {
                return;
            }

            int index = 1;
            registryLine = registryLines[index];

            ParseHelper.ProcessRegistryLine(registryLine);

            if (registryLine.StartsWith("[-", StringComparison.InvariantCulture))
            {
                // remove the - as we won't need it but it will get special treatment in the UI
                registryLine = registryLine.Remove(1, 1);

                string imageName = DELETEDKEYIMAGE;
                ParseHelper.CheckKeyLineForBrackets(ref registryLine, ref imageName);

                // Fuzz test for the StripFirstAndLast method
                registryLine = ParseHelper.StripFirstAndLast(registryLine);
            }
            else if (registryLine.StartsWith('['))
            {
                string imageName = KEYIMAGE;
                ParseHelper.CheckKeyLineForBrackets(ref registryLine, ref imageName);

                // Fuzz test for the StripFirstAndLast method
                registryLine = ParseHelper.StripFirstAndLast(registryLine);
            }
            else if (registryLine.StartsWith('"') && registryLine.EndsWith("=-", StringComparison.InvariantCulture))
            {
                // remove "=-"
                registryLine = registryLine[..^2];

                // remove the "'s without removing all of them
                // Fuzz test for the StripFirstAndLast method
                registryLine = ParseHelper.StripFirstAndLast(registryLine);
            }
            else if (registryLine.StartsWith('"'))
            {
                int equal = registryLine.IndexOf('=');
                if ((equal < 0) || (equal > registryLine.Length - 1))
                {
                    // something is very wrong
                    return;
                }

                // set the name and the value
                string name = registryLine.Substring(0, equal);

                // trim the whitespace and quotes from the name
                name = name.Trim();

                // Fuzz test for the StripFirstAndLast method
                name = ParseHelper.StripFirstAndLast(name);

                // Clean out any escaped characters in the value, only for the preview
                name = ParseHelper.StripEscapedCharacters(name);

                // set the value
                string value = registryLine.Substring(equal + 1);

                // trim the whitespace from the value
                value = value.Trim();

                // if the first character is a " then this is a string value, so find the last most " which will avoid comments
                if (value.StartsWith('"'))
                {
                    int last = value.LastIndexOf('"');
                    if (last >= 0)
                    {
                        value = value.Substring(0, last + 1);
                    }
                }

                if (value.StartsWith('"') && value.EndsWith('"'))
                {
                    value = ParseHelper.StripFirstAndLast(value);
                }
            }
            else
            {
                return;
            }
        }

        public static string GenerateRegistryHeader(ReadOnlySpan<byte> input)
        {
            string header = new Random().Next(2) == 0 ? REGISTRYHEADER4 : REGISTRYHEADER5;

            string inputText = System.Text.Encoding.UTF8.GetString(input);
            string registryContent = header + "\r" + inputText;

            return registryContent;
        }

        private static bool IsValidRegistryHeader(string line)
        {
            // Convert the line to lowercase once for comparison
            switch (line)
            {
                case REGISTRYHEADER4:
                case REGISTRYHEADER5:
                    return true;
                default:
                    return false;
            }
        }
    }
}
