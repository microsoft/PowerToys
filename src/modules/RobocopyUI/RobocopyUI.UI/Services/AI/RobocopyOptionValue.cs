// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// One selectable letter of a multi-select switch, together with what it means.
    /// </summary>
    /// <param name="Letter">The letter as it appears in the command line, e.g. "D".</param>
    /// <param name="Description">What the letter copies, e.g. "Data".</param>
    public record RobocopyOptionValue(string Letter, string Description);
}
