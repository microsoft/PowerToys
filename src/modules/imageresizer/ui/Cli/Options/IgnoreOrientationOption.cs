// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class IgnoreOrientationOption : Option<bool>
    {
        private static readonly string[] _aliases = ["--ignore-orientation"];

        public IgnoreOrientationOption()
            : base(_aliases, Properties.Resources.CLI_Option_IgnoreOrientation)
        {
        }
    }
}
