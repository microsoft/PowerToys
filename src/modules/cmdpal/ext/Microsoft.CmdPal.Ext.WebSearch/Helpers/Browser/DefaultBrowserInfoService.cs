// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser.Providers;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;

/// <summary>
/// Service to get information about the default browser.
/// </summary>
internal class DefaultBrowserInfoService : IBrowserInfoService
{
    private static readonly IDefaultBrowserProvider[] Providers =
    [
        new ShellAssociationProvider(),
        new LegacyRegistryAssociationProvider(),
        new FallbackMsEdgeBrowserProvider(),
    ];

    private readonly Lock _updateLock = new();

    private readonly Dictionary<Type, string> _lastLoggedErrors = [];

    private const long UpdateTimeout = 3000;
    private long _lastUpdateTickCount = -UpdateTimeout;

    private BrowserInfo? _defaultBrowser;

    public BrowserInfo? GetDefaultBrowser()
    {
        try
        {
            UpdateIfTimePassed();
        }
        catch (Exception)
        {
            // exception is already logged at this point
        }

        return _defaultBrowser;
    }

    /// <summary>
    /// Updates only if at least more than 3000ms has passed since the last update, to avoid multiple calls to <see cref="UpdateCore"/>.
    /// (because of multiple plugins calling update at the same time.)
    /// </summary>
    private void UpdateIfTimePassed()
    {
        lock (_updateLock)
        {
            var curTickCount = Environment.TickCount64;
            if (curTickCount - _lastUpdateTickCount < UpdateTimeout && _defaultBrowser != null)
            {
                return;
            }

            var newDefaultBrowser = UpdateCore();
            _defaultBrowser = newDefaultBrowser;
            _lastUpdateTickCount = curTickCount;
        }
    }

    /// <summary>
    /// Consider using <see cref="UpdateIfTimePassed"/> to avoid updating multiple times.
    /// (because of multiple plugins calling update at the same time.)
    /// </summary>
    private BrowserInfo UpdateCore()
    {
        foreach (var provider in Providers)
        {
            try
            {
                var result = provider.GetDefaultBrowserInfo();
#if DEBUG
                result = result with { Name = result.Name + " (" + provider.GetType().Name + ")" };
#endif
                return result;
            }
            catch (Exception ex)
            {
                // since we run this fairly often, avoid logging the same error multiple times
                var lastLoggedError = _lastLoggedErrors.GetValueOrDefault(provider.GetType());
                var error = ex.ToString();
                if (error != lastLoggedError)
                {
                    _lastLoggedErrors[provider.GetType()] = error;
                    Logger.LogError($"Exception when retrieving browser using provider {provider.GetType()}", ex);
                }
            }
        }

        throw new InvalidOperationException("Unable to determine default browser");
    }
}
