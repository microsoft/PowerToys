// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal readonly record struct PowerPlanInfo(
    Guid SchemeGuid,
    string DisplayName,
    string Description);
