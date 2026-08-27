// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.JsonRpc.Models;

internal interface IJSPropertyChangeTarget
{
    void ApplyPropertyChanges(string notificationId, JsonElement properties);
}
