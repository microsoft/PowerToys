// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class UnitOption : Option<ImageResizer.Models.ResizeUnit?>
    {
        public UnitOption()
            : base("--unit", "-u")
        {
            Description = Properties.Resources.CLI_Option_Unit;
        }
    }
}
