// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell.Commands;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

internal static class ResourceLoaderInstance
{
    public static string GetString(string resourceKey)
    {
        return Properties.Resources.ResourceManager.GetString(resourceKey, Properties.Resources.Culture) ?? throw new InvalidOperationException($"Resource key '{resourceKey}' not found.");
    }
}
