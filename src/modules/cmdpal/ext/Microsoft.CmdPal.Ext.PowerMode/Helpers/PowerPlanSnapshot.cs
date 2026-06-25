// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal readonly record struct PowerPlanSnapshot(
    PowerPlanInfo? ActivePlan,
    IReadOnlyList<PowerPlanInfo> AvailablePlans,
    bool CanReadPlans,
    bool CanSetPlans);
