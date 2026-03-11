// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// Shared default prompts for built-in AI actions. Referenced by both the AdvancedPaste module
/// and the Settings UI to ensure consistent defaults and enable "reset to default" functionality.
/// </summary>
public static class AdvancedPasteDefaultPrompts
{
    public const string FixSpellingAndGrammar = "Fix all spelling and grammar errors in the following text. Return only the corrected text without any additional explanation or commentary.";

    public const string FixSpellingAndGrammarCoaching = "Briefly explain what was changed and why in terms of language rules. Be concise as reviewer.";

    public const string FixSpellingAndGrammarCoachingSystem = "You are a writing coach and language teacher. You will be given an original sentence and a corrected version.";
}
