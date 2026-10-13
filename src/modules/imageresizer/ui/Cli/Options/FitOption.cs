// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class FitOption : Option<ImageResizer.Models.ResizeFit?>
    {
        public FitOption()
            : base("--fit", "-f")
        {
            Description = Properties.Resources.CLI_Option_Fit;
        }
    }
}
