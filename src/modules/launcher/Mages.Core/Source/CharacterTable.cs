namespace Mages.Core.Source
{
    using System;

    /// <summary>
    /// A set of special characters.
    /// </summary>
    static class CharacterTable
    {
        /// <summary>
        /// The end of file Character -1.
        /// </summary>
        public const Int32 End = -1;

        /// <summary>
        /// The tilde Character (~).
        /// </summary>
        public const Int32 Tilde = 0x7e;

        /// <summary>
        /// The pipe Character (|).
        /// </summary>
        public const Int32 Pipe = 0x7c;

        /// <summary>
        /// The null Character.
        /// </summary>
        public const Int32 NullPtr = 0x0;

        /// <summary>
        /// The ampersand Character (&amp;).
        /// </summary>
        public const Int32 Ampersand = 0x26;

        /// <summary>
        /// The number sign Character (#).
        /// </summary>
        public const Int32 Hash = 0x23;

        /// <summary>
        /// The dollar sign Character ($).
        /// </summary>
        public const Int32 Dollar = 0x24;

        /// <summary>
        /// The semicolon sign (;).
        /// </summary>
        public const Int32 SemiColon = 0x3b;

        /// <summary>
        /// The asterisk Character (*).
        /// </summary>
        public const Int32 Asterisk = 0x2a;

        /// <summary>
        /// The equals sign (=).
        /// </summary>
        public const Int32 Equal = 0x3d;

        /// <summary>
        /// The comma Character (,).
        /// </summary>
        public const Int32 Comma = 0x2c;

        /// <summary>
        /// The full stop (.).
        /// </summary>
        public const Int32 FullStop = 0x2e;

        /// <summary>
        /// The circumflex accent (^) Character.
        /// </summary>
        public const Int32 CircumflexAccent = 0x5e;

        /// <summary>
        /// The commercial at (@) Character.
        /// </summary>
        public const Int32 At = 0x40;

        /// <summary>
        /// The opening angle bracket (LESS-THAN-SIGN).
        /// </summary>
        public const Int32 LessThan = 0x3c;

        /// <summary>
        /// The closing angle bracket (GREATER-THAN-SIGN).
        /// </summary>
        public const Int32 GreaterThan = 0x3e;

        /// <summary>
        /// The single quote / quotation mark (').
        /// </summary>
        public const Int32 SingleQuotationMark = 0x27;

        /// <summary>
        /// The (double) quotation mark (").
        /// </summary>
        public const Int32 DoubleQuotationMark = 0x22;

        /// <summary>
        /// The (curved) quotation mark (`).
        /// </summary>
        public const Int32 CurvedQuotationMark = 0x60;

        /// <summary>
        /// The question mark (?).
        /// </summary>
        public const Int32 QuestionMark = 0x3f;

        /// <summary>
        /// The tab Character.
        /// </summary>
        public const Int32 Tab = 0x09;

        /// <summary>
        /// The line feed Character.
        /// </summary>
        public const Int32 LineFeed = 0x0a;

        /// <summary>
        /// The carriage return Character.
        /// </summary>
        public const Int32 CarriageReturn = 0x0d;

        /// <summary>
        /// The form feed Character.
        /// </summary>
        public const Int32 FormFeed = 0x0c;

        /// <summary>
        /// The space Character.
        /// </summary>
        public const Int32 Space = 0x20;

        /// <summary>
        /// The slash (solidus, /) Character.
        /// </summary>
        public const Int32 Slash = 0x2f;

        /// <summary>
        /// The backslash (reverse-solidus, \) Character.
        /// </summary>
        public const Int32 Backslash = 0x5c;

        /// <summary>
        /// The colon (:) Character.
        /// </summary>
        public const Int32 Colon = 0x3a;

        /// <summary>
        /// The exlamation mark (!) Character.
        /// </summary>
        public const Int32 ExclamationMark = 0x21;

        /// <summary>
        /// The dash (hypen minus, -) Character.
        /// </summary>
        public const Int32 Minus = 0x2d;

        /// <summary>
        /// The plus sign (+).
        /// </summary>
        public const Int32 Plus = 0x2b;

        /// <summary>
        /// The low line (_) Character.
        /// </summary>
        public const Int32 Lowline = 0x5f;

        /// <summary>
        /// The percent (%) Character.
        /// </summary>
        public const Int32 Percent = 0x25;

        /// <summary>
        /// Opening a round bracket (.
        /// </summary>
        public const Int32 OpenBracket = 0x28;

        /// <summary>
        /// Closing a round bracket ).
        /// </summary>
        public const Int32 CloseBracket = 0x29;

        /// <summary>
        /// Opening an array bracket [.
        /// </summary>
        public const Int32 OpenArray = 0x5b;

        /// <summary>
        /// Closing an array bracket ].
        /// </summary>
        public const Int32 CloseArray = 0x5d;

        /// <summary>
        /// Opening a scope bracket {.
        /// </summary>
        public const Int32 OpenScope = 0x7b;

        /// <summary>
        /// Closing a scope bracket }.
        /// </summary>
        public const Int32 CloseScope = 0x7d;

        /// <summary>
        /// The number 0.
        /// </summary>
        public const Int32 Zero = 0x30;

        /// <summary>
        /// The number 1.
        /// </summary>
        public const Int32 One = 0x31;

        /// <summary>
        /// The letter E.
        /// </summary>
        public const Int32 BigE = 0x45;

        /// <summary>
        /// The letter I.
        /// </summary>
        public const Int32 BigI = 0x49;

        /// <summary>
        /// The letter a.
        /// </summary>
        public const Int32 SmallA = 0x61;

        /// <summary>
        /// The letter b.
        /// </summary>
        public const Int32 SmallB = 0x62;

        /// <summary>
        /// The letter e.
        /// </summary>
        public const Int32 SmallE = 0x65;

        /// <summary>
        /// The letter f.
        /// </summary>
        public const Int32 SmallF = 0x66;

        /// <summary>
        /// The letter i.
        /// </summary>
        public const Int32 SmallI = 0x69;

        /// <summary>
        /// The letter n.
        /// </summary>
        public const Int32 SmallN = 0x6e;

        /// <summary>
        /// The letter r.
        /// </summary>
        public const Int32 SmallR = 0x72;

        /// <summary>
        /// The letter t.
        /// </summary>
        public const Int32 SmallT = 0x74;

        /// <summary>
        /// The letter u.
        /// </summary>
        public const Int32 SmallU = 0x75;

        /// <summary>
        /// The letter v.
        /// </summary>
        public const Int32 SmallV = 0x76;

        /// <summary>
        /// The letter x.
        /// </summary>
        public const Int32 SmallX = 0x78;
    }
}
