// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Why a trigger or a replacement can't be saved. Settings shows each one in the user's
    /// language from its own resource.
    /// </summary>
    public enum TextExpanderSnippetProblem
    {
        None,
        EmptyTrigger,
        TriggerHasLineBreak,
        TriggerStartsWithHiddenCharacter,
        TriggerHasOuterSpace,
        TriggerHasEquals,
        TriggerEndsWithBlockStart,
        TriggerStartsWithComment,
        ReplacementHasBlockEnd,
    }
}
