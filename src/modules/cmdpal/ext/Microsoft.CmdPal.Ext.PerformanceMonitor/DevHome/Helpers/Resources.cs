// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CoreWidgetProvider.Helpers;

// This class was pilfered from devhome, but changed much more substantially to
// get the resources out of our resources.pri the way we need.
public static partial class Resources
{
    private static readonly Windows.ApplicationModel.Resources.Core.ResourceMap? _map;

    private static readonly string ResourcesPath = "Microsoft.CmdPal.Ext.PerformanceMonitor/Resources";

    static Resources()
    {
        try
        {
            var currentResourceManager = Windows.ApplicationModel.Resources.Core.ResourceManager.Current;
            if (currentResourceManager.MainResourceMap is not null)
            {
                _map = currentResourceManager.MainResourceMap;
            }
        }
        catch (Exception)
        {
            // Resource map not available (e.g., during unit tests)
            _map = null;
        }
    }

    public static string GetResource(string identifier, ILogger? logger = null)
    {
        if (_map is null)
        {
            return identifier;
        }

        var fullKey = $"{ResourcesPath}/{identifier}";

        var val = _map.GetValue(fullKey);
#if DEBUG
        if (val == null)
        {
            if (logger is not null)
            {
                Log_FailedLoadingResource(logger, identifier);
            }

            if (logger is not null)
            {
                DebugResources(logger);
            }
        }
#endif
        return val!.ValueAsString;
    }

    public static string ReplaceIdentifersFast(
        string original,
        ILogger? logger = null)
    {
        // walk the string, looking for a pair of '%' characters
        StringBuilder sb = new();
        var length = original.Length;
        for (var i = 0; i < length; i++)
        {
            if (original[i] == '%')
            {
                var end = original.IndexOf('%', i + 1);
                if (end > i)
                {
                    var identifier = original.Substring(i + 1, end - i - 1);
                    var resourceString = GetResource(identifier, logger);
                    sb.Append(resourceString);
                    i = end; // move index to the end '%'
                    continue;
                }
            }

            sb.Append(original[i]);
        }

        return sb.ToString();
    }

    private static void DebugResources(ILogger logger)
    {
        var currentResourceManager = Windows.ApplicationModel.Resources.Core.ResourceManager.Current;
        StringBuilder sb = new();

        foreach (var (k, v) in currentResourceManager.AllResourceMaps)
        {
            sb.AppendLine(k);
            foreach (var (k2, v2) in v)
            {
                sb.Append('\t');
                sb.AppendLine(k2);
            }

            sb.AppendLine();
        }

        Log_ResourceMaps(logger, sb.ToString());
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed loading resource: {Identifier}")]
    static partial void Log_FailedLoadingResource(ILogger logger, string identifier);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resource maps:\n{ResourceMaps}")]
    static partial void Log_ResourceMaps(ILogger logger, string resourceMaps);
}
