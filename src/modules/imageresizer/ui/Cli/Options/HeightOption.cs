// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class HeightOption : Option<double?>
    {
        private static readonly string[] _aliases = ["--height", "-h"];

        public HeightOption()
            : base(_aliases, "Set height")
        {
        }
    }
}
