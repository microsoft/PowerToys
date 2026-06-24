// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class SizeOption : Option<int?>
    {
        private static readonly string[] _aliases = ["--size"];

        public SizeOption()
            : base(_aliases, Properties.Resources.CLI_Option_Size)
        {
            AddValidator(result =>
            {
                var value = result.GetValueOrDefault<int?>();
                if (value.HasValue && value.Value < 0)
                {
                    result.ErrorMessage = "Size index must be a non-negative integer.";
                }
            });
        }
    }
}
