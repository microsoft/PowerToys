// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

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
                var value = result.GetValueOrDefault<int?>();
                if (value.HasValue && (value.Value < 1 || value.Value > 100))
                {
                    result.AddError("JPEG quality must be between 1 and 100.");
                }
            });
        }
    }
}
