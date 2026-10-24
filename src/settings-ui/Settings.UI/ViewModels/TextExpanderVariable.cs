// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>An entry in the Insert variable menu.</summary>
    public sealed class TextExpanderVariable
    {
        private readonly string _dateFormat;

        public TextExpanderVariable(string name, string token, string dateFormat)
        {
            Name = name;
            Token = token;
            _dateFormat = dateFormat;
        }

        public string Name { get; }

        /// <summary>Gets the text inserted into the replacement.</summary>
        public string Token { get; }

        /// <summary>
        /// Gets what this resolves to at <paramref name="now"/>, or empty when it cannot be known
        /// ahead of time.
        ///
        /// <para>
        /// Clipboard and prompt deliberately show nothing rather than a placeholder: inventing a
        /// sample would preview a result the user is never going to see.
        /// </para>
        /// </summary>
        public string SampleAt(DateTime now) => _dateFormat == null
            ? string.Empty
            : now.ToString(_dateFormat, CultureInfo.InvariantCulture);

        /// <summary>Gets a value indicating whether there is a sample worth showing.</summary>
        public bool HasSample => _dateFormat != null;

        /// <summary>Gets a value indicating whether this is the custom date format, which Insert variable offers ready-made formats for.</summary>
        public bool IsCustomDate => Token.StartsWith("{{date:", StringComparison.Ordinal);

        /// <summary>Gets a value indicating whether this variable asks a question at runtime.</summary>
        public bool IsPrompt => Token.StartsWith("{{prompt:", StringComparison.Ordinal)
            || Token.StartsWith("{{prompt?:", StringComparison.Ordinal);
    }
}
