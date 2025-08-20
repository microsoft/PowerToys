﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using EnvironmentVariablesUILib.Models;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace EnvironmentVariables.Telemetry
{
    [EventData]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public class EnvironmentVariablesVariableChangedEvent : EventBase, IEvent
    {
        public VariablesSetType VariablesType { get; set; }

        public EnvironmentVariablesVariableChangedEvent(VariablesSetType type)
        {
            this.VariablesType = type;
        }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
