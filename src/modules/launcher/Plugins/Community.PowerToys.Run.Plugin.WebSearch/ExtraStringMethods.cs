// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Community.PowerToys.Run.Plugin.WebSearch
{
    public static class ExtraStringMethods
    {
        /// <summary>Gets a specific value from a JSON string.</summary>
        /// <param name="json">The string representing the JSON.</param>
        /// <param name="value">The string value to return (e.g. to get json.foo.bar pass `new string[] {"foo", "bar"}`).</param>
        /// <returns>The string value if found. <paramref name="null"/> if not.</returns>
        public static string GetJSONStringValue(string json, string[] value)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length == 0)
            {
                throw new ArgumentException($"{nameof(value)} cannot be empty.", nameof(value));
            }

            int startIndex = 0;
            for (int i = 0; i < value.Length; ++i)
            {
                string val = $"\"{value[i]}\"";

                int valIndex = json.IndexOf(val, startIndex, StringComparison.Ordinal);
                if (valIndex == -1)
                {
                    return null;
                }

                startIndex = valIndex + val.Length + 1;
            }

            int valStartIndex = json.IndexOf("\"", startIndex, StringComparison.Ordinal);
            if (valStartIndex == -1)
            {
                throw new ArgumentException($"The specified value is not of type string.", nameof(value));
            }

            ++valStartIndex;
            return json.Substring(valStartIndex, json.IndexOf("\"", valStartIndex + 1, StringComparison.Ordinal) - valStartIndex);
        }

        /// <summary>Gets the index of the nth occurrence of the specified character in the string.</summary>
        /// <returns>The zero-based index of <paramref name="val"/> if found, or -1 if not.</returns>
        public static int GetStrNthIndex(string s, char val, int n)
        {
            if (s is null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            int count = 1;
            for (int i = 0; i < s.Length; ++i)
            {
                if (s[i] == val)
                {
                    if (n == count)
                    {
                        return i;
                    }

                    ++count;
                }
            }

            return -1;
        }
    }
}
