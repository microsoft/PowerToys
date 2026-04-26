// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToys.MacroCommon.Models;

public enum StepType
{
    [JsonStringEnumMemberName("press_key")]
    PressKey,

    [JsonStringEnumMemberName("type_text")]
    TypeText,

    [JsonStringEnumMemberName("wait")]
    Wait,

    [JsonStringEnumMemberName("repeat")]
    Repeat,
}
