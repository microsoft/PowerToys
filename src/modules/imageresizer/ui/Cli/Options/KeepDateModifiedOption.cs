// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class KeepDateModifiedOption : Option<bool>
    {
        public KeepDateModifiedOption()
            : base("--keep-date-modified")
        {
            Description = Properties.Resources.CLI_Option_KeepDateModified;
        }
    }
}
