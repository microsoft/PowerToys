// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class WidthOption : Option<double?>
    {
        public WidthOption()
            : base("--width", "-w")
        {
            Description = Properties.Resources.CLI_Option_Width;
            Validators.Add(result =>
            {
                var error = DimensionOptionValidator.Validate(result.Tokens.Count == 1 ? result.Tokens[0].Value : null);
                if (error != null)
                {
                    result.AddError(error);
                }
            });
        }
    }
}
