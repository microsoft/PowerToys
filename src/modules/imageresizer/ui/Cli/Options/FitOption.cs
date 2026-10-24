// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Globalization;

namespace ImageResizer.Cli.Options
{
    public sealed class FitOption : Option<ImageResizer.Models.ResizeFit?>
    {
        public FitOption()
            : base("--fit", "-f")
        {
            Description = Properties.Resources.CLI_Option_Fit;
            Validators.Add(result =>
            {
                if (result.Tokens.Count == 1 &&
                    Enum.TryParse<ImageResizer.Models.ResizeFit>(result.Tokens[0].Value, ignoreCase: true, out var value) &&
                    !Enum.IsDefined(value))
                {
                    result.AddError(string.Format(
                        CultureInfo.InvariantCulture,
                        Properties.Resources.CLI_ErrorInvalidEnumValue,
                        result.Tokens[0].Value,
                        result.Option.Name));
                }
            });
        }
    }
}
