// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.Power.Classes;

internal readonly record struct PowerModeSnapshot(
    UserPowerMode UserMode,
    EffectivePowerMode? EffectiveMode,
    Enumerations.PowerSourceKind PowerSourceKind,
    bool HasBattery,
    bool IsOnAcPower,
    bool IsCharging,
    bool CanReadUserMode);
