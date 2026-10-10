// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

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
                var value = result.GetValueOrDefault<int?>();
                if (value.HasValue && value.Value < 0)
                {
                    result.AddError("Size index must be a non-negative integer.");
                }
            });
        }
    }
}
