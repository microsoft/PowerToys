// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class DestinationOption : Option<string>
    {
        public DestinationOption()
            : base("--destination", "-d", "/d")
        {
            Description = Properties.Resources.CLI_Option_Destination;
        }
    }
}
