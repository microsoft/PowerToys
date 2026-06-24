// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace ImageResizer.Cli.Options
{
    public sealed class FilesArgument : Argument<string[]>
    {
        public FilesArgument()
            : base("files", Properties.Resources.CLI_Option_Files)
        {
            Arity = ArgumentArity.ZeroOrMore;
        }
    }
}
