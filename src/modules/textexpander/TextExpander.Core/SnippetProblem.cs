// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander
{
    /// <summary>Why a trigger can't be saved; the editor shows each in the user's language.</summary>
    public enum SnippetProblem
    {
        None,
        EmptyTrigger,
        TriggerHasLineBreak,
        TriggerStartsWithHiddenCharacter,
        TriggerHasOuterSpace,
    }
}
