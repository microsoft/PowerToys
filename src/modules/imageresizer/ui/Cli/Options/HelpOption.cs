// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class HelpOption : Option<bool>
    {
        private static readonly string[] _aliases = ["--help", "-?", "/?"];

        public HelpOption()
            : base(_aliases[0], Properties.Resources.CLI_Option_Help)
        {
        }
    }
}
