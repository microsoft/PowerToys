// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class UnitOption : Option<ImageResizer.Models.ResizeUnit?>
    {
        private static readonly string[] _aliases = ["--unit", "-u"];

        public UnitOption()
            : base(_aliases[0], Properties.Resources.CLI_Option_Unit)
        {
        }
    }
}
