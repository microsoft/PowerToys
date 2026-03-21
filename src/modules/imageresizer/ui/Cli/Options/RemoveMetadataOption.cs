// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class RemoveMetadataOption : Option<bool>
    {
        private static readonly string[] _aliases = ["--remove-metadata"];

        public RemoveMetadataOption()
            : base(_aliases[0], Properties.Resources.CLI_Option_RemoveMetadata)
        {
        }
    }
}
