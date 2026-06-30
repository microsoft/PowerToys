// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.Power.Classes;

internal readonly record struct EnergySaverSignals(
    bool HasWinRt,
    EnergySaverStatus WinRtStatus,
    bool HasOverlay,
    Guid OverlayGuid,
    bool HasSystemStatus,
    bool SystemOn,
    bool HasRegistry,
    bool RegistryOn);
