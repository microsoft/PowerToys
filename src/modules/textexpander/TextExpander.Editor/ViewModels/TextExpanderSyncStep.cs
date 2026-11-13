// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander.Editor.ViewModels
{
    /// <summary>One sentence of the sync advice, around the name of the button it tells the user to select, which is shown bold.</summary>
    public readonly record struct TextExpanderSyncStep(string Before, string Button, string After);
}
