// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.Globalization;

namespace ImageResizer.Cli.Options
{
    public sealed class SizeOption : Option<int?>
    {
        public SizeOption()
            : base("--size")
        {
            Description = Properties.Resources.CLI_Option_Size;
            Validators.Add(result =>
            {
                // Reading the value of an unparsable token throws inside a validator, so parse it the
                // same way System.CommandLine does and let it report its own error for invalid input.
                if (result.Tokens.Count == 1
                    && int.TryParse(result.Tokens[0].Value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value)
                    && value < 0)
                {
                    result.AddError("Size index must be a non-negative integer.");
                }
            });
        }
    }
}
