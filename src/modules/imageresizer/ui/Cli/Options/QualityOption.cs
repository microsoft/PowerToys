// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.Globalization;

namespace ImageResizer.Cli.Options
{
    public sealed class QualityOption : Option<int?>
    {
        public QualityOption()
            : base("--quality", "-q")
        {
            Description = Properties.Resources.CLI_Option_Quality;
            Validators.Add(result =>
            {
                // Reading the value of an unparsable token throws inside a validator, so parse it the
                // same way System.CommandLine does and let it report its own error for invalid input.
                if (result.Tokens.Count == 1
                    && int.TryParse(result.Tokens[0].Value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value)
                    && (value < 1 || value > 100))
                {
                    result.AddError("JPEG quality must be between 1 and 100.");
                }
            });
        }
    }
}
