// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Logging;

public sealed partial class CmdPalLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minLevel;

    public CmdPalLoggerProvider(LogLevel minLevel = LogLevel.Information)
    {
        _minLevel = minLevel;
    }

    public ILogger CreateLogger(string categoryName)
        => new CmdPalLogger(categoryName, _minLevel);

    public void Dispose()
    {
        // No resources to dispose
    }
}
