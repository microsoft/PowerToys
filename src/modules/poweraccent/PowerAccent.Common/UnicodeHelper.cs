// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace PowerAccent.Common;

/// <summary>
/// Provides Unicode character metadata by calling the ICU library that ships with
/// Windows 10 version 1903 and later.
/// </summary>
public static class UnicodeHelper
{
    // u_charName name choice: 0 = U_UNICODE_CHAR_NAME (official Unicode name).
    private const int UnicodeCharName = 0;
    private const int BufferSize = 128;
    private static bool _icuUnavailable;

    /// <summary>
    /// Returns the Unicode name(s) for all code points in <paramref name="character"/>.
    /// For multi-code-point strings (e.g. combining sequences or surrogate pairs) the
    /// individual names are joined with " + ". Returns <see langword="null"/> if no
    /// names can be determined (e.g. control characters, private-use area, or ICU
    /// unavailable).
    /// </summary>
    /// <remarks>
    /// Requires <c>icu.dll</c>, which ships with Windows 10 version 1903 (May 2019)
    /// and later.
    /// </remarks>
    public static string GetCharacterName(string character)
    {
        if (string.IsNullOrEmpty(character) || _icuUnavailable)
        {
            return null;
        }

        try
        {
            var names = new List<string>();

            // Enumerate every code point in the string — handles surrogate pairs and
            // combining sequences such as "°C" (U+00B0 + U+0043) correctly.
            for (int i = 0; i < character.Length;)
            {
                int codePoint = char.ConvertToUtf32(character, i);
                i += char.IsHighSurrogate(character[i]) ? 2 : 1;

                // The native function writes a null-terminated ASCII string.
                // We pass a raw byte[] so the P/Invoke marshaller never touches the
                // encoding — then decode as Latin-1 ourselves.
                string name = GetCodePointName(codePoint);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            return names.Count > 0 ? string.Join(" + ", names) : null;
        }
        catch (DllNotFoundException)
        {
            _icuUnavailable = true;
        }
        catch (EntryPointNotFoundException)
        {
            _icuUnavailable = true;
        }

        return null;
    }

    private static string GetCodePointName(int codePoint)
    {
        byte[] buffer = new byte[BufferSize];

        while (true)
        {
            int errorCode = 0;
            int length = NativeIcu.UCharName(codePoint, UnicodeCharName, buffer, buffer.Length, ref errorCode);
            if (length <= 0)
            {
                return null;
            }

            if (length < buffer.Length)
            {
                return Encoding.Latin1.GetString(buffer, 0, length);
            }

            // ICU returns the required length excluding the null terminator.
            buffer = new byte[length + 1];
        }
    }

    private static class NativeIcu
    {
        // We use the unified Windows system ICU library available on Windows 10
        // version 1903 (May 2019) and later.
        // CharSet.None keeps the marshaller out of encoding decisions - the function
        // writes a null-terminated ASCII string which we decode manually.
        [DllImport("icu.dll", EntryPoint = "u_charName", CharSet = CharSet.None, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int UCharName(
            int codePoint,
            int nameChoice,
            [Out] byte[] buffer,
            int bufferLength,
            ref int errorCode);
    }
}
