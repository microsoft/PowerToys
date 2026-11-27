// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Common;

/// <summary>An in-process command that captures its palette's input target before running on the command worker.</summary>
public interface ICommandWithInputTarget
{
    Func<ICommandResult> PrepareInvocation(nint sourceHwnd, Guid sourceRouteId);
}
