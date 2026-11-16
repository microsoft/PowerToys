// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// Describes a robocopy switch that the Robocopy UI can represent, so generated commands can be
    /// validated against what the UI is actually able to apply.
    /// </summary>
    /// <param name="Name">The switch including its leading slash, e.g. "/E".</param>
    /// <param name="Kind">The kind of value the switch accepts.</param>
    /// <param name="Description">A short human readable description shown to the model.</param>
    /// <param name="AllowedValues">For multi-select switches, the letters that may be combined and what each means.</param>
    public record RobocopyOptionDescriptor(
        string Name,
        RobocopyOptionKind Kind,
        string Description,
        IReadOnlyList<RobocopyOptionValue> AllowedValues)
    {
        /// <summary>
        /// Gets just the letters that may be combined, for validation.
        /// </summary>
        public IEnumerable<string> AllowedValueLetters => AllowedValues.Select(value => value.Letter);
    }
}
