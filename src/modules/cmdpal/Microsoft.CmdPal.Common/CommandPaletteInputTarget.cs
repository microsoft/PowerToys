// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common;

/// <summary>The external window a palette was opened over and the action that releases its source palette.</summary>
public sealed record CommandPaletteInputTarget(nint Window, uint ProcessId, Func<CancellationToken, Task<bool>> ReleaseForeground);
