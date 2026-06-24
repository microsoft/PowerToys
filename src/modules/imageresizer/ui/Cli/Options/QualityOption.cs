// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class QualityOption : Option<int?>
    {
        private static readonly string[] _aliases = ["--quality", "-q"];

        public QualityOption()
            : base(_aliases, Properties.Resources.CLI_Option_Quality)
        {
            AddValidator(result =>
            {
                var value = result.GetValueOrDefault<int?>();
                if (value.HasValue && (value.Value < 1 || value.Value > 100))
                {
                    result.ErrorMessage = "JPEG quality must be between 1 and 100.";
                }
            });
        }
    }
}
