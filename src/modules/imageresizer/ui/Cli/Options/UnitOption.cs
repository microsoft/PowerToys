// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

using ImageResizer.Properties;

namespace ImageResizer.Cli.Options
{
    public sealed class UnitOption : Option<ImageResizer.Models.ResizeUnit?>
    {
        private static readonly string[] _aliases = ["--unit", "-u"];

        public UnitOption()
            : base(_aliases, ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Unit"))
        {
        }
    }
}
