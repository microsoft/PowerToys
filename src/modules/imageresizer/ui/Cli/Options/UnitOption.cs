// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Globalization;

namespace ImageResizer.Cli.Options
{
    public sealed class UnitOption : Option<ImageResizer.Models.ResizeUnit?>
    {
        public UnitOption()
            : base("--unit", "-u")
        {
            Description = Properties.Resources.CLI_Option_Unit;
            Validators.Add(result =>
            {
                if (result.Tokens.Count == 1 &&
                    Enum.TryParse<ImageResizer.Models.ResizeUnit>(result.Tokens[0].Value, ignoreCase: true, out var value) &&
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
