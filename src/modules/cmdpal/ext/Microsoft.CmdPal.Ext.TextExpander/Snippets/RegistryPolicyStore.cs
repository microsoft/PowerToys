// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>Reads policies from <c>SOFTWARE\Policies\PowerToys</c>, the machine's before the user's, as gpo.h does.</summary>
internal sealed class RegistryPolicyStore : IPolicyStore
{
    private const string PoliciesKey = @"SOFTWARE\Policies\PowerToys";

    public int? Read(string valueName) => ReadFrom(Registry.LocalMachine, valueName) ?? ReadFrom(Registry.CurrentUser, valueName);

    private static int? ReadFrom(RegistryKey root, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(PoliciesKey, writable: false);
            return key?.GetValue(valueName) as int?;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
