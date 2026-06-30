// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Enumerations;

namespace Microsoft.CmdPal.Ext.Power.Classes;

internal readonly record struct PowerModeDefinition(
    UserPowerMode Mode,
    Guid Guid,
    string Label,
    string ShortLabel);
