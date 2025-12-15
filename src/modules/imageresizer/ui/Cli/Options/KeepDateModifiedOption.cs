// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class KeepDateModifiedOption : Option<bool>
    {
        private static readonly string[] _aliases = ["--keep-date-modified"];

        public KeepDateModifiedOption()
            : base(_aliases, "Keep original date modified")
        {
        }
    }
}
