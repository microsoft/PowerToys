// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ColorPicker.ModuleServices;

public sealed record SavedColor(string Hex, byte A, byte R, byte G, byte B, IReadOnlyList<ColorFormatValue> Formats);
