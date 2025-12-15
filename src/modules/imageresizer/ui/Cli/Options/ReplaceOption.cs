// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class ReplaceOption : Option<bool>
    {
        private static readonly string[] _aliases = ["--replace", "-r"];

        public ReplaceOption()
            : base(_aliases, "Replace original files")
        {
        }
    }
}
