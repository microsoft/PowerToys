// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Converts optional unsigned integer values for the fallback v2 contract.
/// </summary>
public static class OptionalUInt32Extensions
{
    public static OptionalUInt32 ToOptionalUInt32(this uint? value) => new()
    {
        HasValue = value.HasValue,
        Value = value.GetValueOrDefault(),
    };

    public static uint? ToNullableUInt32(this OptionalUInt32 value) => value.HasValue ? value.Value : null;
}
