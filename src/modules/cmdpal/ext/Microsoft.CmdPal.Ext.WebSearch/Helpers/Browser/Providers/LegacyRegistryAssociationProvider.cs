// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser.Providers;

/// <summary>
/// Provides the default web browser by reading registry keys. This is a legacy method and may not work on all systems.
/// </summary>
internal sealed class LegacyRegistryAssociationProvider : AssociationProviderBase
{
    protected override AssociatedApp? FindAssociation()
    {
        var progId = GetRegistryValue(
                         @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoiceLatest\ProgId",
                         "ProgId")
                     ?? GetRegistryValue(
                         @"HKEY_CURRENT_USER\Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice",
                         "ProgId");
        var appName = GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}\Application", "ApplicationName")
                      ?? GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}", "FriendlyTypeName");

        if (appName is not null)
        {
            appName = GetIndirectString(appName);
            appName = appName
                .Replace("URL", null, StringComparison.OrdinalIgnoreCase)
                .Replace("HTML", null, StringComparison.OrdinalIgnoreCase)
                .Replace("Document", null, StringComparison.OrdinalIgnoreCase)
                .Replace("Web", null, StringComparison.OrdinalIgnoreCase)
                .TrimEnd();
        }

        var commandPattern = GetRegistryValue($@"HKEY_CLASSES_ROOT\{progId}\shell\open\command", null);

        return commandPattern is null ? null : new AssociatedApp(commandPattern, appName);

        static string? GetRegistryValue(string registryLocation, string? valueName)
        {
            return Registry.GetValue(registryLocation, valueName, null) as string;
        }
    }
}
