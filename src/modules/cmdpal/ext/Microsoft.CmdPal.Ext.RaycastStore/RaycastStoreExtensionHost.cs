// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;

namespace Microsoft.CmdPal.Ext.RaycastStore;

public partial class RaycastStoreExtensionHost
{
    internal static ExtensionHostInstance Instance { get; } = new();
}
