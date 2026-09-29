// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Command to test the resource state.
/// </summary>
public sealed class TestCommand : BaseCommand
{
    public TestCommand()
        : base("test", Resources.TestCommandDescription)
    {
    }

    /// <inheritdoc/>
    public override int CommandHandlerInternal(ParseResult parseResult)
    {
        return Resource!.TestState(Input) ? 0 : 1;
    }
}
