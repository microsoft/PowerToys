// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class ShrinkOnlyOption : Option<bool>
    {
        public ShrinkOnlyOption()
            : base("--shrink-only")
        {
            Description = Properties.Resources.CLI_Option_ShrinkOnly;
        }
    }
}
