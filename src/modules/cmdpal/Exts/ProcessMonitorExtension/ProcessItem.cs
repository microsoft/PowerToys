// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace ProcessMonitorExtension;

internal sealed class ProcessItem
{
    internal Process Process { get; init; }

    internal int ProcessId { get; init; }

    internal string Name { get; init; }

    internal string ExePath { get; init; }

    internal long Memory { get; init; }

    internal long CPU { get; init; }
}
