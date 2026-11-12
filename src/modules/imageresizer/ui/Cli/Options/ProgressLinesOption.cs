// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class ProgressLinesOption : Option<bool>
    {
        public ProgressLinesOption()
            : base("--progress-lines", "--accessible")
        {
            Description = "Use line-based progress output for screen reader accessibility (milestones: 0%, 25%, 50%, 75%, 100%)";
        }
    }
}
