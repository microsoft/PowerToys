// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class WidthOption : Option<double?>
    {
        private static readonly string[] _aliases = ["--width", "-w"];

        public WidthOption()
            : base(_aliases, "Set width")
        {
        }
    }
}
