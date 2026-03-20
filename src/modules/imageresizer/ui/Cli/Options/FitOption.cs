// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

using ImageResizer.Properties;

namespace ImageResizer.Cli.Options
{
    public sealed class FitOption : Option<ImageResizer.Models.ResizeFit?>
    {
        private static readonly string[] _aliases = ["--fit", "-f"];

        public FitOption()
            : base(_aliases, ResourceLoaderInstance.ResourceLoader.GetString("CLI_Option_Fit"))
        {
        }
    }
}
