// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Logging;

/// <summary>
/// An <see cref="MEL.ILoggerProvider"/> that creates <see cref="CmdPalLogger"/> instances
/// backed by the <see cref="ManagedCommon.Logger"/> infrastructure.
/// Register via <see cref="CmdPalLoggingExtensions.AddCmdPalLogging"/>.
/// </summary>
public sealed partial class CmdPalLoggerProvider : MEL.ILoggerProvider
{
    private readonly ConcurrentDictionary<string, CmdPalLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public MEL.ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new CmdPalLogger(name));

    public void Dispose() => _loggers.Clear();
}
