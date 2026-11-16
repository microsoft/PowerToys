// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace RobocopyUI.Services.AI
{
    /// <summary>
    /// The outcome of a single command generation round trip.
    /// </summary>
    public sealed class RobocopyPlan
    {
        /// <summary>
        /// Gets a value indicating whether the model needs more information before it can produce a command.
        /// </summary>
        public bool NeedsFollowUp { get; init; }

        /// <summary>
        /// Gets the question to put to the user when <see cref="NeedsFollowUp"/> is true.
        /// </summary>
        public string FollowUpQuestion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the source path the command should operate on.
        /// </summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// Gets the destination path the command should operate on.
        /// </summary>
        public string Destination { get; init; } = string.Empty;

        /// <summary>
        /// Gets the validated switches to apply.
        /// </summary>
        public IReadOnlyList<RobocopyPlanOption> Options { get; init; } = [];

        /// <summary>
        /// Gets a plain-language summary of what the command will do.
        /// </summary>
        public string Explanation { get; init; } = string.Empty;

        /// <summary>
        /// Gets warnings about destructive behaviour, e.g. mirroring or purging.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];

        /// <summary>
        /// Gets the full command line as it will be run.
        /// </summary>
        public string CommandLine { get; init; } = string.Empty;
    }
}
