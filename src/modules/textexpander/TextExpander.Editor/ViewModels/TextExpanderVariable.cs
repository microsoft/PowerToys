// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.PowerToys.TextExpander.Editor.ViewModels
{
    /// <summary>An entry in the Insert variable menu.</summary>
    public sealed class TextExpanderVariable
    {
        public TextExpanderVariable(string name, string token, bool hasSample)
        {
            Name = name;
            Token = token;
            HasSample = hasSample;
        }

        public string Name { get; }

        /// <summary>Gets the text inserted into the replacement.</summary>
        public string Token { get; }

        /// <summary>
        /// Gets what this resolves to at <paramref name="now"/>, as the engine types it, or empty when it can't be
        /// known ahead of time (clipboard and prompt show nothing rather than an invented sample).
        /// </summary>
        public string SampleAt(DateTime now) => HasSample
            ? Variables.ExpandWithPromptRequests(Token, _ => null, null, now, CultureInfo.CurrentCulture).Text
            : string.Empty;

        /// <summary>Gets a value indicating whether there is a sample worth showing.</summary>
        public bool HasSample { get; }

        /// <summary>Gets a value indicating whether this is the custom date format, which Insert variable offers ready-made formats for.</summary>
        public bool IsCustomDate => Token.StartsWith("{{date:", StringComparison.Ordinal);

        /// <summary>Gets a value indicating whether this variable asks a question at runtime.</summary>
        public bool IsPrompt => Token.StartsWith("{{prompt:", StringComparison.Ordinal)
            || Token.StartsWith("{{prompt?:", StringComparison.Ordinal);
    }
}
