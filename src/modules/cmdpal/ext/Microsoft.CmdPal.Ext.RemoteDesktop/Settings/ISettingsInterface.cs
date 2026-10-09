// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ToolkitSettings = Microsoft.CommandPalette.Extensions.Toolkit.Settings;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Settings;

internal interface ISettingsInterface
{
    public IReadOnlyCollection<string> PredefinedConnections { get; }

    public ToolkitSettings Settings { get; }
}
