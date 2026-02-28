// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class ProgressLinesOption : Option<bool>
    {
        private static readonly string[] _aliases = ["--progress-lines", "--accessible"];

        public ProgressLinesOption()
            : base(_aliases[0], "Use line-based progress output for screen reader accessibility (milestones: 0%, 25%, 50%, 75%, 100%)")
        {
        }
    }
}
