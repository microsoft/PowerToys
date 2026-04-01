// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Helper;

internal interface IRdpConnectionsManager
{
    IReadOnlyCollection<ConnectionListItem> Connections { get; }
}
