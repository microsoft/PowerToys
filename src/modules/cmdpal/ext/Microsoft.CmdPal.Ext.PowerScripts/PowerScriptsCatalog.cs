// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal sealed record PowerScriptsCatalog(
    IReadOnlyList<PowerScriptInfo> Scripts,
    bool IsEnabled,
    string? DisabledReason = null,
    bool IsPolicyControlled = false);
