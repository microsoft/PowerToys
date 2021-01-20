namespace Mages.Core.Source
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Specification helpers and extension methods.
    /// </summary>
    static class Specification
    {
        /// <summary>
        /// Converts a given character from the hex representation (0-9A-Fa-f) to an integer.
        /// </summary>
        /// <param name="character">The character to convert.</param>
        /// <returns>The integer value or undefined behavior if invalid.</returns>
        [DebuggerStepThrough]
        public static Int32 FromHex(this Int32 character)
        {
            return IsDigit(character) ? character - 0x30 : character - (IsLowercaseAscii(character) ? 0x57 : 0x37);
        }

        /// <summary>
        /// Determines if the given character is in the given range.
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <param name="lower">The lower bound of the range.</param>
        /// <param name="upper">The upper bound of the range.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsInRange(this Int32 character, Int32 lower, Int32 upper)
        {
            return character >= lower && character <= upper;
        }

        /// <summary>
        /// Determines if the given character is a uppercase character (A-Z).
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsUppercaseAscii(this Int32 character)
        {
            return character >= 0x41 && character <= 0x5a;
        }

        /// <summary>
        /// Determines if the given character is a lowercase character (a-z).
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsLowercaseAscii(this Int32 character)
        {
            return character >= 0x61 && character <= 0x7a;
        }

        /// <summary>
        /// Determines if the given character is a hexadecimal (0-9a-fA-F).
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsHex(this Int32 character)
        {
            return IsDigit(character) || (character >= 0x41 && character <= 0x46) || (character >= 0x61 && character <= 0x66);
        }

        /// <summary>
        /// Gets if the character is actually a (A-Z,a-z) letter.
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsLetter(this Int32 character)
        {
            return IsUppercaseAscii(character) || IsLowercaseAscii(character);
        }

        /// <summary>
        /// Gets if the character is actually a name character.
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsName(this Int32 character)
        {
            return IsNameStart(character) || IsDigit(character);
        }

        /// <summary>
        /// Determines if the given character is a valid character for starting an identifier.
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsNameStart(this Int32 character)
        {
            return character >= 0x80 || IsLetter(character) || character == CharacterTable.Lowline;
        }

        /// <summary>
        /// Determines if the given character is a white-space character.
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsSpaceCharacter(this Int32 character)
        {
            return character.IsInRange(0x0009, 0x000d) || character == 0x0020 || character == 0x0085 || character == 0x00a0 ||
                   character == 0x1680 || character == 0x180e || character.IsInRange(0x2000, 0x200a) || character == 0x2028 ||
                   character == 0x2029 || character == 0x202f || character == 0x205f || character == 0x3000;
        }

        /// <summary>
        /// Determines if the given character is a digit (0-9).
        /// </summary>
        /// <param name="character">The character to examine.</param>
        /// <returns>The result of the test.</returns>
        [DebuggerStepThrough]
        public static Boolean IsDigit(this Int32 character)
        {
            return character >= 0x30 && character <= 0x39;
        }
    }
}
