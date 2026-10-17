// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Deliberately small. Every property here is one the engine actually honours today, so the
    /// settings page cannot offer a control that silently does nothing. Anything the engine does
    /// not yet support -- per-app exclusions, regex triggers, forms -- is left out until it does.
    /// </summary>
    public class TextExpanderProperties
    {
        /// <summary>
        /// Folder holding the snippet library. Empty means "let the engine resolve it", which
        /// defaults to %LOCALAPPDATA%\\Microsoft\\PowerToys\\TextExpander when unset.
        /// </summary>
        [JsonPropertyName("snippets_path")]
        public StringProperty SnippetsPath { get; set; }

        /// <summary>
        /// How a replacement reaches the focused window: "auto", "clipboard" or "type".
        ///
        /// An escape hatch rather than a preference. Some remote-desktop and terminal clients
        /// swallow a synthetic Ctrl+V, and a few hardened apps ignore WM_PASTE outright; "type"
        /// also keeps expansions off the clipboard entirely for users who need that.
        /// </summary>
        [JsonPropertyName("injection_backend")]
        public StringProperty InjectionBackend { get; set; }

        /// <summary>Replacements at least this long are pasted instead of typed.</summary>
        [JsonPropertyName("clipboard_threshold_chars")]
        public IntProperty ClipboardThresholdChars { get; set; }

        /// <summary>
        /// Whether a PowerToys Keyboard Manager remap counts as the user typing.
        ///
        /// True by default: the remapped character is the one the user meant and the only one
        /// anything downstream sees, so ignoring it would silently stop snippets matching for
        /// exactly the users who remap keys. Exposed because the opposite reading -- that a remap
        /// is machinery rather than typing -- is defensible.
        /// </summary>
        [JsonPropertyName("treat_remaps_as_typing")]
        public bool TreatRemapsAsTyping { get; set; }

        /// <summary>
        /// Gets or sets whether a trigger waits for a word terminator before firing.
        ///
        /// Off by default. Firing the instant a trigger completes is what makes expansion feel
        /// immediate, and that is the point of the tool. On, a trigger only fires once followed
        /// by a space or punctuation, which stops it going off inside a longer word at the cost
        /// of that immediacy.
        /// </summary>
        [JsonPropertyName("require_word_boundary")]
        public bool RequireWordBoundary { get; set; }

        /// <summary>
        /// Gets or sets whether the space that released a trigger is handed back after the
        /// replacement.
        ///
        /// On by default: the user typed that space, and not returning it means typing it twice.
        /// Off matters for snippets that expand to an email address or a code, where a trailing
        /// space is wrong rather than merely untidy. Only meaningful with RequireWordBoundary on.
        /// </summary>
        [JsonPropertyName("word_boundary_keeps_space")]
        public bool WordBoundaryKeepsSpace { get; set; }

        public TextExpanderProperties()
        {
            SnippetsPath = new StringProperty();
            InjectionBackend = "auto";
            ClipboardThresholdChars = new IntProperty(TextExpanderSettings.DefaultClipboardThresholdChars);
            TreatRemapsAsTyping = true;
            RequireWordBoundary = false;
            WordBoundaryKeepsSpace = true;
        }
    }
}
